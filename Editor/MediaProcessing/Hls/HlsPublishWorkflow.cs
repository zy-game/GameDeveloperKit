using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorCloud;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.StoryEditor.Media;

namespace GameDeveloperKit.MediaEditor
{
    internal sealed class HlsPublishIntent
    {
        public HlsPublishIntent(
            string sourceMp4Path,
            string displayName,
            string sourceSha256,
            string mediaId,
            bool isOverwrite,
            DateTimeOffset? createdAtUtc,
            DateTimeOffset? expectedUpdatedAtUtc)
        {
            SourceMp4Path = sourceMp4Path ?? throw new ArgumentNullException(nameof(sourceMp4Path));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            SourceSha256 = sourceSha256 ?? throw new ArgumentNullException(nameof(sourceSha256));
            MediaId = mediaId ?? throw new ArgumentNullException(nameof(mediaId));
            IsOverwrite = isOverwrite;
            CreatedAtUtc = createdAtUtc;
            ExpectedUpdatedAtUtc = expectedUpdatedAtUtc;
        }

        public string SourceMp4Path { get; }
        public string DisplayName { get; }
        public string SourceSha256 { get; }
        public string MediaId { get; }
        public bool IsOverwrite { get; }
        public DateTimeOffset? CreatedAtUtc { get; }
        public DateTimeOffset? ExpectedUpdatedAtUtc { get; }
    }

    internal sealed class HlsPublishWorkflowResult
    {
        public HlsPublishWorkflowResult(
            HlsPackagePublishResult package,
            HlsCatalogCommitResult catalog)
        {
            Package = package;
            Catalog = catalog;
        }

        public HlsPackagePublishResult Package { get; }
        public HlsCatalogCommitResult Catalog { get; }
    }

    internal sealed class HlsCatalogCommitPendingException : InvalidOperationException
    {
        public HlsCatalogCommitPendingException(
            HlsPackagePublishResult package,
            Exception innerException)
            : base(
                "HLS 已上传，但 Catalog 提交失败；可直接重试 Catalog，无需重新转码。" +
                Environment.NewLine + innerException.Message,
                innerException)
        {
            Package = package;
        }

        public HlsPackagePublishResult Package { get; }
    }

    internal sealed class HlsPublishWorkflow
    {
        private readonly HlsPackagePublisher m_PackagePublisher;
        private readonly HlsCatalogOriginRepository m_CatalogRepository;
        private readonly Func<DateTimeOffset> m_UtcNow;

        public HlsPublishWorkflow()
            : this(
                new HlsPackagePublisher(),
                new HlsCatalogOriginRepository(),
                () => DateTimeOffset.UtcNow)
        {
        }

        internal HlsPublishWorkflow(
            HlsPackagePublisher packagePublisher,
            HlsCatalogOriginRepository catalogRepository,
            Func<DateTimeOffset> utcNow)
        {
            m_PackagePublisher = packagePublisher ?? throw new ArgumentNullException(nameof(packagePublisher));
            m_CatalogRepository = catalogRepository ?? throw new ArgumentNullException(nameof(catalogRepository));
            m_UtcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        }

        public async UniTask<HlsPublishWorkflowResult> PublishAsync(
            HlsPublishIntent intent,
            HlsTranscodeResult transcodeResult,
            IProgress<CloudUploadProgress> progress,
            CancellationToken cancellationToken)
        {
            if (intent == null)
            {
                throw new ArgumentNullException(nameof(intent));
            }

            HlsPackagePublishResult package;
            try
            {
                package = await m_PackagePublisher.PublishAsync(
                    new HlsPackagePublishRequest(
                        transcodeResult,
                        intent.DisplayName,
                        intent.MediaId),
                    progress,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (CloudException exception)
            {
                throw new InvalidOperationException(
                    "HLS 上传失败，覆盖可能不完整，请重新上传。" + Environment.NewLine + exception.Message,
                    exception);
            }

            HlsCatalogCommitResult catalog;
            try
            {
                catalog = await CommitCatalogAsync(intent, transcodeResult, cancellationToken);
            }
            catch (Exception exception) when (exception is CatalogException || exception is CloudException)
            {
                throw new HlsCatalogCommitPendingException(package, exception);
            }

            return new HlsPublishWorkflowResult(package, catalog);
        }

        public UniTask<HlsCatalogCommitResult> CommitCatalogAsync(
            HlsPublishIntent intent,
            HlsTranscodeResult transcodeResult,
            CancellationToken cancellationToken)
        {
            if (intent == null)
            {
                throw new ArgumentNullException(nameof(intent));
            }

            if (transcodeResult == null)
            {
                throw new ArgumentNullException(nameof(transcodeResult));
            }

            var item = CreateCatalogItem(intent, transcodeResult, m_UtcNow());
            return m_CatalogRepository.UpsertAsync(
                item,
                intent.IsOverwrite,
                intent.ExpectedUpdatedAtUtc,
                cancellationToken);
        }

        internal static CatalogItem CreateCatalogItem(
            HlsPublishIntent intent,
            HlsTranscodeResult result,
            DateTimeOffset now)
        {
            var renditions = result.Renditions
                .Select(rendition => new CatalogRendition(
                    rendition.Label,
                    null,
                    intent.MediaId + "/" + rendition.Label + "/index.m3u8",
                    rendition.Width,
                    rendition.Height,
                    rendition.Bitrate,
                    result.DurationMs))
                .ToArray();
            var primary = renditions
                .OrderByDescending(rendition => rendition.Width * (long)rendition.Height)
                .First();
            return new CatalogItem(
                intent.MediaId,
                intent.DisplayName,
                MediaKind.Video,
                intent.MediaId + "/master.m3u8",
                VideoFormat.Hls,
                intent.MediaId + "/" + HlsPreviewImage.FileName,
                primary.Width,
                primary.Height,
                primary.Bitrate,
                result.DurationMs,
                renditions,
                Path.GetFileName(intent.SourceMp4Path),
                intent.SourceSha256,
                Environment.UserName,
                intent.CreatedAtUtc ?? now,
                now,
                intent.MediaId + "/");
        }

        public static async UniTask<string> ComputeSourceSha256Async(
            string sourcePath,
            CancellationToken cancellationToken)
        {
            if (System.IO.File.Exists(sourcePath) is false)
            {
                throw new FileNotFoundException("MP4 文件不存在。", sourcePath);
            }

            using var algorithm = SHA256.Create();
            using var stream = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                128 * 1024,
                true);
            var buffer = new byte[128 * 1024];
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (read <= 0)
                {
                    break;
                }

                algorithm.TransformBlock(buffer, 0, read, null, 0);
            }

            algorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return string.Concat(algorithm.Hash.Select(value =>
                value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        public static string CreateMediaId()
        {
            return "media-" + Guid.NewGuid().ToString("N").Substring(0, 16);
        }
    }
}
