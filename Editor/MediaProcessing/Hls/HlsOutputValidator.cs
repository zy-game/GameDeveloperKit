using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using IOFile = System.IO.File;

namespace GameDeveloperKit.MediaEditor
{
    internal sealed class HlsOutputValidator
    {
        private const string StreamInfoPrefix = "#EXT-X-STREAM-INF:";
        private readonly IMediaProbeService m_ProbeService;

        public HlsOutputValidator()
            : this(new MediaProbeService())
        {
        }

        internal HlsOutputValidator(IMediaProbeService probeService)
        {
            m_ProbeService = probeService ?? throw new ArgumentNullException(nameof(probeService));
        }

        public async UniTask<IReadOnlyList<HlsRenditionInfo>> ValidateAsync(
            HlsTranscodePlan plan,
            string outputDirectory,
            string ffprobePath,
            CancellationToken cancellationToken)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var root = Path.GetFullPath(outputDirectory ?? string.Empty);
            var masterPath = Path.Combine(root, "master.m3u8");
            if (IOFile.Exists(masterPath) is false)
            {
                throw new InvalidDataException("HLS 输出缺少 master.m3u8。");
            }

            var entries = ParseMaster(root, masterPath);
            if (entries.Count != plan.Renditions.Count)
            {
                throw new InvalidDataException(
                    $"HLS master 变体数量不正确，预期 {plan.Renditions.Count}，实际 {entries.Count}。");
            }

            var result = new List<HlsRenditionInfo>(entries.Count);
            for (var i = 0; i < plan.Renditions.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var expected = plan.Renditions[i];
                var expectedRelativePath = Normalize(expected.PlaylistRelativePath);
                var entry = entries.FirstOrDefault(candidate => string.Equals(
                    candidate.RelativePath,
                    expectedRelativePath,
                    StringComparison.Ordinal));
                if (entry == null)
                {
                    throw new InvalidDataException($"HLS master 缺少变体：{expected.Label}。");
                }

                if (entry.Width != expected.Width || entry.Height != expected.Height || entry.Bandwidth <= 0)
                {
                    throw new InvalidDataException($"HLS master 变体元数据不正确：{expected.Label}。");
                }

                ValidateVariantPlaylist(root, entry.FullPath);
                var probe = await m_ProbeService.ProbeAsync(
                    ffprobePath,
                    entry.FullPath,
                    cancellationToken);
                if (probe.Width != expected.Width || probe.Height != expected.Height)
                {
                    throw new InvalidDataException($"HLS 变体尺寸不正确：{expected.Label}。");
                }

                var durationTolerance = Math.Max(1d, plan.Request.SegmentDurationSeconds + 0.5d);
                if (Math.Abs(probe.DurationSeconds - plan.Source.DurationSeconds) > durationTolerance)
                {
                    throw new InvalidDataException($"HLS 变体时长超出允许误差：{expected.Label}。");
                }

                if (probe.HasAudio != plan.Source.HasAudio)
                {
                    throw new InvalidDataException($"HLS 变体音频流与输入不一致：{expected.Label}。");
                }

                result.Add(new HlsRenditionInfo(
                    expected.Label,
                    entry.Width,
                    entry.Height,
                    entry.Bandwidth,
                    entry.FullPath.Replace('\\', '/')));
            }

            return new ReadOnlyCollection<HlsRenditionInfo>(result);
        }

        private static List<MasterEntry> ParseMaster(string root, string masterPath)
        {
            var lines = IOFile.ReadAllLines(masterPath);
            if (lines.Any(line => string.Equals(line.Trim(), "#EXTM3U", StringComparison.Ordinal)) is false)
            {
                throw new InvalidDataException("HLS master 不是有效的 M3U8 playlist。");
            }

            var result = new List<MasterEntry>();
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith(StreamInfoPrefix, StringComparison.Ordinal) is false)
                {
                    continue;
                }

                ParseStreamInfo(line, out var bandwidth, out var width, out var height);
                var locationIndex = i + 1;
                while (locationIndex < lines.Length &&
                       (string.IsNullOrWhiteSpace(lines[locationIndex]) ||
                        lines[locationIndex].TrimStart().StartsWith("#", StringComparison.Ordinal)))
                {
                    locationIndex++;
                }

                if (locationIndex >= lines.Length)
                {
                    throw new InvalidDataException("HLS master 变体缺少 playlist 路径。");
                }

                var relativePath = Normalize(lines[locationIndex].Trim());
                var fullPath = ResolveRelativePath(root, Path.GetDirectoryName(masterPath), relativePath);
                if (IOFile.Exists(fullPath) is false)
                {
                    throw new InvalidDataException($"HLS master 引用不存在的 playlist：{relativePath}。");
                }

                result.Add(new MasterEntry(relativePath, fullPath, bandwidth, width, height));
                i = locationIndex;
            }

            if (result.Count == 0)
            {
                throw new InvalidDataException("HLS master 不包含任何变体。");
            }

            return result;
        }

        private static void ParseStreamInfo(
            string line,
            out int bandwidth,
            out int width,
            out int height)
        {
            bandwidth = 0;
            width = 0;
            height = 0;
            var attributes = line.Substring(StreamInfoPrefix.Length).Split(',');
            for (var i = 0; i < attributes.Length; i++)
            {
                var pair = attributes[i].Split(new[] { '=' }, 2);
                if (pair.Length != 2)
                {
                    continue;
                }

                if (string.Equals(pair[0].Trim(), "BANDWIDTH", StringComparison.Ordinal))
                {
                    int.TryParse(pair[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out bandwidth);
                }
                else if (string.Equals(pair[0].Trim(), "RESOLUTION", StringComparison.Ordinal))
                {
                    var resolution = pair[1].Trim().Split('x');
                    if (resolution.Length == 2)
                    {
                        int.TryParse(resolution[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out width);
                        int.TryParse(resolution[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out height);
                    }
                }
            }
        }

        private static void ValidateVariantPlaylist(string root, string playlistPath)
        {
            var lines = IOFile.ReadAllLines(playlistPath);
            if (lines.Any(line => string.Equals(line.Trim(), "#EXT-X-ENDLIST", StringComparison.Ordinal)) is false)
            {
                throw new InvalidDataException($"HLS 变体不是 VOD playlist：{playlistPath}。");
            }

            var segmentCount = 0;
            var parent = Path.GetDirectoryName(playlistPath);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var segmentPath = ResolveRelativePath(root, parent, Normalize(line));
                if (IOFile.Exists(segmentPath) is false)
                {
                    throw new InvalidDataException($"HLS 变体引用不存在的切片：{line}。");
                }

                segmentCount++;
            }

            if (segmentCount == 0)
            {
                throw new InvalidDataException($"HLS 变体没有媒体切片：{playlistPath}。");
            }
        }

        private static string ResolveRelativePath(string root, string parent, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) ||
                Uri.TryCreate(relativePath, UriKind.Absolute, out _) ||
                relativePath.StartsWith("/", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"HLS playlist 包含非相对路径：{relativePath}。");
            }

            var fullPath = Path.GetFullPath(Path.Combine(
                parent ?? root,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var rootPrefix = root.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) is false)
            {
                throw new InvalidDataException($"HLS playlist 路径逃逸输出目录：{relativePath}。");
            }

            return fullPath;
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/');
        }

        private sealed class MasterEntry
        {
            public MasterEntry(
                string relativePath,
                string fullPath,
                int bandwidth,
                int width,
                int height)
            {
                RelativePath = relativePath;
                FullPath = fullPath;
                Bandwidth = bandwidth;
                Width = width;
                Height = height;
            }

            public string RelativePath { get; }
            public string FullPath { get; }
            public int Bandwidth { get; }
            public int Width { get; }
            public int Height { get; }
        }
    }
}
