using System.Collections.Generic;
using System;

namespace GameDeveloperKit.ResourcePublisher
{
    /// <summary>
    /// 定义 Unavailable Object Storage Provider 类型。
    /// </summary>
    public sealed class UnavailableObjectStorageProvider : IObjectStorageProvider
    {
        /// <summary>
        /// 初始化 Unavailable Object Storage Provider。
        /// </summary>
        /// <param name="platformId">platform Id 参数。</param>
        /// <param name="displayName">display Name 参数。</param>
        public UnavailableObjectStorageProvider(string platformId, string displayName)
        {
            PlatformId = string.IsNullOrWhiteSpace(platformId) ? "unavailable" : platformId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? PlatformId : displayName;
        }

        public string PlatformId { get; }

        public string DisplayName { get; }

        /// <summary>
        /// 列出可用地域。
        /// </summary>
        /// <param name="credential">credential 参数。</param>
        /// <returns>执行结果。</returns>
        public IReadOnlyList<StorageRegionInfo> ListRegions(StorageCredential credential)
        {
            return new List<StorageRegionInfo>();
        }

        /// <summary>
        /// 列出存储桶。
        /// </summary>
        /// <param name="credential">credential 参数。</param>
        /// <param name="regionId">region Id 参数。</param>
        /// <returns>执行结果。</returns>
        public IReadOnlyList<StorageBucketInfo> ListBuckets(StorageCredential credential, string regionId)
        {
            return new List<StorageBucketInfo>();
        }

        /// <summary>
        /// 列出对象。
        /// </summary>
        /// <param name="credential">credential 参数。</param>
        /// <param name="channel">channel 参数。</param>
        /// <param name="prefix">prefix 参数。</param>
        /// <returns>执行结果。</returns>
        public IReadOnlyList<StorageObjectInfo> ListObjects(StorageCredential credential, PublisherChannel channel, string prefix)
        {
            return new List<StorageObjectInfo>();
        }

        /// <summary>
        /// 执行 Upload Object。
        /// </summary>
        /// <param name="credential">credential 参数。</param>
        /// <param name="channel">channel 参数。</param>
        /// <param name="item">item 参数。</param>
        /// <returns>执行结果。</returns>
        public ResourcePublishOperationResult UploadObject(StorageCredential credential, PublisherChannel channel, StorageUploadItem item)
        {
            return NotAvailable();
        }

        /// <summary>
        /// 执行 Delete Objects。
        /// </summary>
        /// <param name="credential">credential 参数。</param>
        /// <param name="channel">channel 参数。</param>
        /// <param name="keys">keys 参数。</param>
        /// <returns>执行结果。</returns>
        public ResourcePublishOperationResult DeleteObjects(StorageCredential credential, PublisherChannel channel, IReadOnlyList<string> keys)
        {
            return NotAvailable();
        }

        /// <summary>
        /// 执行 Download Text。
        /// </summary>
        /// <param name="credential">credential 参数。</param>
        /// <param name="channel">channel 参数。</param>
        /// <param name="key">key 参数。</param>
        /// <returns>执行结果。</returns>
        public string DownloadText(StorageCredential credential, PublisherChannel channel, string key)
        {
            return null;
        }

        /// <summary>
        /// 执行 Upload Text。
        /// </summary>
        /// <param name="credential">credential 参数。</param>
        /// <param name="channel">channel 参数。</param>
        /// <param name="key">key 参数。</param>
        /// <param name="content">content 参数。</param>
        /// <returns>执行结果。</returns>
        public ResourcePublishOperationResult UploadText(StorageCredential credential, PublisherChannel channel, string key, string content)
        {
            return NotAvailable();
        }

        /// <summary>
        /// 执行 Not Available。
        /// </summary>
        /// <returns>执行结果。</returns>
        private ResourcePublishOperationResult NotAvailable()
        {
            return ResourcePublishOperationResult.Failure($"{DisplayName} provider is not available.");
        }
    }
}
