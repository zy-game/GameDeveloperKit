using System.Collections.Generic;

namespace GameDeveloperKit.ResourcePublisher
{
    public interface IObjectStorageProvider
    {
        string PlatformId { get; }

        string DisplayName { get; }

        IReadOnlyList<StorageRegionInfo> ListRegions(StorageCredential credential);

        IReadOnlyList<StorageBucketInfo> ListBuckets(StorageCredential credential, string regionId);

        IReadOnlyList<StorageObjectInfo> ListObjects(StorageCredential credential, PublisherChannel channel, string prefix);

        ResourcePublishOperationResult UploadObject(StorageCredential credential, PublisherChannel channel, StorageUploadItem item);

        ResourcePublishOperationResult DeleteObjects(StorageCredential credential, PublisherChannel channel, IReadOnlyList<string> keys);

        string DownloadText(StorageCredential credential, PublisherChannel channel, string key);

        ResourcePublishOperationResult UploadText(StorageCredential credential, PublisherChannel channel, string key, string content);
    }
}
