using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.MediaEditor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using IOFile = System.IO.File;

namespace GameDeveloperKit.Tests
{
    public sealed class FfmpegToolchainInstallerTests
    {
        private string m_Root;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(Path.GetTempPath(), "gdk-ffmpeg-installer-" + Guid.NewGuid().ToString("N"));
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

        [UnityTest]
        public IEnumerator InstallAsync_WhenArchiveIsValid_CommitsManagedTools()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var archivePath = CreateArchive(false);
                var package = CreatePackage(archivePath, ComputeSha256(archivePath));
                var installer = CreateInstaller(package, archivePath, new AcceptingValidator());

                var status = await installer.InstallAsync(null, CancellationToken.None);

                Assert.AreEqual(FfmpegToolchainState.Ready, status.State);
                Assert.AreEqual(FfmpegToolchainSource.Managed, status.Source);
                Assert.IsTrue(IOFile.Exists(status.FfmpegPath));
                Assert.IsTrue(IOFile.Exists(status.FfprobePath));
                Assert.IsTrue(IOFile.Exists(Path.Combine(Path.GetDirectoryName(status.FfmpegPath), "NOTICE.txt")));
            });
        }

        [UnityTest]
        public IEnumerator InstallAsync_WhenHashDoesNotMatch_DoesNotCommitTools()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var archivePath = CreateArchive(false);
                var package = CreatePackage(archivePath, new string('0', 64));
                var installer = CreateInstaller(package, archivePath, new AcceptingValidator());
                InvalidDataException failure = null;

                try
                {
                    await installer.InstallAsync(null, CancellationToken.None);
                }
                catch (InvalidDataException exception)
                {
                    failure = exception;
                }

                Assert.NotNull(failure);
                Assert.AreEqual(FfmpegToolchainState.Missing, installer.Detect().State);
                AssertWorkingDirectoryIsEmpty();
            });
        }

        [Test]
        public void ExtractTools_WhenArchiveEscapesRoot_RejectsBeforeExtraction()
        {
            var archivePath = CreateArchive(true);
            var package = CreatePackage(archivePath, ComputeSha256(archivePath));
            var extract = Path.Combine(m_Root, "extract");
            var installed = Path.Combine(m_Root, "installed");

            var exception = Assert.Throws<InvalidDataException>(() =>
                FfmpegToolchainInstaller.ExtractTools(
                    archivePath,
                    extract,
                    installed,
                    package,
                    null,
                    CancellationToken.None));

            StringAssert.Contains("路径逃逸", exception.Message);
            Assert.IsFalse(IOFile.Exists(Path.Combine(m_Root, "escape.txt")));
        }

        [UnityTest]
        public IEnumerator InstallAsync_WhenValidationFails_PreservesPreviousManagedVersion()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var archivePath = CreateArchive(false);
                var package = CreatePackage(archivePath, ComputeSha256(archivePath));
                var resolver = CreateResolver(package);
                var destination = resolver.GetManagedToolDirectory(package);
                Directory.CreateDirectory(destination);
                IOFile.WriteAllText(Path.Combine(destination, "ffmpeg.exe"), "old-ffmpeg");
                IOFile.WriteAllText(Path.Combine(destination, "ffprobe.exe"), "old-ffprobe");
                var installer = new FfmpegToolchainInstaller(
                    resolver,
                    new CopyDownload(archivePath),
                    new FailingValidator(),
                    package);
                InvalidOperationException failure = null;

                try
                {
                    await installer.InstallAsync(null, CancellationToken.None);
                }
                catch (InvalidOperationException exception)
                {
                    failure = exception;
                }

                Assert.NotNull(failure);
                Assert.AreEqual("old-ffmpeg", IOFile.ReadAllText(Path.Combine(destination, "ffmpeg.exe")));
                Assert.AreEqual("old-ffprobe", IOFile.ReadAllText(Path.Combine(destination, "ffprobe.exe")));
                AssertWorkingDirectoryIsEmpty();
            });
        }

        private FfmpegToolchainInstaller CreateInstaller(
            FfmpegToolchainPackage package,
            string archivePath,
            IFfmpegToolchainValidator validator)
        {
            return new FfmpegToolchainInstaller(
                CreateResolver(package),
                new CopyDownload(archivePath),
                validator,
                package);
        }

        private FfmpegToolchainResolver CreateResolver(FfmpegToolchainPackage package)
        {
            Assert.AreEqual(FfmpegToolchainManifest.ManagedVersion, package.Version);
            return new FfmpegToolchainResolver(m_Root, RuntimePlatform.WindowsEditor, true, string.Empty);
        }

        private FfmpegToolchainPackage CreatePackage(string archivePath, string sha256)
        {
            return new FfmpegToolchainPackage(
                FfmpegToolchainManifest.ManagedVersion,
                RuntimePlatform.WindowsEditor,
                true,
                "https://example.com/ffmpeg.zip",
                sha256,
                new FileInfo(archivePath).Length,
                "GNU General Public License v3.0",
                "https://www.gnu.org/licenses/gpl-3.0.html");
        }

        private string CreateArchive(bool includeEscape)
        {
            var path = Path.Combine(m_Root, Guid.NewGuid().ToString("N") + ".zip");
            using (var stream = IOFile.Create(path))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "ffmpeg/bin/ffmpeg.exe", "new-ffmpeg");
                WriteEntry(archive, "ffmpeg/bin/ffprobe.exe", "new-ffprobe");
                if (includeEscape)
                {
                    WriteEntry(archive, "../escape.txt", "escape");
                }
            }

            return path;
        }

        private static void WriteEntry(ZipArchive archive, string name, string content)
        {
            var entry = archive.CreateEntry(name);
            using (var writer = new StreamWriter(entry.Open()))
            {
                writer.Write(content);
            }
        }

        private static string ComputeSha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = IOFile.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private void AssertWorkingDirectoryIsEmpty()
        {
            var path = Path.Combine(m_Root, "Library/GameDeveloperKit/MediaProcessing/Install");
            Assert.IsTrue(Directory.Exists(path) is false || Directory.GetFileSystemEntries(path).Length == 0);
        }

        private sealed class CopyDownload : IFfmpegToolchainDownload
        {
            private readonly string m_Source;

            public CopyDownload(string source)
            {
                m_Source = source;
            }

            public UniTask DownloadAsync(
                FfmpegToolchainPackage package,
                string destinationPath,
                IProgress<ToolchainInstallProgress> progress,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IOFile.Copy(m_Source, destinationPath, true);
                return UniTask.CompletedTask;
            }
        }

        private sealed class AcceptingValidator : IFfmpegToolchainValidator
        {
            public UniTask ValidateAsync(
                string ffmpegPath,
                string ffprobePath,
                CancellationToken cancellationToken)
            {
                Assert.IsTrue(IOFile.Exists(ffmpegPath));
                Assert.IsTrue(IOFile.Exists(ffprobePath));
                return UniTask.CompletedTask;
            }
        }

        private sealed class FailingValidator : IFfmpegToolchainValidator
        {
            public UniTask ValidateAsync(
                string ffmpegPath,
                string ffprobePath,
                CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("validation failed");
            }
        }
    }
}
