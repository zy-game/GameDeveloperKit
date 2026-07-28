using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using IOFile = System.IO.File;

namespace GameDeveloperKit.MediaEditor
{
    internal static class HlsPreviewImage
    {
        public const string FileName = "preview.jpg";
        public const int Width = 640;
        public const int Height = 360;

        public static IReadOnlyList<string> BuildArguments(
            HlsTranscodePlan plan,
            string outputDirectory)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var sampleSeconds = Math.Min(5d, plan.Source.DurationSeconds / 10d);
            var outputPath = Path.Combine(outputDirectory, FileName);
            return new[]
            {
                "-hide_banner",
                "-y",
                "-ss",
                sampleSeconds.ToString("0.###", CultureInfo.InvariantCulture),
                "-i",
                plan.Request.InputMp4Path,
                "-frames:v",
                "1",
                "-an",
                "-vf",
                "scale='min(640,iw)':'min(360,ih)':force_original_aspect_ratio=decrease," +
                "pad=640:360:(ow-iw)/2:(oh-ih)/2:black",
                "-q:v",
                "3",
                outputPath
            };
        }

        public static void Validate(string path)
        {
            if (IOFile.Exists(path) is false)
            {
                throw new InvalidDataException("HLS 预览图 preview.jpg 不存在。");
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length < 12 || stream.ReadByte() != 0xff || stream.ReadByte() != 0xd8)
            {
                throw new InvalidDataException("HLS 预览图不是有效的 JPEG 文件。");
            }

            while (stream.Position + 4 <= stream.Length)
            {
                if (stream.ReadByte() != 0xff)
                {
                    continue;
                }

                int marker;
                do
                {
                    marker = stream.ReadByte();
                } while (marker == 0xff);

                if (marker < 0 || marker == 0xd9 || marker == 0xda)
                {
                    break;
                }

                var segmentLength = ReadUInt16(stream);
                if (segmentLength < 2 || stream.Position + segmentLength - 2 > stream.Length)
                {
                    throw new InvalidDataException("HLS 预览图 JPEG 结构损坏。");
                }

                if (IsStartOfFrame(marker))
                {
                    if (segmentLength < 7)
                    {
                        break;
                    }

                    stream.ReadByte();
                    var height = ReadUInt16(stream);
                    var width = ReadUInt16(stream);
                    if (width != Width || height != Height)
                    {
                        throw new InvalidDataException(
                            $"HLS 预览图尺寸必须为 {Width}x{Height}，实际为 {width}x{height}。");
                    }

                    return;
                }

                stream.Position += segmentLength - 2;
            }

            throw new InvalidDataException("HLS 预览图缺少 JPEG 尺寸信息。");
        }

        private static int ReadUInt16(Stream stream)
        {
            var high = stream.ReadByte();
            var low = stream.ReadByte();
            if (high < 0 || low < 0)
            {
                throw new EndOfStreamException();
            }

            return high << 8 | low;
        }

        private static bool IsStartOfFrame(int marker)
        {
            return marker >= 0xc0 && marker <= 0xc3 ||
                   marker >= 0xc5 && marker <= 0xc7 ||
                   marker >= 0xc9 && marker <= 0xcb ||
                   marker >= 0xcd && marker <= 0xcf;
        }
    }
}
