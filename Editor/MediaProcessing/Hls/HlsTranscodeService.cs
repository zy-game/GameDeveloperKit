using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorConfiguration;

namespace GameDeveloperKit.MediaEditor
{
    public sealed class HlsTranscodeService
    {
        private readonly HlsTranscodeDependencies m_Dependencies;

        public HlsTranscodeService()
            : this(HlsTranscodeDependencies.CreateDefault())
        {
        }

        internal HlsTranscodeService(HlsTranscodeDependencies dependencies)
        {
            m_Dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        }

        public async UniTask<HlsTranscodeResult> TranscodeAsync(
            HlsTranscodeRequest request,
            IProgress<HlsTranscodeProgress> progress,
            CancellationToken cancellationToken)
        {
            var targetDirectory = HlsTranscodePlanner.ValidateRequest(
                request,
                m_Dependencies.ProjectRoot);
            var toolchain = m_Dependencies.ToolchainProvider();
            if (toolchain == null || toolchain.IsReady is false)
            {
                throw new InvalidOperationException(
                    toolchain?.Message ?? "FFmpeg 工具链不可用。");
            }

            using (HlsOutputLease.Acquire(targetDirectory))
            {
                if (Directory.Exists(targetDirectory) && request.OverwriteExisting is false)
                {
                    throw new IOException($"HLS 输出目录已存在：{targetDirectory}");
                }

                progress?.Report(new HlsTranscodeProgress(
                    HlsTranscodeStage.Probing,
                    0f,
                    "正在探测输入 MP4。"));
                var source = await m_Dependencies.ProbeService.ProbeAsync(
                    toolchain.FfprobePath,
                    request.InputMp4Path,
                    cancellationToken);

                progress?.Report(new HlsTranscodeProgress(
                    HlsTranscodeStage.Planning,
                    1f,
                    "正在生成 HLS 转码计划。"));
                var plan = HlsTranscodePlanner.Create(
                    request,
                    source,
                    m_Dependencies.ProjectRoot);
                using (var transaction = m_Dependencies.TransactionFactory(
                           m_Dependencies.ProjectRoot,
                           targetDirectory,
                           request.OverwriteExisting))
                {
                    transaction.PrepareRenditionDirectories(plan);
                    var processResult = await EncodeAsync(
                        plan,
                        transaction.StagingDirectory,
                        toolchain.FfmpegPath,
                        progress,
                        cancellationToken);

                    progress?.Report(new HlsTranscodeProgress(
                        HlsTranscodeStage.Verifying,
                        0f,
                        "正在校验 HLS master、变体和切片。"));
                    var stagedRenditions = await m_Dependencies.OutputValidator.ValidateAsync(
                        plan,
                        transaction.StagingDirectory,
                        toolchain.FfprobePath,
                        cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new HlsTranscodeProgress(
                        HlsTranscodeStage.Committing,
                        0f,
                        "正在提交 HLS 包到 StreamingAssets。"));
                    transaction.Commit();

                    var finalRenditions = CreateFinalRenditions(
                        targetDirectory,
                        stagedRenditions);
                    var masterPath = Path.Combine(targetDirectory, "master.m3u8").Replace('\\', '/');
                    progress?.Report(new HlsTranscodeProgress(
                        HlsTranscodeStage.Completed,
                        1f,
                        "HLS 转码完成。"));
                    return new HlsTranscodeResult(
                        targetDirectory.Replace('\\', '/'),
                        masterPath,
                        finalRenditions,
                        processResult.StandardOutput,
                        processResult.StandardError);
                }
            }
        }

        private async UniTask<MediaProcessResult> EncodeAsync(
            HlsTranscodePlan plan,
            string stagingDirectory,
            string ffmpegPath,
            IProgress<HlsTranscodeProgress> progress,
            CancellationToken cancellationToken)
        {
            progress?.Report(new HlsTranscodeProgress(
                HlsTranscodeStage.Encoding,
                0f,
                "正在编码 HLS 变体。"));
            var arguments = HlsFfmpegCommandBuilder.Build(plan, stagingDirectory);
            var currentProgress = 0f;
            var progressLock = new object();
            void ReportLine(string line)
            {
                if (progress == null)
                {
                    return;
                }

                lock (progressLock)
                {
                    var message = "正在编码 HLS 变体。";
                    if (FfmpegProgressParser.TryParse(
                        line,
                        plan.Source.DurationSeconds,
                        out var value,
                        out var processedTime))
                    {
                        currentProgress = value;
                        message = $"正在编码：{processedTime:hh\\:mm\\:ss} / " +
                                  $"{TimeSpan.FromSeconds(plan.Source.DurationSeconds):hh\\:mm\\:ss}";
                    }

                    progress.Report(new HlsTranscodeProgress(
                        HlsTranscodeStage.Encoding,
                        currentProgress,
                        message,
                        line));
                }
            }

            var result = await m_Dependencies.ProcessRunner.RunAsync(
                new MediaProcessRequest(
                    ffmpegPath,
                    arguments,
                    stagingDirectory,
                    TimeSpan.Zero,
                    ReportLine,
                    ReportLine),
                cancellationToken);
            if (result.Succeeded is false)
            {
                throw new InvalidDataException(
                    $"FFmpeg 转码失败，退出码 {result.ExitCode}：{result.StandardError}");
            }

            return result;
        }

        private static IReadOnlyList<HlsRenditionInfo> CreateFinalRenditions(
            string targetDirectory,
            IReadOnlyList<HlsRenditionInfo> stagedRenditions)
        {
            var result = new List<HlsRenditionInfo>(stagedRenditions.Count);
            for (var i = 0; i < stagedRenditions.Count; i++)
            {
                var rendition = stagedRenditions[i];
                result.Add(new HlsRenditionInfo(
                    rendition.Label,
                    rendition.Width,
                    rendition.Height,
                    rendition.Bitrate,
                    Path.Combine(targetDirectory, rendition.Label, "index.m3u8").Replace('\\', '/')));
            }

            return new ReadOnlyCollection<HlsRenditionInfo>(result);
        }
    }

    internal sealed class HlsTranscodeDependencies
    {
        public HlsTranscodeDependencies(string projectRoot)
        {
            ProjectRoot = string.IsNullOrWhiteSpace(projectRoot)
                ? throw new ArgumentException("Project root cannot be empty.", nameof(projectRoot))
                : Path.GetFullPath(projectRoot);
            var resolver = new FfmpegToolchainResolver();
            var userConfig = EditorUserConfig.LoadOrCreate();
            ProbeService = new MediaProbeService();
            ToolchainProvider = () => resolver.Detect(userConfig.FfmpegPath, userConfig.FfprobePath);
            ProcessRunner = new MediaProcessRunner();
            OutputValidator = new HlsOutputValidator(ProbeService);
            TransactionFactory = (root, target, overwrite) =>
                new HlsOutputTransaction(root, target, overwrite);
        }

        public string ProjectRoot { get; }
        public Func<FfmpegToolchainStatus> ToolchainProvider { get; set; }
        public IMediaProbeService ProbeService { get; set; }
        public IMediaProcessRunner ProcessRunner { get; set; }
        public HlsOutputValidator OutputValidator { get; set; }
        public Func<string, string, bool, HlsOutputTransaction> TransactionFactory { get; set; }

        public static HlsTranscodeDependencies CreateDefault()
        {
            return new HlsTranscodeDependencies(Directory.GetCurrentDirectory());
        }
    }
}
