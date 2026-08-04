using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.EditorCloud
{
    public sealed partial class CloudService
    {
        public UniTask<CloudObjectGetResult> GetObjectAsync(
            CloudObjectGetRequest request,
            CancellationToken cancellationToken)
        {
            return GetObjectAsync(ResolveGetContext(request), cancellationToken);
        }

        internal async UniTask<CloudObjectGetResult> GetObjectAsync(
            CloudGetObjectContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateGetContext(context);
            var provider = m_ProviderRegistry.Resolve(context.ProviderId) as ICloudReadProvider;
            if (provider == null)
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    $"Cloud provider '{context.ProviderId}' does not support object reads.",
                    context.ProviderId);
            }

            if (m_HttpTransport is not ICloudHttpReadTransport transport)
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    "Cloud HTTP transport does not support object reads.",
                    context.ProviderId);
            }

            provider.Validate(context);
            for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var request = provider.CreateGetObjectRequest(context);
                CloudHttpResponse response = null;
                CloudException failure = null;
                try
                {
                    response = await transport.SendAsync(request, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (CloudException exception)
                {
                    failure = exception;
                }

                if (failure == null && response == null)
                {
                    failure = new CloudException(
                        CloudFailureKind.ProviderResponse,
                        $"Cloud provider '{context.ProviderId}' returned no GET response.",
                        context.ProviderId);
                }

                if (failure == null && response.IsSuccessStatusCode is false)
                {
                    failure = ClassifyGetResponse(context, response);
                }

                if (failure == null)
                {
                    return new CloudObjectGetResult(
                        context.ProviderId,
                        context.Bucket,
                        context.Request.ObjectKey,
                        response.GetHeader("ETag"),
                        FirstNonEmpty(
                            response.GetHeader("x-cos-request-id"),
                            response.GetHeader("x-oss-request-id"),
                            response.GetHeader("x-request-id")),
                        response.Body);
                }

                if (attempt >= MaximumAttempts || IsRetryable(failure) is false)
                {
                    throw failure;
                }

                await m_RetryDelay(
                    TimeSpan.FromMilliseconds(250 * (1 << (attempt - 1))),
                    cancellationToken);
            }

            throw new InvalidOperationException("Cloud GET retry loop completed without a result.");
        }

        private CloudGetObjectContext ResolveGetContext(CloudObjectGetRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

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

            return new CloudGetObjectContext(
                config.ProviderId,
                config.Bucket,
                config.Region,
                config.Endpoint,
                credential,
                request);
        }

        private static void ValidateGetContext(CloudGetObjectContext context)
        {
            if (context?.Request == null)
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    "Cloud GET context and request are required.",
                    context?.ProviderId);
            }

            ValidateObjectKey(context.Request.ObjectKey, context.ProviderId);
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

        private static CloudException ClassifyGetResponse(
            CloudGetObjectContext context,
            CloudHttpResponse response)
        {
            var kind = response.StatusCode switch
            {
                401 => CloudFailureKind.Authentication,
                403 => CloudFailureKind.Permission,
                404 => CloudFailureKind.NotFound,
                408 => CloudFailureKind.Network,
                429 => CloudFailureKind.RateLimited,
                _ => CloudFailureKind.ProviderResponse
            };
            return new CloudException(
                kind,
                $"Cloud GET failed for '{context.Request.ObjectKey}' with HTTP {response.StatusCode}.",
                context.ProviderId,
                response.StatusCode,
                FirstNonEmpty(
                    response.GetHeader("x-cos-request-id"),
                    response.GetHeader("x-oss-request-id"),
                    response.GetHeader("x-request-id")));
        }
    }
}
