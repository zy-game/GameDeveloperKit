using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorCloud;
using GameDeveloperKit.StoryEditor.Media;

namespace GameDeveloperKit.MediaEditor
{
    internal enum HlsBatchPublishItemState
    {
        Pending,
        Probing,
        Transcoding,
        Uploading,
        CommittingCatalog,
        Completed,
        Failed,
        CatalogPending,
        Cancelled
    }

    internal sealed class HlsBatchPublishItem
    {
        public HlsBatchPublishItem(HlsPublishIntent intent)
        {
            Intent = intent ?? throw new ArgumentNullException(nameof(intent));
            State = HlsBatchPublishItemState.Pending;
            Message = "等待开始。";
        }

        public HlsPublishIntent Intent { get; }
        public HlsBatchPublishItemState State { get; internal set; }
        public float Progress { get; internal set; }
        public string Message { get; internal set; }
        public string Error { get; internal set; }
        public HlsTranscodeResult TranscodeResult { get; internal set; }
        public HlsPackagePublishResult PendingCatalogPackage { get; internal set; }
    }

    internal sealed class HlsBatchPublishDependencies
    {
        public Func<string, string, CancellationToken, UniTask<MediaProbeInfo>> ProbeAsync { get; set; }
        public Func<
            HlsTranscodeRequest,
            IProgress<HlsTranscodeProgress>,
            CancellationToken,
            UniTask<HlsTranscodeResult>> TranscodeAsync { get; set; }
        public Func<
            HlsPublishIntent,
            HlsTranscodeResult,
            IProgress<CloudUploadProgress>,
            IProgress<HlsPublishWorkflowStage>,
            CancellationToken,
            UniTask<HlsPublishWorkflowResult>> PublishAsync { get; set; }
        public Func<
            HlsPublishIntent,
            HlsTranscodeResult,
            CancellationToken,
            UniTask<HlsCatalogCommitResult>> CommitCatalogAsync { get; set; }
        public Func<string, bool> DirectoryExists { get; set; }

        public static HlsBatchPublishDependencies CreateDefault()
        {
            var probeService = new MediaProbeService();
            var transcodeService = new HlsTranscodeService();
            var publishWorkflow = new HlsPublishWorkflow();
            return new HlsBatchPublishDependencies
            {
                ProbeAsync = probeService.ProbeAsync,
                TranscodeAsync = transcodeService.TranscodeAsync,
                PublishAsync = publishWorkflow.PublishAsync,
                CommitCatalogAsync = publishWorkflow.CommitCatalogAsync,
                DirectoryExists = Directory.Exists
            };
        }

        public void Validate()
        {
            if (ProbeAsync == null ||
                TranscodeAsync == null ||
                PublishAsync == null ||
                CommitCatalogAsync == null ||
                DirectoryExists == null)
            {
                throw new InvalidOperationException("Batch publish dependencies are incomplete.");
            }
        }
    }

    internal sealed class HlsBatchPublishController
    {
        private readonly IReadOnlyList<HlsBatchPublishItem> m_Items;
        private readonly string m_ProjectRoot;
        private readonly string m_FfprobePath;
        private readonly HlsBatchPublishDependencies m_Dependencies;

        public HlsBatchPublishController(
            IReadOnlyList<HlsPublishIntent> intents,
            string projectRoot,
            string ffprobePath,
            HlsBatchPublishDependencies dependencies = null)
        {
            if (intents == null || intents.Count == 0)
            {
                throw new ArgumentException("At least one publish intent is required.", nameof(intents));
            }

            m_ProjectRoot = string.IsNullOrWhiteSpace(projectRoot)
                ? throw new ArgumentException("Project root cannot be empty.", nameof(projectRoot))
                : Path.GetFullPath(projectRoot);
            m_FfprobePath = string.IsNullOrWhiteSpace(ffprobePath)
                ? throw new ArgumentException("FFprobe path cannot be empty.", nameof(ffprobePath))
                : ffprobePath;
            m_Dependencies = dependencies ?? HlsBatchPublishDependencies.CreateDefault();
            m_Dependencies.Validate();
            m_Items = new ReadOnlyCollection<HlsBatchPublishItem>(
                intents.Select(intent => new HlsBatchPublishItem(intent)).ToArray());
        }

        public event Action<HlsBatchPublishItem> ItemChanged;

        public IReadOnlyList<HlsBatchPublishItem> Items => m_Items;
        public bool IsRunning { get; private set; }

        public async UniTask RunAsync(CancellationToken cancellationToken)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("Batch publish is already running.");
            }

            IsRunning = true;
            try
            {
                foreach (var item in m_Items.Where(candidate =>
                             candidate.State == HlsBatchPublishItemState.Pending))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ProcessItemAsync(item, cancellationToken);
                }
            }
            finally
            {
                IsRunning = false;
            }
        }

        public async UniTask RetryAsync(
            HlsBatchPublishItem item,
            CancellationToken cancellationToken)
        {
            if (item == null || m_Items.Contains(item) is false)
            {
                throw new ArgumentException("Item does not belong to this batch.", nameof(item));
            }

            if (IsRunning)
            {
                throw new InvalidOperationException("Cannot retry while the batch is running.");
            }

            IsRunning = true;
            try
            {
                if (item.State == HlsBatchPublishItemState.CatalogPending)
                {
                    await RetryCatalogAsync(item, cancellationToken);
                    return;
                }

                if (item.State != HlsBatchPublishItemState.Failed &&
                    item.State != HlsBatchPublishItemState.Cancelled)
                {
                    throw new InvalidOperationException("Only failed or cancelled items can be retried.");
                }

                item.State = HlsBatchPublishItemState.Pending;
                item.Progress = 0f;
                item.Message = "等待重试。";
                item.Error = string.Empty;
                Notify(item);
                await ProcessItemAsync(item, cancellationToken);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private async UniTask ProcessItemAsync(
            HlsBatchPublishItem item,
            CancellationToken cancellationToken)
        {
            try
            {
                SetState(item, HlsBatchPublishItemState.Probing, "正在探测源视频。", 0f);
                var source = await m_Dependencies.ProbeAsync(
                    m_FfprobePath,
                    item.Intent.SourceMp4Path,
                    cancellationToken);
                var renditions = HlsRenditionEligibilityPolicy
                    .Evaluate(source, HlsRenditionPresets.Default)
                    .Renditions
                    .Where(rendition => rendition.IsEligible)
                    .Select(rendition => rendition.Preset)
                    .ToArray();
                if (renditions.Length == 0)
                {
                    throw new InvalidOperationException("没有符合源视频分辨率和码率的固定档位。");
                }

                var draft = new HlsTranscodeRequest(
                    item.Intent.SourceMp4Path,
                    item.Intent.MediaId,
                    renditions);
                var target = HlsTranscodePlanner.ValidateRequest(draft, m_ProjectRoot);
                var request = new HlsTranscodeRequest(
                    item.Intent.SourceMp4Path,
                    item.Intent.MediaId,
                    renditions,
                    overwriteExisting: m_Dependencies.DirectoryExists(target));
                SetState(item, HlsBatchPublishItemState.Transcoding, "正在转码。", 0f);
                var transcodeProgress = new Progress<HlsTranscodeProgress>(value =>
                {
                    item.Progress = ClampProgress(value.Progress);
                    item.Message = value.Message;
                    Notify(item);
                });
                item.TranscodeResult = await m_Dependencies.TranscodeAsync(
                    request,
                    transcodeProgress,
                    cancellationToken);

                var uploadProgress = new Progress<CloudUploadProgress>(value =>
                {
                    item.State = HlsBatchPublishItemState.Uploading;
                    item.Progress = value.TotalBytes <= 0
                        ? 0f
                        : ClampProgress((float)(value.TotalBytesSent / (double)value.TotalBytes));
                    item.Message = "正在上传：" + value.ObjectKey;
                    Notify(item);
                });
                var stageProgress = new Progress<HlsPublishWorkflowStage>(stage =>
                {
                    item.State = stage == HlsPublishWorkflowStage.Uploading
                        ? HlsBatchPublishItemState.Uploading
                        : HlsBatchPublishItemState.CommittingCatalog;
                    item.Message = stage == HlsPublishWorkflowStage.Uploading
                        ? "正在上传 HLS 包。"
                        : "正在提交 Catalog。";
                    Notify(item);
                });
                var published = await m_Dependencies.PublishAsync(
                    item.Intent,
                    item.TranscodeResult,
                    uploadProgress,
                    stageProgress,
                    cancellationToken);
                item.PendingCatalogPackage = null;
                SetState(
                    item,
                    HlsBatchPublishItemState.Completed,
                    "发布完成。Media ID：" + published.Package.MediaId,
                    1f);
            }
            catch (OperationCanceledException)
            {
                SetState(item, HlsBatchPublishItemState.Cancelled, "任务已取消。", item.Progress);
                throw;
            }
            catch (HlsCatalogCommitPendingException exception)
            {
                item.PendingCatalogPackage = exception.Package;
                item.Error = exception.Message;
                SetState(
                    item,
                    HlsBatchPublishItemState.CatalogPending,
                    "HLS 已上传，等待重试提交 Catalog。",
                    item.Progress);
            }
            catch (Exception exception)
            {
                item.Error = exception.Message;
                SetState(item, HlsBatchPublishItemState.Failed, exception.Message, item.Progress);
            }
        }

        private async UniTask RetryCatalogAsync(
            HlsBatchPublishItem item,
            CancellationToken cancellationToken)
        {
            if (item.TranscodeResult == null || item.PendingCatalogPackage == null)
            {
                throw new InvalidOperationException("Catalog retry state is incomplete.");
            }

            try
            {
                SetState(
                    item,
                    HlsBatchPublishItemState.CommittingCatalog,
                    "正在重试提交 Catalog。",
                    item.Progress);
                await m_Dependencies.CommitCatalogAsync(
                    item.Intent,
                    item.TranscodeResult,
                    cancellationToken);
                item.PendingCatalogPackage = null;
                item.Error = string.Empty;
                SetState(item, HlsBatchPublishItemState.Completed, "Catalog 提交完成。", 1f);
            }
            catch (OperationCanceledException)
            {
                SetState(item, HlsBatchPublishItemState.Cancelled, "任务已取消。", item.Progress);
                throw;
            }
            catch (Exception exception)
            {
                item.Error = exception.Message;
                SetState(
                    item,
                    HlsBatchPublishItemState.CatalogPending,
                    "Catalog 提交仍未完成：" + exception.Message,
                    item.Progress);
            }
        }

        private void SetState(
            HlsBatchPublishItem item,
            HlsBatchPublishItemState state,
            string message,
            float progress)
        {
            item.State = state;
            item.Message = message ?? string.Empty;
            item.Progress = ClampProgress(progress);
            Notify(item);
        }

        private void Notify(HlsBatchPublishItem item)
        {
            ItemChanged?.Invoke(item);
        }

        private static float ClampProgress(float progress)
        {
            return Math.Max(0f, Math.Min(1f, progress));
        }
    }
}
