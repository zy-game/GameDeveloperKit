using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GameDeveloperKit.MediaEditor
{
    internal static class HlsFfmpegCommandBuilder
    {
        public static IReadOnlyList<string> Build(
            HlsTranscodePlan plan,
            string outputDirectory)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("Output directory cannot be empty.", nameof(outputDirectory));
            }

            if (plan.Renditions.Count == 0)
            {
                throw new ArgumentException("Transcode plan must contain at least one rendition.", nameof(plan));
            }

            var arguments = new List<string>
            {
                "-hide_banner",
                "-y",
                "-i",
                plan.Request.InputMp4Path,
                "-filter_complex",
                BuildVideoFilter(plan.Renditions)
            };
            for (var i = 0; i < plan.Renditions.Count; i++)
            {
                arguments.Add("-map");
                arguments.Add($"[v{i}]");
                if (plan.Source.HasAudio)
                {
                    arguments.Add("-map");
                    arguments.Add("0:a:0");
                }
            }

            var keyframeInterval = Math.Max(
                1,
                (int)Math.Round(plan.Source.FrameRate * plan.Request.SegmentDurationSeconds));
            for (var i = 0; i < plan.Renditions.Count; i++)
            {
                var rendition = plan.Renditions[i];
                Add(arguments, $"-c:v:{i}", "libx264");
                Add(arguments, $"-preset:v:{i}", "medium");
                Add(arguments, $"-pix_fmt:v:{i}", "yuv420p");
                Add(arguments, $"-b:v:{i}", rendition.VideoBitrate.ToString(CultureInfo.InvariantCulture));
                Add(arguments, $"-maxrate:v:{i}", rendition.VideoBitrate.ToString(CultureInfo.InvariantCulture));
                Add(arguments, $"-bufsize:v:{i}", (rendition.VideoBitrate * 2L).ToString(CultureInfo.InvariantCulture));
                Add(arguments, $"-g:v:{i}", keyframeInterval.ToString(CultureInfo.InvariantCulture));
                Add(arguments, $"-keyint_min:v:{i}", keyframeInterval.ToString(CultureInfo.InvariantCulture));
                Add(arguments, $"-sc_threshold:v:{i}", "0");
                Add(
                    arguments,
                    $"-force_key_frames:v:{i}",
                    $"expr:gte(t,n_forced*{plan.Request.SegmentDurationSeconds.ToString(CultureInfo.InvariantCulture)})");
                if (plan.Source.HasAudio)
                {
                    Add(arguments, $"-c:a:{i}", "aac");
                    Add(arguments, $"-b:a:{i}", rendition.AudioBitrate.ToString(CultureInfo.InvariantCulture));
                }
            }

            Add(arguments, "-f", "hls");
            Add(
                arguments,
                "-hls_time",
                plan.Request.SegmentDurationSeconds.ToString(CultureInfo.InvariantCulture));
            Add(arguments, "-hls_playlist_type", "vod");
            Add(arguments, "-hls_flags", "independent_segments");
            Add(arguments, "-master_pl_name", "master.m3u8");
            Add(arguments, "-var_stream_map", BuildVariantMap(plan));
            Add(arguments, "-hls_segment_filename", Normalize(Path.Combine(
                outputDirectory,
                "%v",
                "segment_%05d.ts")));
            Add(arguments, "-progress", "pipe:1");
            arguments.Add("-nostats");
            arguments.Add(Normalize(Path.Combine(outputDirectory, "%v", "index.m3u8")));
            return arguments;
        }

        private static string BuildVideoFilter(IReadOnlyList<HlsRenditionPlan> renditions)
        {
            var builder = new StringBuilder();
            if (renditions.Count == 1)
            {
                AppendScale(builder, "0:v:0", renditions[0], 0);
                return builder.ToString();
            }

            builder.Append("[0:v:0]split=");
            builder.Append(renditions.Count.ToString(CultureInfo.InvariantCulture));
            for (var i = 0; i < renditions.Count; i++)
            {
                builder.Append($"[v{i}in]");
            }

            builder.Append(';');
            for (var i = 0; i < renditions.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(';');
                }

                AppendScale(builder, $"v{i}in", renditions[i], i);
            }

            return builder.ToString();
        }

        private static void AppendScale(
            StringBuilder builder,
            string input,
            HlsRenditionPlan rendition,
            int index)
        {
            builder.Append('[');
            builder.Append(input);
            builder.Append("]scale=w=");
            builder.Append(rendition.Width.ToString(CultureInfo.InvariantCulture));
            builder.Append(":h=");
            builder.Append(rendition.Height.ToString(CultureInfo.InvariantCulture));
            builder.Append(":flags=lanczos[v");
            builder.Append(index.ToString(CultureInfo.InvariantCulture));
            builder.Append(']');
        }

        private static string BuildVariantMap(HlsTranscodePlan plan)
        {
            var values = new string[plan.Renditions.Count];
            for (var i = 0; i < plan.Renditions.Count; i++)
            {
                values[i] = plan.Source.HasAudio
                    ? $"v:{i},a:{i},name:{plan.Renditions[i].Label}"
                    : $"v:{i},name:{plan.Renditions[i].Label}";
            }

            return string.Join(" ", values);
        }

        private static void Add(List<string> arguments, string name, string value)
        {
            arguments.Add(name);
            arguments.Add(value);
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
