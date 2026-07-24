using System;
using UnityEngine;

namespace GameDeveloperKit.MediaEditor
{
    public enum FfmpegToolchainState
    {
        Ready = 0,
        Missing = 1,
        InvalidManualConfiguration = 2,
        InvalidManagedInstallation = 3,
        UnsupportedPlatform = 4
    }

    public enum FfmpegToolchainSource
    {
        None = 0,
        Manual = 1,
        Managed = 2,
        Path = 3
    }

    public enum ToolchainInstallStage
    {
        Downloading = 0,
        VerifyingArchive = 1,
        Extracting = 2,
        VerifyingTools = 3,
        Committing = 4,
        Completed = 5
    }

    public readonly struct ToolchainInstallProgress
    {
        public ToolchainInstallProgress(
            ToolchainInstallStage stage,
            float progress,
            string message)
        {
            Stage = stage;
            Progress = Mathf.Clamp01(progress);
            Message = message ?? string.Empty;
        }

        public ToolchainInstallStage Stage { get; }
        public float Progress { get; }
        public string Message { get; }
    }

    public sealed class FfmpegToolchainPackage
    {
        public FfmpegToolchainPackage(
            string version,
            RuntimePlatform platform,
            bool requires64Bit,
            string archiveUrl,
            string archiveSha256,
            long archiveSize,
            string licenseName,
            string licenseUrl)
        {
            Version = Require(version, nameof(version));
            Platform = platform;
            Requires64Bit = requires64Bit;
            ArchiveUrl = RequireHttps(archiveUrl, nameof(archiveUrl));
            ArchiveSha256 = RequireSha256(archiveSha256);
            ArchiveSize = archiveSize > 0
                ? archiveSize
                : throw new ArgumentOutOfRangeException(nameof(archiveSize));
            LicenseName = Require(licenseName, nameof(licenseName));
            LicenseUrl = RequireHttps(licenseUrl, nameof(licenseUrl));
        }

        public string Version { get; }
        public RuntimePlatform Platform { get; }
        public bool Requires64Bit { get; }
        public string ArchiveUrl { get; }
        public string ArchiveSha256 { get; }
        public long ArchiveSize { get; }
        public string LicenseName { get; }
        public string LicenseUrl { get; }

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }

            return value.Trim();
        }

        private static string RequireHttps(string value, string parameterName)
        {
            value = Require(value, parameterName);
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) is false ||
                string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) is false)
            {
                throw new ArgumentException("Value must be an absolute HTTPS URL.", parameterName);
            }

            return uri.AbsoluteUri;
        }

        private static string RequireSha256(string value)
        {
            value = Require(value, nameof(value)).ToLowerInvariant();
            if (value.Length != 64)
            {
                throw new ArgumentException("SHA-256 must contain 64 hexadecimal characters.", nameof(value));
            }

            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if ((character < '0' || character > '9') && (character < 'a' || character > 'f'))
                {
                    throw new ArgumentException("SHA-256 contains a non-hexadecimal character.", nameof(value));
                }
            }

            return value;
        }
    }

    public sealed class FfmpegToolchainStatus
    {
        internal FfmpegToolchainStatus(
            FfmpegToolchainState state,
            FfmpegToolchainSource source,
            string ffmpegPath,
            string ffprobePath,
            string message,
            FfmpegToolchainPackage package)
        {
            State = state;
            Source = source;
            FfmpegPath = ffmpegPath ?? string.Empty;
            FfprobePath = ffprobePath ?? string.Empty;
            Message = message ?? string.Empty;
            Package = package;
        }

        public FfmpegToolchainState State { get; }
        public FfmpegToolchainSource Source { get; }
        public string FfmpegPath { get; }
        public string FfprobePath { get; }
        public string Message { get; }
        public FfmpegToolchainPackage Package { get; }
        public bool IsReady => State == FfmpegToolchainState.Ready;
        public bool CanInstall => Package != null &&
                                  (State == FfmpegToolchainState.Missing ||
                                   State == FfmpegToolchainState.InvalidManagedInstallation);
    }

    public static class FfmpegToolchainManifest
    {
        public const string ManagedVersion = "N-125748-g80eb9e99b9";

        private static readonly FfmpegToolchainPackage s_WindowsX64 = new FfmpegToolchainPackage(
            ManagedVersion,
            RuntimePlatform.WindowsEditor,
            true,
            "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-07-23-14-16/ffmpeg-N-125748-g80eb9e99b9-win64-gpl.zip",
            "de09059774945893dfe6a2a35a952d31f4d5dcebf4f27c050ee476f04241d358",
            168733461,
            "GNU General Public License v3.0",
            "https://www.gnu.org/licenses/gpl-3.0.html");

        public static bool TryGetPackage(
            RuntimePlatform platform,
            bool is64Bit,
            out FfmpegToolchainPackage package)
        {
            if (platform == RuntimePlatform.WindowsEditor && is64Bit)
            {
                package = s_WindowsX64;
                return true;
            }

            package = null;
            return false;
        }
    }
}
