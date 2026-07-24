using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.MediaEditor
{
    internal interface IFfmpegToolchainValidator
    {
        UniTask ValidateAsync(
            string ffmpegPath,
            string ffprobePath,
            CancellationToken cancellationToken);
    }

    internal sealed class FfmpegToolchainValidator : IFfmpegToolchainValidator
    {
        public async UniTask ValidateAsync(
            string ffmpegPath,
            string ffprobePath,
            CancellationToken cancellationToken)
        {
            var ffmpegVersion = await RunAsync(ffmpegPath, "-hide_banner -version", cancellationToken);
            var ffprobeVersion = await RunAsync(ffprobePath, "-hide_banner -version", cancellationToken);
            var encoders = await RunAsync(ffmpegPath, "-hide_banner -encoders", cancellationToken);

            if (ffmpegVersion.IndexOf("ffmpeg version", StringComparison.OrdinalIgnoreCase) < 0 ||
                ffprobeVersion.IndexOf("ffprobe version", StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new InvalidOperationException("下载的工具未返回有效的 FFmpeg/ffprobe 版本信息。");
            }

            if (encoders.IndexOf("libx264", StringComparison.OrdinalIgnoreCase) < 0 ||
                encoders.IndexOf(" aac", StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new InvalidOperationException("下载的 FFmpeg 不包含 H.264(libx264) 或 AAC 编码器。");
            }
        }

        private static async UniTask<string> RunAsync(
            string executablePath,
            string arguments,
            CancellationToken cancellationToken)
        {
            var output = new StringBuilder();
            var error = new StringBuilder();
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                process.OutputDataReceived += (_, args) => Append(output, args.Data);
                process.ErrorDataReceived += (_, args) => Append(error, args.Data);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                while (process.HasExited is false)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        TryKill(process);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    await UniTask.Yield();
                }

                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"FFmpeg 工具验证失败，退出码 {process.ExitCode}：{Read(error)}");
                }
            }

            return Read(output) + Environment.NewLine + Read(error);
        }

        private static void Append(StringBuilder buffer, string line)
        {
            if (line == null)
            {
                return;
            }

            lock (buffer)
            {
                buffer.AppendLine(line);
            }
        }

        private static string Read(StringBuilder buffer)
        {
            lock (buffer)
            {
                return buffer.ToString();
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (process.HasExited is false)
                {
                    process.Kill();
                }
            }
            catch (InvalidOperationException exception)
            {
                UnityEngine.Debug.LogWarning($"FFmpeg 验证进程已经结束，无法再次终止：{exception.Message}");
            }
        }
    }
}
