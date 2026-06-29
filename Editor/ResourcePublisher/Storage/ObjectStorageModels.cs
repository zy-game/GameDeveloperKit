using System;
using System.Collections.Generic;

namespace GameDeveloperKit.ResourcePublisher
{
    /// <summary>
    /// 定义 Storage Region Info 类型。
    /// </summary>
    public sealed class StorageRegionInfo
    {
        public string RegionId { get; set; }

        public string DisplayName { get; set; }

        public Dictionary<string, string> ProviderMetadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// 定义 Storage Bucket Info 类型。
    /// </summary>
    public sealed class StorageBucketInfo
    {
        public string BucketName { get; set; }

        public string RegionId { get; set; }

        public DateTime? CreatedAt { get; set; }

        public string VersioningStatus { get; set; }

        public string Endpoint { get; set; }

        public Dictionary<string, string> ProviderMetadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// 定义 Resource Publish Operation Result 类型。
    /// </summary>
    public sealed class ResourcePublishOperationResult
    {
        public bool Succeeded { get; set; }

        public string Message { get; set; }

        public string ProviderRequestId { get; set; }

        public List<ResourcePublishOperationItem> Items { get; set; } = new List<ResourcePublishOperationItem>();

        /// <summary>
        /// 执行 Success。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <param name="providerRequestId">provider Request Id 参数。</param>
        /// <returns>执行结果。</returns>
        public static ResourcePublishOperationResult Success(string message = null, string providerRequestId = null)
        {
            return new ResourcePublishOperationResult
            {
                Succeeded = true,
                Message = message,
                ProviderRequestId = providerRequestId
            };
        }

        /// <summary>
        /// 执行 Failure。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <param name="providerRequestId">provider Request Id 参数。</param>
        /// <returns>执行结果。</returns>
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

    /// <summary>
    /// 定义 Resource Publish Operation Item 类型。
    /// </summary>
    public sealed class ResourcePublishOperationItem
    {
        public string Key { get; set; }

        public bool Succeeded { get; set; }

        public string Message { get; set; }
    }

    /// <summary>
    /// 定义 Storage Object Info 类型。
    /// </summary>
    public sealed class StorageObjectInfo
    {
        public string Key { get; set; }

        public bool Exists { get; set; }

        public long Size { get; set; }

        public string Hash { get; set; }

        public string VersionId { get; set; }
    }

    /// <summary>
    /// 定义 Storage Upload Item 类型。
    /// </summary>
    public sealed class StorageUploadItem
    {
        public string LocalPath { get; set; }

        public string RemoteKey { get; set; }

        public string Hash { get; set; }

        public long Size { get; set; }
    }

    /// <summary>
    /// 定义 Resource Upload Plan 类型。
    /// </summary>
    public sealed class ResourceUploadPlan
    {
        public string Version { get; set; }

        public string BuildTarget { get; set; }

        public string Channel { get; set; }

        public string IndexKey { get; set; }

        public string ManifestKey { get; set; }

        public List<StorageUploadItem> Items { get; } = new List<StorageUploadItem>();
    }

    /// <summary>
    /// 定义 Resource Publish Pointer 类型。
    /// </summary>
    public sealed class ResourcePublishPointer
    {
        public string version { get; set; }
    }
}
