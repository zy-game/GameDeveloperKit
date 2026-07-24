using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using IOFile = System.IO.File;

namespace GameDeveloperKit.MediaEditor
{
    public sealed class FfmpegToolchainInstaller
    {
        private const string WorkingRelativePath = "Library/GameDeveloperKit/MediaProcessing/Install";

        private readonly FfmpegToolchainResolver m_Resolver;
        private readonly IFfmpegToolchainDownload m_Download;
        private readonly IFfmpegToolchainValidator m_Validator;
        private readonly string m_ProjectRoot;
        private readonly FfmpegToolchainPackage m_PackageOverride;

        public FfmpegToolchainInstaller()
            : this(
                new FfmpegToolchainResolver(),
                new UnityWebRequestFfmpegToolchainDownload(),
                new FfmpegToolchainValidator(),
                null)
        {
        }

        internal FfmpegToolchainInstaller(
            FfmpegToolchainResolver resolver,
            IFfmpegToolchainDownload download,
            IFfmpegToolchainValidator validator,
            FfmpegToolchainPackage packageOverride)
        {
            m_Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            m_Download = download ?? throw new ArgumentNullException(nameof(download));
            m_Validator = validator ?? throw new ArgumentNullException(nameof(validator));
            m_ProjectRoot = resolver.ProjectRoot;
            m_PackageOverride = packageOverride;
        }

        public FfmpegToolchainStatus Detect()
        {
            return m_Resolver.Detect(string.Empty, string.Empty);
        }

        public async UniTask<FfmpegToolchainStatus> InstallAsync(
            IProgress<ToolchainInstallProgress> progress,
            CancellationToken cancellationToken)
        {
            var current = Detect();
            var package = m_PackageOverride ?? current.Package;
            if (package == null)
            {
                return current;
            }

            var workingRoot = Path.Combine(
                m_ProjectRoot,
                WorkingRelativePath,
                Guid.NewGuid().ToString("N"));
            var archivePath = Path.Combine(workingRoot, "ffmpeg.zip");
            var extractedPath = Path.Combine(workingRoot, "extracted");
            var installedPath = Path.Combine(workingRoot, "installed");
            var backupPath = Path.Combine(workingRoot, "backup");
            var destinationPath = m_Resolver.GetManagedToolDirectory(package);
            Directory.CreateDirectory(workingRoot);

            try
            {
                await m_Download.DownloadAsync(package, archivePath, progress, cancellationToken);
                await VerifyHashAsync(archivePath, package.ArchiveSha256, progress, cancellationToken);
                ExtractTools(archivePath, extractedPath, installedPath, package, progress, cancellationToken);

                progress?.Report(new ToolchainInstallProgress(
                    ToolchainInstallStage.VerifyingTools,
                    0f,
                    "正在验证 FFmpeg 版本和编码器。"));
                await m_Validator.ValidateAsync(
                    Path.Combine(installedPath, "ffmpeg.exe"),
                    Path.Combine(installedPath, "ffprobe.exe"),
                    cancellationToken);

                progress?.Report(new ToolchainInstallProgress(
                    ToolchainInstallStage.Committing,
                    0f,
                    "正在提交托管 FFmpeg。"));
                CommitInstallation(installedPath, destinationPath, backupPath);
                progress?.Report(new ToolchainInstallProgress(
                    ToolchainInstallStage.Completed,
                    1f,
                    "FFmpeg 安装完成。"));
                return Detect();
            }
            finally
            {
                DeleteDirectory(workingRoot);
            }
        }

        internal static async UniTask VerifyHashAsync(
            string archivePath,
            string expectedSha256,
            IProgress<ToolchainInstallProgress> progress,
            CancellationToken cancellationToken)
        {
            progress?.Report(new ToolchainInstallProgress(
                ToolchainInstallStage.VerifyingArchive,
                0f,
                "正在校验 FFmpeg 下载包。"));

            using (var sha256 = SHA256.Create())
            using (var stream = IOFile.OpenRead(archivePath))
            {
                var buffer = new byte[1024 * 1024];
                long total = 0;
                int count;
                while ((count = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    sha256.TransformBlock(buffer, 0, count, null, 0);
                    total += count;
                    progress?.Report(new ToolchainInstallProgress(
                        ToolchainInstallStage.VerifyingArchive,
                        stream.Length == 0 ? 0f : (float)total / stream.Length,
                        "正在校验 FFmpeg 下载包。"));
                }

                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                var actual = ToHex(sha256.Hash);
                if (string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase) is false)
                {
                    throw new InvalidDataException(
                        $"FFmpeg 下载包 SHA-256 不匹配。期望 {expectedSha256}，实际 {actual}。");
                }
            }
        }

        internal static void ExtractTools(
            string archivePath,
            string extractedPath,
            string installedPath,
            FfmpegToolchainPackage package,
            IProgress<ToolchainInstallProgress> progress,
            CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(extractedPath);
            Directory.CreateDirectory(installedPath);
            var extractionRoot = Path.GetFullPath(extractedPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string ffmpegSource = null;
            string ffprobeSource = null;

            using (var archive = ZipFile.OpenRead(archivePath))
            {
                for (var i = 0; i < archive.Entries.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = archive.Entries[i];
                    var destination = GetSafeEntryPath(extractionRoot, entry.FullName);
                    if (entry.Name.Length == 0)
                    {
                        continue;
                    }

                    if (string.Equals(entry.Name, "ffmpeg.exe", StringComparison.OrdinalIgnoreCase) is false &&
                        string.Equals(entry.Name, "ffprobe.exe", StringComparison.OrdinalIgnoreCase) is false)
                    {
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? extractionRoot);
                    entry.ExtractToFile(destination, false);
                    if (string.Equals(entry.Name, "ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        ffmpegSource = AssignUnique(ffmpegSource, destination, "ffmpeg.exe");
                    }
                    else
                    {
                        ffprobeSource = AssignUnique(ffprobeSource, destination, "ffprobe.exe");
                    }

                    progress?.Report(new ToolchainInstallProgress(
                        ToolchainInstallStage.Extracting,
                        archive.Entries.Count == 0 ? 0f : (float)(i + 1) / archive.Entries.Count,
                        "正在解压 FFmpeg。"));
                }
            }

            if (ffmpegSource == null || ffprobeSource == null)
            {
                throw new InvalidDataException("FFmpeg 下载包中缺少 ffmpeg.exe 或 ffprobe.exe。");
            }

            IOFile.Move(ffmpegSource, Path.Combine(installedPath, "ffmpeg.exe"));
            IOFile.Move(ffprobeSource, Path.Combine(installedPath, "ffprobe.exe"));
            IOFile.WriteAllText(
                Path.Combine(installedPath, "NOTICE.txt"),
                BuildNotice(package),
                new UTF8Encoding(false));
        }

        internal static void CommitInstallation(
            string installedPath,
            string destinationPath,
            string backupPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");
            var hadPrevious = Directory.Exists(destinationPath);
            if (hadPrevious)
            {
                Directory.Move(destinationPath, backupPath);
            }

            try
            {
                Directory.Move(installedPath, destinationPath);
            }
            catch
            {
                if (hadPrevious && Directory.Exists(backupPath) && Directory.Exists(destinationPath) is false)
                {
                    Directory.Move(backupPath, destinationPath);
                }

                throw;
            }

            DeleteDirectory(backupPath);
        }

        private static string GetSafeEntryPath(string extractionRoot, string entryName)
        {
            var normalized = (entryName ?? string.Empty).Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal) ||
                normalized.IndexOf(':') >= 0)
            {
                throw new InvalidDataException($"FFmpeg 下载包包含非法路径：{entryName}");
            }

            var destination = Path.GetFullPath(Path.Combine(extractionRoot, normalized));
            if (destination.StartsWith(extractionRoot, StringComparison.OrdinalIgnoreCase) is false)
            {
                throw new InvalidDataException($"FFmpeg 下载包路径逃逸安装目录：{entryName}");
            }

            return destination;
        }

        private static string AssignUnique(string current, string candidate, string fileName)
        {
            if (current != null)
            {
                throw new InvalidDataException($"FFmpeg 下载包包含重复的 {fileName}。");
            }

            return candidate;
        }

        private static string BuildNotice(FfmpegToolchainPackage package)
        {
            return $"FFmpeg managed toolchain{Environment.NewLine}"
                + $"Version: {package.Version}{Environment.NewLine}"
                + $"Source: {package.ArchiveUrl}{Environment.NewLine}"
                + $"SHA-256: {package.ArchiveSha256}{Environment.NewLine}"
                + $"License: {package.LicenseName}{Environment.NewLine}"
                + $"License URL: {package.LicenseUrl}{Environment.NewLine}";
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }

            return builder.ToString();
        }

        private static void DeleteDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Directory.Exists(path) is false)
            {
                return;
            }

            Directory.Delete(path, true);
        }
    }
}
