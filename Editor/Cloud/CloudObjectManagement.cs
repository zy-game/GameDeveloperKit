using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.EditorCloud
{
    public sealed partial class CloudService
    {
        public UniTask<CloudObjectPage> ListObjectsAsync(
            CloudObjectListRequest request,
            CancellationToken cancellationToken)
        {
            return ListObjectsAsync(ResolveListContext(request), cancellationToken);
        }

        public UniTask<CloudDeleteResult> DeleteObjectAsync(
            CloudObjectDeleteRequest request,
            CancellationToken cancellationToken)
        {
            return DeleteObjectAsync(ResolveDeleteContext(request), cancellationToken);
        }

        internal async UniTask<CloudObjectPage> ListObjectsAsync(
            CloudListObjectsContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateListContext(context);
            var registered = m_ProviderRegistry.Resolve(context.ProviderId);
            if ((registered.Capabilities & CloudProviderCapabilities.ListObjects) == 0 ||
                registered is not ICloudListProvider provider)
            {
                throw UnsupportedCapability(context.ProviderId, "object listing");
            }

            if (m_HttpTransport is not ICloudHttpReadTransport transport)
            {
                throw UnsupportedTransport(context.ProviderId);
            }

            provider.Validate(context);
            for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var request = provider.CreateListObjectsRequest(context);
                ValidateHttpRequest(context.ProviderId, request);
                var outcome = await SendManagementRequestAsync(
                    transport,
                    request,
                    context.ProviderId,
                    "LIST",
                    context.Request.Prefix,
                    context.Bucket,
                    context.Region,
                    context.Endpoint,
                    cancellationToken);
                if (outcome.Failure == null)
                {
                    var page = provider.ParseListObjectsResponse(context, outcome.Response);
                    ValidateListPage(context, page);
                    return page;
                }

                if (attempt >= MaximumAttempts || IsRetryable(outcome.Failure) is false)
                {
                    throw outcome.Failure;
                }

                await m_RetryDelay(
                    TimeSpan.FromMilliseconds(250 * (1 << (attempt - 1))),
                    cancellationToken);
            }

            throw new InvalidOperationException("Cloud LIST retry loop completed without a result.");
        }

        internal async UniTask<CloudDeleteResult> DeleteObjectAsync(
            CloudDeleteObjectContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateDeleteContext(context);
            var registered = m_ProviderRegistry.Resolve(context.ProviderId);
            if ((registered.Capabilities & CloudProviderCapabilities.DeleteObject) == 0 ||
                registered is not ICloudDeleteProvider provider)
            {
                throw UnsupportedCapability(context.ProviderId, "object deletion");
            }

            if (m_HttpTransport is not ICloudHttpReadTransport transport)
            {
                throw UnsupportedTransport(context.ProviderId);
            }

            provider.Validate(context);
            for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var request = provider.CreateDeleteObjectRequest(context);
                ValidateHttpRequest(context.ProviderId, request);
                var outcome = await SendManagementRequestAsync(
                    transport,
                    request,
                    context.ProviderId,
                    "DELETE",
                    context.Request.ObjectKey,
                    context.Bucket,
                    context.Region,
                    context.Endpoint,
                    cancellationToken,
                    true);
                if (outcome.Failure == null)
                {
                    return provider.ParseDeleteObjectResponse(
                        context,
                        outcome.Response,
                        outcome.Response.StatusCode != 404);
                }

                if (attempt >= MaximumAttempts || IsRetryable(outcome.Failure) is false)
                {
                    throw outcome.Failure;
                }

                await m_RetryDelay(
                    TimeSpan.FromMilliseconds(250 * (1 << (attempt - 1))),
                    cancellationToken);
            }

            throw new InvalidOperationException("Cloud DELETE retry loop completed without a result.");
        }

        private async UniTask<ManagementRequestOutcome> SendManagementRequestAsync(
            ICloudHttpReadTransport transport,
            CloudHttpRequest request,
            string providerId,
            string operation,
            string target,
            string bucket,
            string region,
            string endpoint,
            CancellationToken cancellationToken,
            bool notFoundIsSuccess = false)
        {
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
                    $"Cloud provider '{providerId}' returned no {operation} response.",
                    providerId);
            }
            else if (failure == null &&
                     response.IsSuccessStatusCode is false &&
                     (notFoundIsSuccess is false || response.StatusCode != 404))
            {
                failure = ClassifyManagementResponse(
                    providerId,
                    operation,
                    target,
                    bucket,
                    region,
                    endpoint,
                    response);
            }

            return new ManagementRequestOutcome(response, failure);
        }

        private CloudListObjectsContext ResolveListContext(CloudObjectListRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            ResolveConfiguration(out var config, out var credential);
            return new CloudListObjectsContext(
                config.ProviderId,
                config.Bucket,
                config.Region,
                config.Endpoint,
                credential,
                request);
        }

        private CloudDeleteObjectContext ResolveDeleteContext(CloudObjectDeleteRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            ResolveConfiguration(out var config, out var credential);
            return new CloudDeleteObjectContext(
                config.ProviderId,
                config.Bucket,
                config.Region,
                config.Endpoint,
                credential,
                request);
        }

        private void ResolveConfiguration(
            out GameDeveloperKit.EditorConfiguration.CloudProjectConfig config,
            out CloudCredential credential)
        {
            if (m_ProjectConfigProvider == null || m_CredentialStore == null)
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    "CloudService project configuration is not available.");
            }

            config = m_ProjectConfigProvider();
            if (config == null)
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    "Cloud project configuration is missing.");
            }

            if (m_CredentialStore.TryGet(
                    config.ProviderId,
                    config.CredentialProfileName,
                    out credential) is false)
            {
                throw new CloudException(
                    CloudFailureKind.CredentialsMissing,
                    $"Credential profile '{config.CredentialProfileName}' is missing for provider '{config.ProviderId}'.",
                    config.ProviderId);
            }
        }

        private static void ValidateListContext(CloudListObjectsContext context)
        {
            if (context?.Request == null)
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    "Cloud LIST context and request are required.",
                    context?.ProviderId);
            }

            ValidateObjectPrefix(context.Request.Prefix, context.ProviderId);
            if (context.Request.ContinuationToken.Length > 4096 ||
                context.Request.ContinuationToken.Any(char.IsControl))
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    "Cloud continuation token is invalid.",
                    context.ProviderId);
            }

            ValidateManagementCredential(context.Credential, context.ProviderId);
        }

        private static void ValidateDeleteContext(CloudDeleteObjectContext context)
        {
            if (context?.Request == null)
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    "Cloud DELETE context and request are required.",
                    context?.ProviderId);
            }

            ValidateObjectKey(context.Request.ObjectKey, context.ProviderId);
            ValidateManagementCredential(context.Credential, context.ProviderId);
        }

        private static void ValidateObjectPrefix(string prefix, string providerId)
        {
            var normalized = prefix?.TrimEnd('/') ?? string.Empty;
            if (normalized.Length == 0)
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    "Cloud object prefix cannot be empty or target the bucket root.",
                    providerId);
            }

            ValidateObjectKey(normalized, providerId);
        }

        private static void ValidateManagementCredential(CloudCredential credential, string providerId)
        {
            if (credential == null ||
                string.IsNullOrWhiteSpace(credential.AccessKeyId) ||
                string.IsNullOrWhiteSpace(credential.SecretAccessKey))
            {
                throw new CloudException(
                    CloudFailureKind.CredentialsMissing,
                    $"Cloud credentials are missing for provider '{providerId}'.",
                    providerId);
            }
        }

        private static void ValidateListPage(
            CloudListObjectsContext context,
            CloudObjectPage page)
        {
            if (page == null || page.Objects.Count > context.Request.MaxKeys)
            {
                throw new CloudException(
                    CloudFailureKind.ProviderResponse,
                    "Cloud LIST returned an invalid page.",
                    context.ProviderId);
            }

            if (page.IsTruncated && string.IsNullOrWhiteSpace(page.NextContinuationToken))
            {
                throw new CloudException(
                    CloudFailureKind.ProviderResponse,
                    "Cloud LIST response is truncated but has no continuation token.",
                    context.ProviderId);
            }

            foreach (var item in page.Objects)
            {
                if (item == null ||
                    item.Size < 0 ||
                    item.ObjectKey.StartsWith(context.Request.Prefix, StringComparison.Ordinal) is false)
                {
                    throw new CloudException(
                        CloudFailureKind.ProviderResponse,
                        "Cloud LIST returned an object outside the requested prefix.",
                        context.ProviderId);
                }

                ValidateObjectKey(item.ObjectKey, context.ProviderId);
            }
        }

        private static CloudException UnsupportedCapability(string providerId, string capability)
        {
            return new CloudException(
                CloudFailureKind.InvalidConfiguration,
                $"Cloud provider '{providerId}' does not support {capability}.",
                providerId);
        }

        private static CloudException UnsupportedTransport(string providerId)
        {
            return new CloudException(
                CloudFailureKind.InvalidConfiguration,
                "Cloud HTTP transport does not support object management requests.",
                providerId);
        }

        private static CloudException ClassifyManagementResponse(
            string providerId,
            string operation,
            string target,
            string bucket,
            string region,
            string endpoint,
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
                $"Cloud {operation} failed for '{target}' with HTTP {response.StatusCode}." +
                CloudEndpointContext.Format(bucket, region, endpoint),
                providerId,
                response.StatusCode,
                FirstNonEmpty(
                    response.GetHeader("x-cos-request-id"),
                    response.GetHeader("x-oss-request-id"),
                    response.GetHeader("x-request-id")));
        }

        private readonly struct ManagementRequestOutcome
        {
            public ManagementRequestOutcome(CloudHttpResponse response, CloudException failure)
            {
                Response = response;
                Failure = failure;
            }

            public CloudHttpResponse Response { get; }
            public CloudException Failure { get; }
        }
    }

    internal static class CloudListResponseParser
    {
        public static CloudObjectPage Parse(
            CloudHttpResponse response,
            string requestId,
            string providerId)
        {
            try
            {
                using var source = new StringReader(response.Body);
                using var reader = XmlReader.Create(source, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                });
                var document = XDocument.Load(reader, LoadOptions.None);
                var root = document.Root ?? throw new XmlException("LIST response has no root element.");
                var objects = root.Elements()
                    .Where(element => element.Name.LocalName == "Contents")
                    .Select(ParseObject)
                    .ToArray();
                var isTruncatedText = ReadElement(root, "IsTruncated");
                var isTruncated = string.Equals(
                    isTruncatedText,
                    "true",
                    StringComparison.OrdinalIgnoreCase);
                return new CloudObjectPage(
                    objects,
                    isTruncated,
                    ReadElement(root, "NextContinuationToken"),
                    requestId);
            }
            catch (Exception exception) when (
                exception is XmlException ||
                exception is InvalidOperationException ||
                exception is FormatException ||
                exception is OverflowException)
            {
                throw new CloudException(
                    CloudFailureKind.ProviderResponse,
                    "Cloud LIST returned malformed XML.",
                    providerId,
                    response.StatusCode,
                    requestId,
                    exception);
            }
        }

        private static CloudObjectInfo ParseObject(XElement element)
        {
            var key = ReadElement(element, "Key");
            var sizeText = ReadElement(element, "Size");
            if (string.IsNullOrWhiteSpace(key) ||
                long.TryParse(sizeText, NumberStyles.None, CultureInfo.InvariantCulture, out var size) is false)
            {
                throw new FormatException("LIST object entry is invalid.");
            }

            return new CloudObjectInfo(key, ReadElement(element, "ETag"), size);
        }

        private static string ReadElement(XElement parent, string localName)
        {
            return parent.Elements()
                .FirstOrDefault(element => element.Name.LocalName == localName)
                ?.Value
                ?.Trim() ?? string.Empty;
        }
    }
}
