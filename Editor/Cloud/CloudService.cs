using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorConfiguration;
using IOFile = System.IO.File;
using IOFileInfo = System.IO.FileInfo;

namespace GameDeveloperKit.EditorCloud
{
    public interface ICloudHttpTransport
    {
        UniTask<CloudHttpResponse> SendAsync(
            CloudHttpRequest request,
            CloudObjectUploadRequest upload,
            IProgress<CloudUploadProgress> progress,
            CancellationToken cancellationToken);
    }

    public interface ICloudHttpReadTransport
    {
        UniTask<CloudHttpResponse> SendAsync(
            CloudHttpRequest request,
            CancellationToken cancellationToken);
    }

    public sealed partial class CloudService
    {
        private const int MaximumAttempts = 3;
        private static readonly Lazy<CloudService> s_Shared = new Lazy<CloudService>(() =>
            new CloudService(
                CloudProviderRegistry.CreateBuiltIn(),
                new CloudHttpTransport(),
                () => EditorGlobalConfig.LoadOrCreate().Cloud,
                new CloudCredentialStore()));
        private readonly CloudProviderRegistry m_ProviderRegistry;
        private readonly ICloudHttpTransport m_HttpTransport;
        private readonly Func<CloudProjectConfig> m_ProjectConfigProvider;
        private readonly CloudCredentialStore m_CredentialStore;
        private readonly Func<TimeSpan, CancellationToken, UniTask> m_RetryDelay;

        public static CloudService Shared => s_Shared.Value;

        public CloudService(
            CloudProviderRegistry providerRegistry,
            ICloudHttpTransport httpTransport)
        {
            m_ProviderRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
            m_HttpTransport = httpTransport ?? throw new ArgumentNullException(nameof(httpTransport));
            m_RetryDelay = DelayRetryAsync;
        }

        internal CloudService(
            CloudProviderRegistry providerRegistry,
            ICloudHttpTransport httpTransport,
            Func<TimeSpan, CancellationToken, UniTask> retryDelay)
            : this(providerRegistry, httpTransport)
        {
            m_RetryDelay = retryDelay ?? throw new ArgumentNullException(nameof(retryDelay));
        }

        public CloudService(
            CloudProviderRegistry providerRegistry,
            ICloudHttpTransport httpTransport,
            Func<CloudProjectConfig> projectConfigProvider,
            CloudCredentialStore credentialStore)
            : this(providerRegistry, httpTransport)
        {
            m_ProjectConfigProvider = projectConfigProvider ??
                                      throw new ArgumentNullException(nameof(projectConfigProvider));
            m_CredentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        }

        internal CloudService(
            CloudProviderRegistry providerRegistry,
            ICloudHttpTransport httpTransport,
            Func<CloudProjectConfig> projectConfigProvider,
            CloudCredentialStore credentialStore,
            Func<TimeSpan, CancellationToken, UniTask> retryDelay)
            : this(providerRegistry, httpTransport, projectConfigProvider, credentialStore)
        {
            m_RetryDelay = retryDelay ?? throw new ArgumentNullException(nameof(retryDelay));
        }

        public UniTask<CloudUploadResult> UploadObjectAsync(
            CloudObjectUploadRequest request,
            IProgress<CloudUploadProgress> progress,
            CancellationToken cancellationToken)
        {
            return UploadObjectAsync(
                ResolveContext(request),
                progress,
                cancellationToken);
        }

        public async UniTask<CloudBatchUploadResult> UploadBatchAsync(
            CloudBatchUploadRequest request,
            IProgress<CloudUploadProgress> progress,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var contexts = new CloudPutObjectContext[request.Objects.Count];
            var objectKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < request.Objects.Count; i++)
            {
                contexts[i] = ResolveContext(request.Objects[i]);
                ValidateProviderContext(contexts[i]);
                if (objectKeys.Add(contexts[i].Request.ObjectKey) is false)
                {
                    throw new CloudException(
                        CloudFailureKind.InvalidConfiguration,
                        $"Batch upload contains duplicate object key '{contexts[i].Request.ObjectKey}'.",
                        contexts[i].ProviderId);
                }
            }

            if (contexts.Length == 0)
            {
                return new CloudBatchUploadResult(
                    Array.Empty<CloudUploadResult>(),
                    new Dictionary<string, CloudException>(StringComparer.Ordinal));
            }

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var state = new BatchUploadState(contexts, progress);
            var workers = new UniTask[Math.Min(request.MaxConcurrency, contexts.Length)];
            for (var i = 0; i < workers.Length; i++)
            {
                workers[i] = RunBatchWorkerAsync(
                    state,
                    linkedCancellation,
                    cancellationToken);
            }

            await UniTask.WhenAll(workers);
            cancellationToken.ThrowIfCancellationRequested();
            return state.CreateResult();
        }

        private CloudPutObjectContext ResolveContext(CloudObjectUploadRequest request)
        {
            if (m_ProjectConfigProvider == null || m_CredentialStore == null)
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    "CloudService project configuration is not available.");
            }

            var config = m_ProjectConfigProvider();
            if (config == null)
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    "Cloud project configuration is missing.");
            }

            if (m_CredentialStore.TryGet(
                    config.ProviderId,
                    config.CredentialProfileName,
                    out var credential) is false)
            {
                throw new CloudException(
                    CloudFailureKind.CredentialsMissing,
                    $"Credential profile '{config.CredentialProfileName}' is missing for provider '{config.ProviderId}'.",
                    config.ProviderId);
            }

            return new CloudPutObjectContext(
                config.ProviderId,
                config.Bucket,
                config.Region,
                config.Endpoint,
                credential,
                request);
        }

        public async UniTask<CloudUploadResult> UploadObjectAsync(
            CloudPutObjectContext context,
            IProgress<CloudUploadProgress> progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var provider = ValidateProviderContext(context);
            var monotonicProgress = new MonotonicUploadProgress(context.Request, progress);

            for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var httpRequest = provider.CreatePutObjectRequest(context);
                ValidateHttpRequest(context.ProviderId, httpRequest);

                CloudHttpResponse response = null;
                CloudException failure = null;
                try
                {
                    response = await m_HttpTransport.SendAsync(
                        httpRequest,
                        context.Request,
                        monotonicProgress,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (CloudException exception)
                {
                    failure = exception;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (failure == null && response == null)
                {
                    failure = new CloudException(
                        CloudFailureKind.ProviderResponse,
                        $"Cloud provider '{context.ProviderId}' returned no response.",
                        context.ProviderId);
                }

                if (failure == null && response.IsSuccessStatusCode is false)
                {
                    failure = ClassifyResponse(context, response);
                }

                if (failure == null)
                {
                    var result = provider.ParsePutObjectResponse(context, response);
                    monotonicProgress.Complete();
                    return result;
                }

                if (attempt >= MaximumAttempts || IsRetryable(failure) is false)
                {
                    throw failure;
                }

                await m_RetryDelay(
                    TimeSpan.FromMilliseconds(250 * (1 << (attempt - 1))),
                    cancellationToken);
            }

            throw new InvalidOperationException("Cloud upload retry loop completed without a result.");
        }

        private async UniTask RunBatchWorkerAsync(
            BatchUploadState state,
            CancellationTokenSource linkedCancellation,
            CancellationToken callerCancellation)
        {
            while (state.TryTakeNext(out var context))
            {
                try
                {
                    var result = await UploadObjectAsync(
                        context,
                        state.CreateProgress(context.Request.ObjectKey),
                        linkedCancellation.Token);
                    state.RecordSuccess(result);
                }
                catch (OperationCanceledException)
                {
                    if (callerCancellation.IsCancellationRequested)
                    {
                        throw;
                    }

                    return;
                }
                catch (CloudException exception)
                {
                    state.RecordFailure(context.Request.ObjectKey, exception);
                    linkedCancellation.Cancel();
                    return;
                }
                catch (Exception exception)
                {
                    state.RecordFailure(
                        context.Request.ObjectKey,
                        new CloudException(
                            CloudFailureKind.ProviderResponse,
                            $"Cloud upload failed with {exception.GetType().Name}.",
                            context.ProviderId,
                            innerException: exception));
                    linkedCancellation.Cancel();
                    return;
                }
            }
        }

        private ICloudProvider ValidateProviderContext(CloudPutObjectContext context)
        {
            ValidateCommonContext(context);
            var provider = m_ProviderRegistry.Resolve(context.ProviderId);
            if ((provider.Capabilities & CloudProviderCapabilities.PutObject) == 0)
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    $"Cloud provider '{context.ProviderId}' does not support object upload.",
                    context.ProviderId);
            }

            if (context.Request.WriteCondition.Kind != CloudWriteConditionKind.None &&
                (provider.Capabilities & CloudProviderCapabilities.ConditionalPut) == 0)
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    $"Cloud provider '{context.ProviderId}' does not support conditional object upload.",
                    context.ProviderId);
            }

            provider.Validate(context);
            return provider;
        }

        private static void ValidateHttpRequest(string providerId, CloudHttpRequest request)
        {
            if (request == null ||
                request.Uri.IsAbsoluteUri is false ||
                string.Equals(request.Uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) is false)
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    $"Cloud provider '{providerId}' produced an invalid HTTPS endpoint.",
                    providerId);
            }
        }

        private static CloudException ClassifyResponse(
            CloudPutObjectContext context,
            CloudHttpResponse response)
        {
            var requestId = FirstNonEmpty(
                response.GetHeader("x-cos-request-id"),
                response.GetHeader("x-oss-request-id"),
                response.GetHeader("x-request-id"));
            var kind = response.StatusCode switch
            {
                401 => CloudFailureKind.Authentication,
                403 => CloudFailureKind.Permission,
                404 => CloudFailureKind.NotFound,
                408 => CloudFailureKind.Network,
                409 => CloudFailureKind.PreconditionFailed,
                412 => CloudFailureKind.PreconditionFailed,
                429 => CloudFailureKind.RateLimited,
                _ => CloudFailureKind.ProviderResponse
            };
            var message =
                $"Cloud upload failed for '{context.Request.ObjectKey}' with HTTP {response.StatusCode}." +
                CloudEndpointContext.Format(context.Bucket, context.Region, context.Endpoint);
            if (response.StatusCode == 404)
            {
                message += " PUT 404 usually means the bucket does not exist or the endpoint does not include the bucket.";
            }

            return new CloudException(
                kind,
                message,
                context.ProviderId,
                response.StatusCode,
                requestId);
        }

        private static bool IsRetryable(CloudException exception)
        {
            return exception.Kind == CloudFailureKind.Network ||
                   exception.Kind == CloudFailureKind.RateLimited ||
                   exception.StatusCode >= 500;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (var i = 0; i < values.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(values[i]) is false)
                {
                    return values[i];
                }
            }

            return string.Empty;
        }

        private static UniTask DelayRetryAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            return UniTask.Delay(delay, cancellationToken: cancellationToken);
        }

        private static void ValidateCommonContext(CloudPutObjectContext context)
        {
            if (context == null)
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    "Cloud upload context is required.");
            }

            if (context.Request == null)
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    "Cloud object upload request is required.",
                    context.ProviderId);
            }

            ValidateObjectKey(context.Request.ObjectKey, context.ProviderId);

            if (IOFile.Exists(context.Request.LocalFilePath) is false)
            {
                throw new CloudException(
                    CloudFailureKind.LocalFile,
                    $"Local upload file does not exist: {context.Request.LocalFilePath}",
                    context.ProviderId);
            }

            if (context.Credential == null ||
                string.IsNullOrWhiteSpace(context.Credential.AccessKeyId) ||
                string.IsNullOrWhiteSpace(context.Credential.SecretAccessKey))
            {
                throw new CloudException(
                    CloudFailureKind.CredentialsMissing,
                    $"Cloud credentials are missing for provider '{context.ProviderId}'.",
                    context.ProviderId);
            }
        }

        private static void ValidateObjectKey(string objectKey, string providerId)
        {
            if (string.IsNullOrWhiteSpace(objectKey) ||
                objectKey[0] == '/' ||
                objectKey.IndexOf('\\') >= 0 ||
                Uri.TryCreate(objectKey, UriKind.Absolute, out _))
            {
                ThrowInvalidObjectKey(providerId);
            }

            var segments = objectKey.Split('/');
            for (var i = 0; i < segments.Length; i++)
            {
                if (segments[i].Length == 0 ||
                    string.Equals(segments[i], ".", StringComparison.Ordinal) ||
                    string.Equals(segments[i], "..", StringComparison.Ordinal))
                {
                    ThrowInvalidObjectKey(providerId);
                }
            }

            for (var i = 0; i < objectKey.Length; i++)
            {
                if (char.IsControl(objectKey[i]))
                {
                    ThrowInvalidObjectKey(providerId);
                }
            }
        }

        private static void ThrowInvalidObjectKey(string providerId)
        {
            throw new CloudException(
                CloudFailureKind.InvalidConfiguration,
                "Object key must be a relative path without empty, dot, backslash, URL, or control-character segments.",
                providerId);
        }

        private sealed class MonotonicUploadProgress : IProgress<CloudUploadProgress>
        {
            private readonly CloudObjectUploadRequest m_Request;
            private readonly IProgress<CloudUploadProgress> m_Target;
            private readonly long m_TotalBytes;
            private long m_ReportedBytes;

            public MonotonicUploadProgress(
                CloudObjectUploadRequest request,
                IProgress<CloudUploadProgress> target)
            {
                m_Request = request;
                m_Target = target;
                m_TotalBytes = new IOFileInfo(request.LocalFilePath).Length;
            }

            public void Report(CloudUploadProgress value)
            {
                if (m_Target == null || value == null)
                {
                    return;
                }

                var sent = Math.Max(0, Math.Min(value.ObjectBytesSent, m_TotalBytes));
                if (sent <= m_ReportedBytes)
                {
                    return;
                }

                m_ReportedBytes = sent;
                m_Target.Report(new CloudUploadProgress(
                    m_Request.ObjectKey,
                    sent,
                    m_TotalBytes,
                    sent,
                    m_TotalBytes));
            }

            public void Complete()
            {
                Report(new CloudUploadProgress(
                    m_Request.ObjectKey,
                    m_TotalBytes,
                    m_TotalBytes,
                    m_TotalBytes,
                    m_TotalBytes));
            }
        }

        private sealed class BatchUploadState
        {
            private readonly object m_Gate = new object();
            private readonly CloudPutObjectContext[] m_Contexts;
            private readonly IProgress<CloudUploadProgress> m_Progress;
            private readonly Dictionary<string, long> m_TotalBytesByKey =
                new Dictionary<string, long>(StringComparer.Ordinal);
            private readonly Dictionary<string, long> m_ReportedBytesByKey =
                new Dictionary<string, long>(StringComparer.Ordinal);
            private readonly Dictionary<string, CloudUploadResult> m_Succeeded =
                new Dictionary<string, CloudUploadResult>(StringComparer.Ordinal);
            private readonly Dictionary<string, CloudException> m_Failed =
                new Dictionary<string, CloudException>(StringComparer.Ordinal);
            private int m_NextIndex;
            private long m_TotalBytes;
            private long m_ReportedBytes;

            public BatchUploadState(
                CloudPutObjectContext[] contexts,
                IProgress<CloudUploadProgress> progress)
            {
                m_Contexts = contexts;
                m_Progress = progress;
                foreach (var context in contexts)
                {
                    var length = new IOFileInfo(context.Request.LocalFilePath).Length;
                    m_TotalBytesByKey.Add(context.Request.ObjectKey, length);
                    m_ReportedBytesByKey.Add(context.Request.ObjectKey, 0);
                    m_TotalBytes += length;
                }
            }

            public bool TryTakeNext(out CloudPutObjectContext context)
            {
                lock (m_Gate)
                {
                    if (m_Failed.Count > 0 || m_NextIndex >= m_Contexts.Length)
                    {
                        context = null;
                        return false;
                    }

                    context = m_Contexts[m_NextIndex++];
                    return true;
                }
            }

            public IProgress<CloudUploadProgress> CreateProgress(string objectKey)
            {
                return new CallbackProgress(value => Report(objectKey, value));
            }

            public void RecordSuccess(CloudUploadResult result)
            {
                lock (m_Gate)
                {
                    m_Succeeded[result.ObjectKey] = result;
                }
            }

            public void RecordFailure(string objectKey, CloudException exception)
            {
                lock (m_Gate)
                {
                    if (m_Failed.Count == 0)
                    {
                        m_Failed.Add(objectKey, exception);
                    }
                }
            }

            public CloudBatchUploadResult CreateResult()
            {
                lock (m_Gate)
                {
                    var succeeded = new List<CloudUploadResult>();
                    foreach (var context in m_Contexts)
                    {
                        if (m_Succeeded.TryGetValue(context.Request.ObjectKey, out var result))
                        {
                            succeeded.Add(result);
                        }
                    }

                    return new CloudBatchUploadResult(
                        succeeded,
                        new Dictionary<string, CloudException>(m_Failed, StringComparer.Ordinal));
                }
            }

            private void Report(string objectKey, CloudUploadProgress value)
            {
                if (m_Progress == null || value == null)
                {
                    return;
                }

                lock (m_Gate)
                {
                    var objectTotal = m_TotalBytesByKey[objectKey];
                    var sent = Math.Max(0, Math.Min(value.ObjectBytesSent, objectTotal));
                    var previous = m_ReportedBytesByKey[objectKey];
                    if (sent <= previous)
                    {
                        return;
                    }

                    m_ReportedBytesByKey[objectKey] = sent;
                    m_ReportedBytes += sent - previous;
                    m_Progress.Report(new CloudUploadProgress(
                        objectKey,
                        sent,
                        objectTotal,
                        m_ReportedBytes,
                        m_TotalBytes));
                }
            }

            private sealed class CallbackProgress : IProgress<CloudUploadProgress>
            {
                private readonly Action<CloudUploadProgress> m_Report;

                public CallbackProgress(Action<CloudUploadProgress> report)
                {
                    m_Report = report;
                }

                public void Report(CloudUploadProgress value)
                {
                    m_Report(value);
                }
            }
        }
    }
}
