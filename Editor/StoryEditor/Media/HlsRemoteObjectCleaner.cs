using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorCloud;
using GameDeveloperKit.EditorConfiguration;

namespace GameDeveloperKit.StoryEditor.Media
{
    internal sealed class HlsObjectCleanupResult
    {
        public HlsObjectCleanupResult(
            string mediaId,
            string objectPrefix,
            int succeededCount,
            IReadOnlyDictionary<string, CloudException> failed)
        {
            MediaId = mediaId ?? string.Empty;
            ObjectPrefix = objectPrefix ?? string.Empty;
            SucceededCount = succeededCount;
            Failed = failed ?? new Dictionary<string, CloudException>(StringComparer.Ordinal);
        }

        public string MediaId { get; }
        public string ObjectPrefix { get; }
        public int SucceededCount { get; }
        public IReadOnlyDictionary<string, CloudException> Failed { get; }
        public bool IsSuccess => Failed.Count == 0;
    }

    internal sealed class HlsRemoteObjectCleaner
    {
        private readonly CloudService m_CloudService;
        private readonly Func<CloudProjectConfig> m_ConfigProvider;

        public HlsRemoteObjectCleaner()
            : this(
                CloudService.Shared,
                () => EditorGlobalConfig.LoadOrCreate().Cloud)
        {
        }

        internal HlsRemoteObjectCleaner(
            CloudService cloudService,
            Func<CloudProjectConfig> configProvider)
        {
            m_CloudService = cloudService ?? throw new ArgumentNullException(nameof(cloudService));
            m_ConfigProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        }

        public async UniTask<HlsObjectCleanupResult> CleanupAsync(
            string mediaId,
            CancellationToken cancellationToken)
        {
            ValidateMediaId(mediaId);
            var root = m_ConfigProvider()?.RootPrefix?.Trim().Trim('/') ?? string.Empty;
            var prefix = root.Length == 0
                ? mediaId + "/"
                : root + "/" + mediaId + "/";
            var keys = await ListAllAsync(prefix, cancellationToken);
            var failed = new Dictionary<string, CloudException>(StringComparer.Ordinal);
            var succeeded = 0;
            foreach (var key in keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await m_CloudService.DeleteObjectAsync(
                        new CloudObjectDeleteRequest(key),
                        cancellationToken);
                    succeeded++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (CloudException exception)
                {
                    failed[key] = exception;
                }
            }

            return new HlsObjectCleanupResult(mediaId, prefix, succeeded, failed);
        }

        private async UniTask<IReadOnlyList<string>> ListAllAsync(
            string prefix,
            CancellationToken cancellationToken)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var seenTokens = new HashSet<string>(StringComparer.Ordinal);
            var token = string.Empty;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var page = await m_CloudService.ListObjectsAsync(
                        new CloudObjectListRequest(prefix, token),
                        cancellationToken);
                    foreach (var item in page.Objects)
                    {
                        keys.Add(item.ObjectKey);
                    }

                    if (page.IsTruncated is false)
                    {
                        return keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
                    }

                    token = page.NextContinuationToken;
                }
                catch (CloudException exception) when (exception.Kind == CloudFailureKind.NotFound)
                {
                    // A missing prefix means all remote media objects are already gone.
                    return keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
                }

                if (seenTokens.Add(token) is false)
                {
                    throw new CloudException(
                        CloudFailureKind.ProviderResponse,
                        "Cloud LIST returned a repeated continuation token.");
                }
            }
        }

        private static void ValidateMediaId(string mediaId)
        {
            if (string.IsNullOrWhiteSpace(mediaId) ||
                mediaId.Any(character =>
                    character != '-' &&
                    (character < 'a' || character > 'z') &&
                    (character < '0' || character > '9')))
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    "HLS media ID is invalid for remote cleanup.");
            }
        }
    }
}
