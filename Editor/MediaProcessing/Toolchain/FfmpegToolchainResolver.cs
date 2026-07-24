using System;
using System.IO;
using UnityEngine;
using IOFile = System.IO.File;

namespace GameDeveloperKit.MediaEditor
{
    public sealed class FfmpegToolchainResolver
    {
        public const string ManagedToolsRelativePath = "Library/GameDeveloperKit/MediaProcessing/Tools";

        private readonly string m_ProjectRoot;
        private readonly RuntimePlatform m_Platform;
        private readonly bool m_Is64Bit;
        private readonly string m_PathEnvironment;

        internal string ProjectRoot => m_ProjectRoot;

        public FfmpegToolchainResolver()
            : this(
                Directory.GetCurrentDirectory(),
                Application.platform,
                Environment.Is64BitProcess,
                Environment.GetEnvironmentVariable("PATH"))
        {
        }

        internal FfmpegToolchainResolver(
            string projectRoot,
            RuntimePlatform platform,
            bool is64Bit,
            string pathEnvironment)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("Project root cannot be empty.", nameof(projectRoot));
            }

            m_ProjectRoot = Path.GetFullPath(projectRoot);
            m_Platform = platform;
            m_Is64Bit = is64Bit;
            m_PathEnvironment = pathEnvironment ?? string.Empty;
        }

        public FfmpegToolchainStatus Detect(string manualFfmpegPath, string manualFfprobePath)
        {
            if (string.IsNullOrWhiteSpace(manualFfmpegPath) is false ||
                string.IsNullOrWhiteSpace(manualFfprobePath) is false)
            {
                return DetectManual(manualFfmpegPath, manualFfprobePath);
            }

            var hasPackage = FfmpegToolchainManifest.TryGetPackage(m_Platform, m_Is64Bit, out var package);
            if (hasPackage)
            {
                var managed = DetectManaged(package);
                if (managed != null)
                {
                    return managed;
                }
            }

            var fromPath = DetectPath();
            if (fromPath != null)
            {
                return fromPath;
            }

            if (hasPackage)
            {
                return new FfmpegToolchainStatus(
                    FfmpegToolchainState.Missing,
                    FfmpegToolchainSource.None,
                    string.Empty,
                    string.Empty,
                    "未检测到 FFmpeg，可安装项目托管版本。",
                    package);
            }

            return new FfmpegToolchainStatus(
                FfmpegToolchainState.UnsupportedPlatform,
                FfmpegToolchainSource.None,
                string.Empty,
                string.Empty,
                "当前 Editor 平台没有托管安装包，请在全局设置中配置 ffmpeg 和 ffprobe。",
                null);
        }

        public string GetManagedToolDirectory(FfmpegToolchainPackage package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            return Path.Combine(m_ProjectRoot, ManagedToolsRelativePath, package.Version);
        }

        private FfmpegToolchainStatus DetectManual(string ffmpegPath, string ffprobePath)
        {
            var resolvedFfmpeg = ResolveConfiguredPath(ffmpegPath);
            var resolvedFfprobe = ResolveConfiguredPath(ffprobePath);
            if (IOFile.Exists(resolvedFfmpeg) && IOFile.Exists(resolvedFfprobe))
            {
                return Ready(FfmpegToolchainSource.Manual, resolvedFfmpeg, resolvedFfprobe);
            }

            return new FfmpegToolchainStatus(
                FfmpegToolchainState.InvalidManualConfiguration,
                FfmpegToolchainSource.Manual,
                resolvedFfmpeg,
                resolvedFfprobe,
                "手动配置的 ffmpeg 或 ffprobe 不存在，请修正路径或清空两个覆盖项。",
                null);
        }

        private FfmpegToolchainStatus DetectManaged(FfmpegToolchainPackage package)
        {
            var directory = GetManagedToolDirectory(package);
            var ffmpegPath = Path.Combine(directory, ExecutableName("ffmpeg"));
            var ffprobePath = Path.Combine(directory, ExecutableName("ffprobe"));
            var hasFfmpeg = IOFile.Exists(ffmpegPath);
            var hasFfprobe = IOFile.Exists(ffprobePath);
            if (hasFfmpeg && hasFfprobe)
            {
                return Ready(FfmpegToolchainSource.Managed, ffmpegPath, ffprobePath);
            }

            if (Directory.Exists(directory) || hasFfmpeg || hasFfprobe)
            {
                return new FfmpegToolchainStatus(
                    FfmpegToolchainState.InvalidManagedInstallation,
                    FfmpegToolchainSource.Managed,
                    ffmpegPath,
                    ffprobePath,
                    "项目托管 FFmpeg 不完整，请重新安装。",
                    package);
            }

            return null;
        }

        private FfmpegToolchainStatus DetectPath()
        {
            var ffmpegPath = FindOnPath(ExecutableName("ffmpeg"));
            var ffprobePath = FindOnPath(ExecutableName("ffprobe"));
            return ffmpegPath != null && ffprobePath != null
                ? Ready(FfmpegToolchainSource.Path, ffmpegPath, ffprobePath)
                : null;
        }

        private FfmpegToolchainStatus Ready(
            FfmpegToolchainSource source,
            string ffmpegPath,
            string ffprobePath)
        {
            return new FfmpegToolchainStatus(
                FfmpegToolchainState.Ready,
                source,
                Normalize(ffmpegPath),
                Normalize(ffprobePath),
                $"FFmpeg 工具链已就绪（{source}）。",
                null);
        }

        private string ResolveConfiguredPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim().Replace('/', Path.DirectorySeparatorChar);
            return Normalize(Path.IsPathRooted(trimmed)
                ? Path.GetFullPath(trimmed)
                : Path.GetFullPath(Path.Combine(m_ProjectRoot, trimmed)));
        }

        private string FindOnPath(string executableName)
        {
            var directories = m_PathEnvironment.Split(Path.PathSeparator);
            for (var i = 0; i < directories.Length; i++)
            {
                var directory = directories[i].Trim().Trim('"');
                if (directory.Length == 0)
                {
                    continue;
                }

                try
                {
                    var candidate = Path.Combine(directory, executableName);
                    if (IOFile.Exists(candidate))
                    {
                        return Normalize(Path.GetFullPath(candidate));
                    }
                }
                catch (Exception exception)
                {
                    // Ignore malformed PATH entries and continue searching valid directories.
                    Debug.LogWarning($"跳过无效的 PATH 目录：{directory}。{exception.Message}");
                }
            }

            return null;
        }

        private string ExecutableName(string command)
        {
            return m_Platform == RuntimePlatform.WindowsEditor ? command + ".exe" : command;
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }
}
