using System;
using System.Collections.Generic;

namespace GameDeveloperKit.EditorCloud
{
    public interface ICloudProvider
    {
        string ProviderId { get; }

        CloudProviderCapabilities Capabilities { get; }

        void Validate(CloudPutObjectContext context);

        CloudHttpRequest CreatePutObjectRequest(CloudPutObjectContext context);

        CloudUploadResult ParsePutObjectResponse(
            CloudPutObjectContext context,
            CloudHttpResponse response);
    }

    public interface ICloudReadProvider
    {
        void Validate(CloudGetObjectContext context);

        CloudHttpRequest CreateGetObjectRequest(CloudGetObjectContext context);
    }

    public interface ICloudListProvider
    {
        void Validate(CloudListObjectsContext context);

        CloudHttpRequest CreateListObjectsRequest(CloudListObjectsContext context);

        CloudObjectPage ParseListObjectsResponse(
            CloudListObjectsContext context,
            CloudHttpResponse response);
    }

    public interface ICloudDeleteProvider
    {
        void Validate(CloudDeleteObjectContext context);

        CloudHttpRequest CreateDeleteObjectRequest(CloudDeleteObjectContext context);

        CloudDeleteResult ParseDeleteObjectResponse(
            CloudDeleteObjectContext context,
            CloudHttpResponse response,
            bool existed);
    }

    public sealed class CloudProviderRegistry
    {
        private readonly Dictionary<string, ICloudProvider> m_Providers =
            new Dictionary<string, ICloudProvider>(StringComparer.Ordinal);

        public static CloudProviderRegistry CreateBuiltIn()
        {
            return new CloudProviderRegistry()
                .Register(new TencentCosProvider())
                .Register(new AliyunOssProvider());
        }

        public CloudProviderRegistry Register(ICloudProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            var providerId = provider.ProviderId?.Trim() ?? string.Empty;
            if (providerId.Length == 0)
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    "Cloud provider ID cannot be empty.");
            }

            if (m_Providers.ContainsKey(providerId))
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    $"Cloud provider '{providerId}' is already registered.",
                    providerId);
            }

            m_Providers.Add(providerId, provider);
            return this;
        }

        public ICloudProvider Resolve(string providerId)
        {
            var normalized = providerId?.Trim() ?? string.Empty;
            if (m_Providers.TryGetValue(normalized, out var provider))
            {
                return provider;
            }

            throw new CloudException(
                CloudFailureKind.InvalidConfiguration,
                normalized.Length == 0
                    ? "Cloud provider is not configured."
                    : $"Cloud provider '{normalized}' is not registered.",
                normalized);
        }
    }
}
