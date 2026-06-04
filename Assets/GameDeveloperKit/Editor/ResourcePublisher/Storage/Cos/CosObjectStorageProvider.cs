using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using COSXML;
using COSXML.Auth;
using COSXML.CosException;
using COSXML.Model;
using COSXML.Model.Bucket;
using COSXML.Model.Object;
using COSXML.Model.Service;
using COSXML.Model.Tag;
using UnityEditor;

namespace GameDeveloperKit.ResourcePublisher
{
    public sealed class CosObjectStorageProvider : IObjectStorageProvider
    {
        private const string Platform = "cos";
        private const long CredentialDurationSeconds = 600;

        private static readonly IReadOnlyList<StorageRegionInfo> s_Regions = new List<StorageRegionInfo>
        {
            Region("ap-beijing", "北京"),
            Region("ap-nanjing", "南京"),
            Region("ap-shanghai", "上海"),
            Region("ap-guangzhou", "广州"),
            Region("ap-chengdu", "成都"),
            Region("ap-chongqing", "重庆"),
            Region("ap-hongkong", "中国香港"),
            Region("ap-singapore", "新加坡"),
            Region("ap-seoul", "首尔"),
            Region("ap-tokyo", "东京"),
            Region("eu-frankfurt", "法兰克福"),
            Region("na-siliconvalley", "硅谷"),
            Region("na-ashburn", "弗吉尼亚"),
            Region("na-toronto", "多伦多"),
            Region("sa-saopaulo", "圣保罗")
        };

        public string PlatformId => Platform;

        public string DisplayName => "COS";

        public IReadOnlyList<StorageRegionInfo> ListRegions(StorageCredential credential)
        {
            return s_Regions;
        }

        public IReadOnlyList<StorageBucketInfo> ListBuckets(StorageCredential credential, string regionId)
        {
            var cosXml = CreateClient(credential, NormalizeRegion(regionId));
            var request = new GetServiceRequest();
            if (string.IsNullOrWhiteSpace(regionId) is false)
            {
                request.host = $"cos.{regionId}.myqcloud.com";
            }

            var result = cosXml.GetService(request);
            var buckets = new List<StorageBucketInfo>();
            if (result?.listAllMyBuckets?.buckets == null)
            {
                return buckets;
            }

            foreach (var bucket in result.listAllMyBuckets.buckets)
            {
                if (bucket == null)
                {
                    continue;
                }

                buckets.Add(new StorageBucketInfo
                {
                    BucketName = bucket.name,
                    RegionId = bucket.location,
                    CreatedAt = ParseCosDate(bucket.createDate),
                    Endpoint = string.IsNullOrWhiteSpace(bucket.location) || string.IsNullOrWhiteSpace(bucket.name)
                        ? string.Empty
                        : $"{bucket.name}.cos.{bucket.location}.myqcloud.com"
                });
            }

            return buckets;
        }

        public IReadOnlyList<StorageObjectInfo> ListObjects(StorageCredential credential, PublisherChannel channel, string prefix)
        {
            var cosXml = CreateClient(credential, NormalizeRegion(channel?.RegionId));
            var objects = new List<StorageObjectInfo>();
            string nextMarker = null;

            do
            {
                var request = new GetBucketRequest(EnsureBucketName(channel?.BucketName));
                if (string.IsNullOrWhiteSpace(prefix) is false)
                {
                    request.SetPrefix(prefix);
                }

                if (string.IsNullOrWhiteSpace(nextMarker) is false)
                {
                    request.SetMarker(nextMarker);
                }

                var result = cosXml.GetBucket(request);
                var listBucket = result?.listBucket;
                if (listBucket?.contentsList != null)
                {
                    foreach (var content in listBucket.contentsList)
                    {
                        if (content == null || string.IsNullOrWhiteSpace(content.key))
                        {
                            continue;
                        }

                        objects.Add(new StorageObjectInfo
                        {
                            Key = content.key,
                            Exists = true,
                            Size = content.size,
                            Hash = TrimQuotes(content.eTag)
                        });
                    }
                }

                nextMarker = listBucket != null && listBucket.isTruncated ? listBucket.nextMarker : null;
            } while (string.IsNullOrWhiteSpace(nextMarker) is false);

            return objects;
        }

        public ResourcePublishOperationResult UploadObject(StorageCredential credential, PublisherChannel channel, StorageUploadItem item)
        {
            if (item == null)
            {
                return ResourcePublishOperationResult.Failure("上传对象不能为空");
            }

            try
            {
                var cosXml = CreateClient(credential, NormalizeRegion(channel?.RegionId));
                var request = new PutObjectRequest(EnsureBucketName(channel?.BucketName), item.RemoteKey, item.LocalPath);
                var result = cosXml.PutObject(request);
                return ToSuccess($"上传成功：{item.RemoteKey}", result);
            }
            catch (Exception exception)
            {
                return ToFailure($"上传失败：{item.RemoteKey}", exception);
            }
        }

        public ResourcePublishOperationResult DeleteObjects(StorageCredential credential, PublisherChannel channel, IReadOnlyList<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return ResourcePublishOperationResult.Success("没有需要删除的对象");
            }

            try
            {
                var cosXml = CreateClient(credential, NormalizeRegion(channel?.RegionId));
                var result = ResourcePublishOperationResult.Success($"删除成功：{keys.Count} objects");
                foreach (var batch in Batch(keys.Where(x => string.IsNullOrWhiteSpace(x) is false).Distinct(StringComparer.Ordinal), 1000))
                {
                    var request = new DeleteMultiObjectRequest(EnsureBucketName(channel?.BucketName));
                    request.SetDeleteQuiet(false);
                    request.SetObjectKeys(batch);
                    var deleteResult = cosXml.DeleteMultiObjects(request);
                    result.ProviderRequestId = RequestId(deleteResult);
                    foreach (var key in batch)
                    {
                        result.Items.Add(new ResourcePublishOperationItem
                        {
                            Key = key,
                            Succeeded = true,
                            Message = "Deleted"
                        });
                    }
                }

                return result;
            }
            catch (Exception exception)
            {
                return ToFailure("删除对象失败", exception);
            }
        }

        public string DownloadText(StorageCredential credential, PublisherChannel channel, string key)
        {
            try
            {
                var cosXml = CreateClient(credential, NormalizeRegion(channel?.RegionId));
                var request = new GetObjectBytesRequest(EnsureBucketName(channel?.BucketName), key);
                var result = cosXml.GetObject(request);
                return result?.content == null ? null : Encoding.UTF8.GetString(result.content);
            }
            catch (CosServerException exception) when (exception.statusCode == 404)
            {
                return null;
            }
        }

        public ResourcePublishOperationResult UploadText(StorageCredential credential, PublisherChannel channel, string key, string content)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
                var cosXml = CreateClient(credential, NormalizeRegion(channel?.RegionId));
                var request = new PutObjectRequest(EnsureBucketName(channel?.BucketName), key, bytes);
                var result = cosXml.PutObject(request);
                return ToSuccess($"上传成功：{key}", result);
            }
            catch (Exception exception)
            {
                return ToFailure($"上传失败：{key}", exception);
            }
        }

        private static StorageRegionInfo Region(string regionId, string displayName)
        {
            return new StorageRegionInfo
            {
                RegionId = regionId,
                DisplayName = $"{displayName} ({regionId})"
            };
        }

        private static CosXml CreateClient(StorageCredential credential, string regionId)
        {
            if (credential == null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            var cosCredential = ResolveCredential(credential);
            var config = new CosXmlConfig.Builder()
                .SetRegion(regionId)
                .SetDebugLog(false)
                .Build();
            var provider = new DefaultQCloudCredentialProvider(cosCredential.SecretId, cosCredential.SecretKey, CredentialDurationSeconds);
            return new CosXmlServer(config, provider);
        }

        private static CosCredential ResolveCredential(StorageCredential credential)
        {
            if (string.IsNullOrWhiteSpace(credential.SecretId) || string.IsNullOrWhiteSpace(credential.SecretKey))
            {
                throw new InvalidOperationException("COS SecretId 和 SecretKey 不能为空。");
            }

            return new CosCredential(credential.SecretId, credential.SecretKey);
        }

        private static string NormalizeRegion(string regionId)
        {
            return string.IsNullOrWhiteSpace(regionId) ? "ap-guangzhou" : regionId;
        }

        private static string EnsureBucketName(string bucketName)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
            {
                throw new ArgumentException("Bucket 不能为空。", nameof(bucketName));
            }

            return bucketName;
        }

        private static DateTime? ParseCosDate(string value)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static ResourcePublishOperationResult ToSuccess(string message, CosResult result)
        {
            return ResourcePublishOperationResult.Success($"{message} · HTTP {result?.httpCode}", RequestId(result));
        }

        private static ResourcePublishOperationResult ToFailure(string prefix, Exception exception)
        {
            switch (exception)
            {
                case CosServerException serverException:
                    return ResourcePublishOperationResult.Failure(
                        $"{prefix} · COS {serverException.statusCode} {serverException.errorCode}: {serverException.errorMessage}",
                        serverException.requestId);
                case CosClientException clientException:
                    return ResourcePublishOperationResult.Failure($"{prefix} · COS Client: {clientException.errorCode}");
                default:
                    return ResourcePublishOperationResult.Failure($"{prefix} · {exception.Message}");
            }
        }

        private static string RequestId(CosResult result)
        {
            if (result?.responseHeaders == null)
            {
                return null;
            }

            return result.responseHeaders.TryGetValue("x-cos-request-id", out var requestIds) && requestIds.Count > 0
                ? requestIds[0]
                : null;
        }

        private static string TrimQuotes(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? value : value.Trim('"');
        }

        private static IEnumerable<List<string>> Batch(IEnumerable<string> source, int size)
        {
            var batch = new List<string>(size);
            foreach (var item in source)
            {
                batch.Add(item);
                if (batch.Count < size)
                {
                    continue;
                }

                yield return batch;
                batch = new List<string>(size);
            }

            if (batch.Count > 0)
            {
                yield return batch;
            }
        }

        private readonly struct CosCredential
        {
            public CosCredential(string secretId, string secretKey)
            {
                SecretId = secretId;
                SecretKey = secretKey;
            }

            public string SecretId { get; }

            public string SecretKey { get; }
        }
    }

    [InitializeOnLoad]
    internal static class CosObjectStorageProviderRegistration
    {
        static CosObjectStorageProviderRegistration()
        {
            ObjectStorageProviderRegistry.Register(new CosObjectStorageProvider());
        }
    }
}
