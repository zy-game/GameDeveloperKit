using System.Collections.Generic;
using System;

namespace GameDeveloperKit.ResourcePublisher
{
    public sealed class UnavailableObjectStorageProvider : IObjectStorageProvider
    {
        public UnavailableObjectStorageProvider(string platformId, string displayName)
        {
            PlatformId = string.IsNullOrWhiteSpace(platformId) ? "unavailable" : platformId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? PlatformId : displayName;
        }

        public string PlatformId { get; }

        public string DisplayName { get; }

        public IReadOnlyList<StorageRegionInfo> ListRegions(StorageCredential credential)
        {
            return new List<StorageRegionInfo>();
        }

        public IReadOnlyList<StorageBucketInfo> ListBuckets(StorageCredential credential, string regionId)
        {
            return new List<StorageBucketInfo>();
        }

        public IReadOnlyList<StorageObjectInfo> ListObjects(StorageCredential credential, PublisherChannel channel, string prefix)
        {
            return new List<StorageObjectInfo>();
        }

        public ResourcePublishOperationResult UploadObject(StorageCredential credential, PublisherChannel channel, StorageUploadItem item)
        {
            return NotAvailable();
        }

        public ResourcePublishOperationResult DeleteObjects(StorageCredential credential, PublisherChannel channel, IReadOnlyList<string> keys)
        {
            return NotAvailable();
        }

        public string DownloadText(StorageCredential credential, PublisherChannel channel, string key)
        {
            return null;
        }

        public ResourcePublishOperationResult UploadText(StorageCredential credential, PublisherChannel channel, string key, string content)
        {
            return NotAvailable();
        }

        private ResourcePublishOperationResult NotAvailable()
        {
            return ResourcePublishOperationResult.Failure($"{DisplayName} provider is not available.");
        }
    }
}
