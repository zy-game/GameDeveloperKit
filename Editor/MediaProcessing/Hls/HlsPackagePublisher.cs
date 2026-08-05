using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorCloud;
using GameDeveloperKit.EditorConfiguration;
using IOFile = System.IO.File;

namespace GameDeveloperKit.MediaEditor
{
    public sealed class HlsPackagePublishRequest
    {
        public HlsPackagePublishRequest(
            HlsTranscodeResult transcodeResult,
            string displayName,
            string mediaId = null)
        {
            TranscodeResult = transcodeResult ?? throw new ArgumentNullException(nameof(transcodeResult));
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? throw new ArgumentException("HLS display name cannot be empty.", nameof(displayName))
                : displayName.Trim();
            MediaId = mediaId?.Trim() ?? string.Empty;
        }

        public HlsTranscodeResult TranscodeResult { get; }

        public string DisplayName { get; }

        public string MediaId { get; }
    }

    public sealed class HlsPackagePublishResult
    {
        internal HlsPackagePublishResult(
            string mediaId,
            string masterObjectKey,
            IReadOnlyList<CloudUploadResult> uploads)
        {
            MediaId = mediaId;
            MasterObjectKey = masterObjectKey;
            Uploads = uploads;
        }

        public string MediaId { get; }

        public string MasterObjectKey { get; }

        public IReadOnlyList<CloudUploadResult> Uploads { get; }
    }

    public sealed class HlsPackagePublisher
    {
        private readonly CloudService m_CloudService;
        private readonly Func<CloudProjectConfig> m_ProjectConfigProvider;
        private readonly Func<string> m_MediaIdFactory;

        public HlsPackagePublisher()
            : this(
                CloudService.Shared,
                () => EditorGlobalConfig.LoadOrCreate().Cloud,
                CreateMediaId)
        {
        }

        internal HlsPackagePublisher(
            CloudService cloudService,
            Func<CloudProjectConfig> projectConfigProvider,
            Func<string> mediaIdFactory)
        {
            m_CloudService = cloudService ?? throw new ArgumentNullException(nameof(cloudService));
            m_ProjectConfigProvider = projectConfigProvider ??
                                      throw new ArgumentNullException(nameof(projectConfigProvider));
            m_MediaIdFactory = mediaIdFactory ?? throw new ArgumentNullException(nameof(mediaIdFactory));
        }

        public async UniTask<HlsPackagePublishResult> PublishAsync(
            HlsPackagePublishRequest request,
            IProgress<CloudUploadProgress> progress,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var package = CreatePackagePlan(request);
            var config = m_ProjectConfigProvider();
            if (config == null)
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    "Cloud project configuration is missing.");
            }

            var mediaId = string.IsNullOrWhiteSpace(request.MediaId)
                ? m_MediaIdFactory()
                : request.MediaId;
            ValidateMediaId(mediaId);
            var objectPrefix = CombineObjectKey(config.RootPrefix, mediaId);
            var contentRequests = package.ContentFiles
                .Select(file => new CloudObjectUploadRequest(
                    file.FullPath,
                    CombineObjectKey(objectPrefix, file.RelativePath),
                    ContentTypeFor(file.RelativePath)))
                .ToArray();
            var uploads = new List<CloudUploadResult>(contentRequests.Length + 1);

            if (contentRequests.Length > 0)
            {
                var contentResult = await m_CloudService.UploadBatchAsync(
                    new CloudBatchUploadRequest(contentRequests),
                    progress,
                    cancellationToken);
                uploads.AddRange(contentResult.Succeeded);
                if (contentResult.IsSuccess is false)
                {
                    var failure = FirstFailure(contentRequests, contentResult.Failed);
                    throw PublishFailure(
                        "content",
                        uploads.Count,
                        failure);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            var masterObjectKey = CombineObjectKey(objectPrefix, "master.m3u8");
            try
            {
                uploads.Add(await m_CloudService.UploadObjectAsync(
                    new CloudObjectUploadRequest(
                        package.MasterPath,
                        masterObjectKey,
                        "application/vnd.apple.mpegurl"),
                    progress,
                    cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (CloudException exception)
            {
                throw PublishFailure("master", uploads.Count, exception);
            }

            return new HlsPackagePublishResult(
                mediaId,
                masterObjectKey,
                new ReadOnlyCollection<CloudUploadResult>(uploads));
        }

        private static HlsPackagePlan CreatePackagePlan(HlsPackagePublishRequest request)
        {
            var root = Path.GetFullPath(request.TranscodeResult.PackageDirectory ?? string.Empty);
            if (Directory.Exists(root) is false)
            {
                throw new CloudException(
                    CloudFailureKind.LocalFile,
                    $"HLS package directory does not exist: {root}");
            }

            var masterPath = Path.GetFullPath(request.TranscodeResult.MasterPlaylistPath ?? string.Empty);
            var expectedMaster = Path.GetFullPath(Path.Combine(root, "master.m3u8"));
            if (string.Equals(masterPath, expectedMaster, StringComparison.OrdinalIgnoreCase) is false ||
                IOFile.Exists(masterPath) is false)
            {
                throw new CloudException(
                    CloudFailureKind.LocalFile,
                    "HLS package must contain master.m3u8 at its root.");
            }

            var renditionLocations = new List<HlsRenditionLocation>();
            foreach (var rendition in request.TranscodeResult.Renditions ?? Array.Empty<HlsRenditionInfo>())
            {
                if (rendition == null)
                {
                    throw new CloudException(
                        CloudFailureKind.LocalFile,
                        "HLS package contains an invalid rendition.");
                }

                var playlistPath = Path.GetFullPath(rendition.PlaylistPath ?? string.Empty);
                var relativePath = GetRelativePackagePath(root, playlistPath);
                if (IOFile.Exists(playlistPath) is false)
                {
                    throw new CloudException(
                        CloudFailureKind.LocalFile,
                        $"HLS rendition playlist does not exist: {playlistPath}");
                }

                renditionLocations.Add(new HlsRenditionLocation(rendition, relativePath));
            }

            if (renditionLocations.Count == 0)
            {
                throw new CloudException(
                    CloudFailureKind.LocalFile,
                    "HLS package does not contain renditions.");
            }

            var previewPath = Path.GetFullPath(request.TranscodeResult.PreviewImagePath ?? string.Empty);
            var expectedPreview = Path.GetFullPath(Path.Combine(root, HlsPreviewImage.FileName));
            if (string.Equals(previewPath, expectedPreview, StringComparison.OrdinalIgnoreCase) is false)
            {
                throw new CloudException(
                    CloudFailureKind.LocalFile,
                    "HLS package preview path is invalid.");
            }

            try
            {
                HlsPreviewImage.Validate(previewPath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                throw new CloudException(
                    CloudFailureKind.LocalFile,
                    "HLS package preview.jpg is invalid.",
                    innerException: exception);
            }

            var contentFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Select(Path.GetFullPath)
                .Where(path => string.Equals(path, masterPath, StringComparison.OrdinalIgnoreCase) is false)
                .Select(path => new HlsPackageFile(path, GetRelativePackagePath(root, path)))
                .Where(file => string.Equals(file.RelativePath, "metadata.json", StringComparison.OrdinalIgnoreCase) is false)
                .Where(file => string.Equals(file.RelativePath, "manifest.json", StringComparison.OrdinalIgnoreCase) is false)
                .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                .ToArray();
            return new HlsPackagePlan(masterPath, contentFiles, renditionLocations);
        }

        private static CloudException FirstFailure(
            IReadOnlyList<CloudObjectUploadRequest> requests,
            IReadOnlyDictionary<string, CloudException> failures)
        {
            for (var i = 0; i < requests.Count; i++)
            {
                if (failures.TryGetValue(requests[i].ObjectKey, out var failure))
                {
                    return failure;
                }
            }

            return failures.Values.First();
        }

        private static CloudException PublishFailure(
            string stage,
            int uploadedObjectCount,
            CloudException failure)
        {
            return new CloudException(
                failure.Kind,
                $"HLS {stage} publish failed; {uploadedObjectCount} remote objects may remain uncommitted. " +
                failure.Message,
                failure.ProviderId,
                failure.StatusCode,
                failure.RequestId,
                failure);
        }

        private static string GetRelativePackagePath(string root, string fullPath)
        {
            var rootPrefix = root.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) is false)
            {
                throw new CloudException(
                    CloudFailureKind.LocalFile,
                    $"HLS package file escapes the package directory: {fullPath}");
            }

            return fullPath.Substring(rootPrefix.Length).Replace('\\', '/');
        }

        private static string CombineObjectKey(string left, string right)
        {
            var normalizedLeft = left?.Trim().Trim('/') ?? string.Empty;
            var normalizedRight = right?.Trim().Trim('/') ?? string.Empty;
            return normalizedLeft.Length == 0
                ? normalizedRight
                : normalizedLeft + "/" + normalizedRight;
        }

        private static string ContentTypeFor(string relativePath)
        {
            switch (Path.GetExtension(relativePath).ToLowerInvariant())
            {
                case ".m3u8": return "application/vnd.apple.mpegurl";
                case ".ts": return "video/mp2t";
                case ".m4s": return "video/iso.segment";
                case ".mp4": return "video/mp4";
                case ".aac": return "audio/aac";
                case ".vtt": return "text/vtt";
                default: return "application/octet-stream";
            }
        }

        private static string CreateMediaId()
        {
            return DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) +
                   "-" + Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        private static void ValidateMediaId(string mediaId)
        {
            if (string.IsNullOrWhiteSpace(mediaId) ||
                mediaId == "." ||
                mediaId == ".." ||
                mediaId.IndexOf('/') >= 0 ||
                mediaId.IndexOf('\\') >= 0 ||
                mediaId.Any(char.IsControl))
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    "HLS media ID is invalid.");
            }
        }

        private sealed class HlsPackagePlan
        {
            public HlsPackagePlan(
                string masterPath,
                IReadOnlyList<HlsPackageFile> contentFiles,
                IReadOnlyList<HlsRenditionLocation> renditions)
            {
                MasterPath = masterPath;
                ContentFiles = contentFiles;
                Renditions = renditions;
            }

            public string MasterPath { get; }
            public IReadOnlyList<HlsPackageFile> ContentFiles { get; }
            public IReadOnlyList<HlsRenditionLocation> Renditions { get; }
        }

        private sealed class HlsPackageFile
        {
            public HlsPackageFile(string fullPath, string relativePath)
            {
                FullPath = fullPath;
                RelativePath = relativePath;
            }

            public string FullPath { get; }
            public string RelativePath { get; }
        }

        private sealed class HlsRenditionLocation
        {
            public HlsRenditionLocation(HlsRenditionInfo rendition, string relativePath)
            {
                Rendition = rendition;
                RelativePath = relativePath;
            }

            public HlsRenditionInfo Rendition { get; }
            public string RelativePath { get; }
        }
    }
}
