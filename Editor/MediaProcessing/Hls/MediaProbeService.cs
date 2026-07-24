using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace GameDeveloperKit.MediaEditor
{
    internal interface IMediaProbeService
    {
        UniTask<MediaProbeInfo> ProbeAsync(
            string ffprobePath,
            string inputPath,
            CancellationToken cancellationToken);
    }

    internal sealed class MediaProbeService : IMediaProbeService
    {
        private readonly IMediaProcessRunner m_ProcessRunner;

        public MediaProbeService()
            : this(new MediaProcessRunner())
        {
        }

        internal MediaProbeService(IMediaProcessRunner processRunner)
        {
            m_ProcessRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        }

        public async UniTask<MediaProbeInfo> ProbeAsync(
            string ffprobePath,
            string inputPath,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                throw new ArgumentException("Input path cannot be empty.", nameof(inputPath));
            }

            var result = await m_ProcessRunner.RunAsync(
                new MediaProcessRequest(
                    ffprobePath,
                    new[]
                    {
                        "-v", "error",
                        "-print_format", "json",
                        "-show_streams",
                        "-show_format",
                        inputPath
                    },
                    Path.GetDirectoryName(inputPath),
                    TimeSpan.FromSeconds(30)),
                cancellationToken);
            if (result.Succeeded is false)
            {
                throw new InvalidDataException(
                    $"ffprobe 失败，退出码 {result.ExitCode}：{result.StandardError}");
            }

            return Parse(result.StandardOutput);
        }

        internal static MediaProbeInfo Parse(string json)
        {
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("ffprobe 返回了无效 JSON。", exception);
            }

            var streams = root["streams"] as JArray;
            var video = streams?
                .OfType<JObject>()
                .FirstOrDefault(stream => string.Equals(
                    stream.Value<string>("codec_type"),
                    "video",
                    StringComparison.Ordinal));
            if (video == null)
            {
                throw new InvalidDataException("输入 MP4 不包含视频流。");
            }

            var width = video.Value<int?>("width") ?? 0;
            var height = video.Value<int?>("height") ?? 0;
            var duration = ParsePositiveDouble(video.Value<string>("duration"));
            if (duration <= 0d && root["format"] is JObject format)
            {
                duration = ParsePositiveDouble(format.Value<string>("duration"));
            }

            var frameRate = ParseFrameRate(video.Value<string>("avg_frame_rate"));
            if (frameRate <= 0d)
            {
                frameRate = ParseFrameRate(video.Value<string>("r_frame_rate"));
            }

            var hasAudio = streams?
                .OfType<JObject>()
                .Any(stream => string.Equals(
                    stream.Value<string>("codec_type"),
                    "audio",
                    StringComparison.Ordinal)) == true;
            return new MediaProbeInfo(width, height, duration, frameRate, hasAudio);
        }

        private static double ParsePositiveDouble(string value)
        {
            return double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var result) && result > 0d
                ? result
                : 0d;
        }

        private static double ParseFrameRate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0d;
            }

            var parts = value.Split('/');
            if (parts.Length == 2)
            {
                var numerator = ParsePositiveDouble(parts[0]);
                var denominator = ParsePositiveDouble(parts[1]);
                return denominator > 0d ? numerator / denominator : 0d;
            }

            return ParsePositiveDouble(value);
        }
    }
}
