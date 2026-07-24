using System;
using System.IO;
using GameDeveloperKit.MediaEditor;
using NUnit.Framework;
using UnityEngine;
using IOFile = System.IO.File;

namespace GameDeveloperKit.Tests
{
    public sealed class FfmpegToolchainResolverTests
    {
        private string m_Root;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(Path.GetTempPath(), "gdk-ffmpeg-resolver-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_Root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_Root))
            {
                Directory.Delete(m_Root, true);
            }
        }

        [Test]
        public void Manifest_WindowsX64_IsPinnedAndVerifiable()
        {
            Assert.IsTrue(FfmpegToolchainManifest.TryGetPackage(
                RuntimePlatform.WindowsEditor,
                true,
                out var package));

            Assert.AreEqual(FfmpegToolchainManifest.ManagedVersion, package.Version);
            StringAssert.StartsWith("https://", package.ArchiveUrl);
            Assert.AreEqual(64, package.ArchiveSha256.Length);
            Assert.Greater(package.ArchiveSize, 0);
            StringAssert.Contains("License", package.LicenseName);
        }

        [Test]
        public void Detect_WhenWindowsToolchainIsMissing_OffersManagedInstall()
        {
            var resolver = CreateResolver(RuntimePlatform.WindowsEditor, true, string.Empty);

            var status = resolver.Detect(string.Empty, string.Empty);

            Assert.AreEqual(FfmpegToolchainState.Missing, status.State);
            Assert.IsTrue(status.CanInstall);
            Assert.NotNull(status.Package);
        }

        [Test]
        public void Detect_WhenPlatformHasNoPackage_RequiresManualConfiguration()
        {
            var resolver = CreateResolver(RuntimePlatform.OSXEditor, true, string.Empty);

            var status = resolver.Detect(string.Empty, string.Empty);

            Assert.AreEqual(FfmpegToolchainState.UnsupportedPlatform, status.State);
            Assert.IsFalse(status.CanInstall);
        }

        [Test]
        public void Detect_WhenManualPathsAreInvalid_DoesNotFallBackToPath()
        {
            var pathRoot = Path.Combine(m_Root, "path");
            CreateExecutablePair(pathRoot, true);
            var resolver = CreateResolver(RuntimePlatform.WindowsEditor, true, pathRoot);

            var status = resolver.Detect("missing/ffmpeg.exe", "missing/ffprobe.exe");

            Assert.AreEqual(FfmpegToolchainState.InvalidManualConfiguration, status.State);
            Assert.AreEqual(FfmpegToolchainSource.Manual, status.Source);
        }

        [Test]
        public void Detect_WhenManualPathsExist_SelectsManualSource()
        {
            var manualRoot = Path.Combine(m_Root, "manual");
            CreateExecutablePair(manualRoot, true);
            var resolver = CreateResolver(RuntimePlatform.WindowsEditor, true, string.Empty);

            var status = resolver.Detect(
                Path.Combine(manualRoot, "ffmpeg.exe"),
                Path.Combine(manualRoot, "ffprobe.exe"));

            Assert.AreEqual(FfmpegToolchainState.Ready, status.State);
            Assert.AreEqual(FfmpegToolchainSource.Manual, status.Source);
        }

        [Test]
        public void Detect_WhenManagedInstallationExists_PrefersItOverPath()
        {
            var resolver = CreateResolver(RuntimePlatform.WindowsEditor, true, Path.Combine(m_Root, "path"));
            Assert.IsTrue(FfmpegToolchainManifest.TryGetPackage(
                RuntimePlatform.WindowsEditor,
                true,
                out var package));
            CreateExecutablePair(resolver.GetManagedToolDirectory(package), true);
            CreateExecutablePair(Path.Combine(m_Root, "path"), true);

            var status = resolver.Detect(string.Empty, string.Empty);

            Assert.AreEqual(FfmpegToolchainState.Ready, status.State);
            Assert.AreEqual(FfmpegToolchainSource.Managed, status.Source);
        }

        [Test]
        public void Detect_WhenManagedInstallationIsIncomplete_ReportsDamage()
        {
            var resolver = CreateResolver(RuntimePlatform.WindowsEditor, true, string.Empty);
            Assert.IsTrue(FfmpegToolchainManifest.TryGetPackage(
                RuntimePlatform.WindowsEditor,
                true,
                out var package));
            var managedRoot = resolver.GetManagedToolDirectory(package);
            Directory.CreateDirectory(managedRoot);
            IOFile.WriteAllText(Path.Combine(managedRoot, "ffmpeg.exe"), string.Empty);

            var status = resolver.Detect(string.Empty, string.Empty);

            Assert.AreEqual(FfmpegToolchainState.InvalidManagedInstallation, status.State);
            Assert.AreEqual(FfmpegToolchainSource.Managed, status.Source);
            Assert.IsTrue(status.CanInstall);
        }

        private FfmpegToolchainResolver CreateResolver(
            RuntimePlatform platform,
            bool is64Bit,
            string pathEnvironment)
        {
            return new FfmpegToolchainResolver(m_Root, platform, is64Bit, pathEnvironment);
        }

        private static void CreateExecutablePair(string directory, bool windows)
        {
            Directory.CreateDirectory(directory);
            var extension = windows ? ".exe" : string.Empty;
            IOFile.WriteAllText(Path.Combine(directory, "ffmpeg" + extension), string.Empty);
            IOFile.WriteAllText(Path.Combine(directory, "ffprobe" + extension), string.Empty);
        }
    }
}
