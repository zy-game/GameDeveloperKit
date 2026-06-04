using System;
using System.Collections.Generic;

namespace GameDeveloperKit.ResourcePublisher
{
    public sealed class StorageRegionInfo
    {
        public string RegionId { get; set; }

        public string DisplayName { get; set; }

        public Dictionary<string, string> ProviderMetadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class StorageBucketInfo
    {
        public string BucketName { get; set; }

        public string RegionId { get; set; }

        public DateTime? CreatedAt { get; set; }

        public string VersioningStatus { get; set; }

        public string Endpoint { get; set; }

        public Dictionary<string, string> ProviderMetadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class ResourcePublishOperationResult
    {
        public bool Succeeded { get; set; }

        public string Message { get; set; }

        public string ProviderRequestId { get; set; }

        public List<ResourcePublishOperationItem> Items { get; set; } = new List<ResourcePublishOperationItem>();

        public static ResourcePublishOperationResult Success(string message = null, string providerRequestId = null)
        {
            return new ResourcePublishOperationResult
            {
                Succeeded = true,
                Message = message,
                ProviderRequestId = providerRequestId
            };
        }

        public static ResourcePublishOperationResult Failure(string message, string providerRequestId = null)
        {
            return new ResourcePublishOperationResult
            {
                Succeeded = false,
                Message = message,
                ProviderRequestId = providerRequestId
            };
        }
    }

    public sealed class ResourcePublishOperationItem
    {
        public string Key { get; set; }

        public bool Succeeded { get; set; }

        public string Message { get; set; }
    }

    public sealed class StorageObjectInfo
    {
        public string Key { get; set; }

        public bool Exists { get; set; }

        public long Size { get; set; }

        public string Hash { get; set; }

        public string VersionId { get; set; }
    }

    public sealed class StorageUploadItem
    {
        public string LocalPath { get; set; }

        public string RemoteKey { get; set; }

        public string Hash { get; set; }

        public long Size { get; set; }
    }

    public sealed class ResourceUploadPlan
    {
        public string Version { get; set; }

        public string BuildTarget { get; set; }

        public string Channel { get; set; }

        public string IndexKey { get; set; }

        public string ManifestKey { get; set; }

        public List<StorageUploadItem> Items { get; } = new List<StorageUploadItem>();
    }

    public sealed class ResourcePublishPointer
    {
        public string version { get; set; }
    }
}
