using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.File;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class FileModuleTests : RuntimeTestBase
    {
        private FileModule m_Module;
        private string m_RootPath;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                if (m_Module != null)
                {
                    await ((IAsyncShutdownParticipant)m_Module).PrepareShutdownAsync();
                    m_Module.Shutdown();
                    m_Module = null;
                }

                try
                {
                    await App.Unregister<FileModule>();
                }
                catch (GameException)
                {
                }

                if (!string.IsNullOrEmpty(m_RootPath) && Directory.Exists(m_RootPath))
                {
                    Directory.Delete(m_RootPath, true);
                    m_RootPath = null;
                }
            });
        }

        [UnityTest]
        public IEnumerator Register_WhenFileModuleIsRegistered_ReturnsFile()
        {
            return UniTask.ToCoroutine(() =>
            {
                App.Register<FileModule>();

                Assert.IsNotNull(App.File);
                return UniTask.CompletedTask;
            });
        }

        [UnityTest]
        public IEnumerator WriteReadDelete_WhenDataWritten_RoundTripsThroughVfs()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var path = UniquePath("roundtrip");
                var data = Encoding.UTF8.GetBytes("hello-vfs");

                await module.WriteAsync(path, "1.0.0", data);

                Assert.IsTrue(module.Exists(path));
                Assert.IsTrue(module.Exists(path, "1.0.0"));
                Assert.IsFalse(module.Exists(path, "2.0.0"));
                Assert.IsTrue(module.TryGetFileInfo(path, out var entry));
                Assert.AreEqual(data.Length, entry.Size);

                var read = await module.ReadAsync(path);
                CollectionAssert.AreEqual(data, read);

                await module.DeleteAsync(path);

                Assert.IsFalse(module.Exists(path));
                Assert.IsNull(await module.ReadAsync(path));
                Assert.IsFalse(module.TryGetFileInfo(path, out _));
            });
        }

        [UnityTest]
        public IEnumerator DeleteAsync_WhenLastEntryUsesBundle_RemovesBundleFile()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var path = UniquePath("delete-bundle");

                await module.WriteAsync(path, "1.0.0", Encoding.UTF8.GetBytes("delete-bundle-file"));
                Assert.IsTrue(module.TryGetFileInfo(path, out var entry));

                var bundlePath = GetBundlePath(module, entry.BundlePath);
                Assert.IsTrue(System.IO.File.Exists(bundlePath));

                await module.DeleteAsync(path);

                Assert.IsFalse(System.IO.File.Exists(bundlePath));
                Assert.IsFalse(module.TryGetFileInfo(path, out _));
                Assert.IsFalse(BundlePathExists(module, entry.BundlePath));
                Assert.IsFalse(ManifestBundlePathExists(module, entry.BundlePath));
            });
        }

        [UnityTest]
        public IEnumerator WriteAsync_WhenOverwritingPath_CommitsNewEntryAndSurvivesRestart()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var path = UniquePath("overwrite-bundle");

                await module.WriteAsync(path, "1.0.0", Encoding.UTF8.GetBytes("first"));
                Assert.IsTrue(module.TryGetFileInfo(path, out var firstEntry));
                Assert.IsTrue(System.IO.File.Exists(GetBundlePath(module, firstEntry.BundlePath)));

                await module.WriteAsync(path, "2.0.0", Encoding.UTF8.GetBytes("second"));

                Assert.IsTrue(module.TryGetFileInfo(path, out var secondEntry));
                Assert.AreEqual("2.0.0", secondEntry.Version);
                Assert.IsFalse(string.IsNullOrEmpty(secondEntry.BundlePath));
                Assert.AreNotEqual(firstEntry.Offset, secondEntry.Offset);
                CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("second"), await module.ReadAsync(path));

                module = RestartIsolatedModule(module);
                Assert.IsTrue(module.Exists(path, "2.0.0"));
                CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("second"), await module.ReadAsync(path));
            });
        }

        [Test]
        public void Startup_WhenManifestBundlePathIsNotCanonical_RejectsManifest()
        {
            m_RootPath = Path.Combine(
                UnityEngine.Application.temporaryCachePath,
                "vfs-manifest-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_RootPath);
            WriteManifest(new VFSMeta
            {
                FilePath = "tests/escape.bin",
                BundlePath = "../outside",
                Storage = StorageType.Standalone,
                Offset = 0,
                Size = 1,
                Usegd = true,
            });
            var module = new FileModule(m_RootPath);

            var exception = Assert.Throws<GameException>(() => module.Startup());

            StringAssert.Contains("BundlePath must be a canonical lowercase GUID", exception.Message);
        }

        [Test]
        public void Startup_WhenManifestEntryExceedsBundleLength_RejectsManifest()
        {
            m_RootPath = Path.Combine(
                UnityEngine.Application.temporaryCachePath,
                "vfs-manifest-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_RootPath);
            var bundlePath = Guid.NewGuid().ToString("N");
            System.IO.File.WriteAllBytes(Path.Combine(m_RootPath, bundlePath), new byte[4]);
            WriteManifest(new VFSMeta
            {
                FilePath = "tests/out-of-range.bin",
                BundlePath = bundlePath,
                Storage = StorageType.Standalone,
                Offset = 0,
                Size = 5,
                Usegd = true,
            });
            var module = new FileModule(m_RootPath);

            var exception = Assert.Throws<GameException>(() => module.Startup());

            StringAssert.Contains("exceeds bundle length", exception.Message);
        }

        [UnityTest]
        public IEnumerator WriteStreamAsync_WhenSourceIsLargeAndNonSeekable_RoundTripsThroughBoundedStream()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var path = UniquePath("stream-roundtrip");
                var expected = new byte[VfsConstants.DefaultThreshold * 512];
                for (var index = 0; index < expected.Length; index++)
                {
                    expected[index] = (byte)(index * 31);
                }

                using (var source = new NonSeekableReadStream(expected))
                {
                    await module.WriteAsync(path, "stream-v1", source);
                }

                Assert.IsTrue(module.TryGetFileInfo(path, out var entry));
                Assert.AreEqual(StorageType.Standalone, entry.Storage);
                Assert.AreEqual(expected.Length, entry.Size);

                using (var stream = await module.OpenReadAsync(path, "stream-v1"))
                {
                    Assert.IsNotNull(stream);
                    Assert.AreEqual(expected.Length, stream.Length);
                    var buffer = new byte[32768];
                    var offset = 0;
                    while (offset < expected.Length)
                    {
                        var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                        Assert.Greater(read, 0);
                        for (var byteIndex = 0; byteIndex < read; byteIndex++)
                        {
                            Assert.AreEqual(expected[offset + byteIndex], buffer[byteIndex]);
                        }

                        offset += read;
                    }

                    Assert.AreEqual(0, await stream.ReadAsync(buffer, 0, buffer.Length));
                }
            });
        }

        [UnityTest]
        public IEnumerator OpenReadAsync_WhenPackedEntriesShareBundle_DoesNotCrossEntryBoundary()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var firstPath = UniquePath("packed-first");
                var secondPath = UniquePath("packed-second");
                var first = Encoding.UTF8.GetBytes("first-entry");
                var second = Encoding.UTF8.GetBytes("second-entry");
                await module.WriteAsync(firstPath, "1", first);
                await module.WriteAsync(secondPath, "1", second);

                Assert.IsTrue(module.TryGetFileInfo(firstPath, out var firstEntry));
                Assert.IsTrue(module.TryGetFileInfo(secondPath, out var secondEntry));
                Assert.AreEqual(firstEntry.BundlePath, secondEntry.BundlePath);

                using (var stream = await module.OpenReadAsync(firstPath))
                {
                    var buffer = new byte[first.Length + second.Length + 16];
                    var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    Assert.AreEqual(first.Length, read);
                    CollectionAssert.AreEqual(first, new ArraySegment<byte>(buffer, 0, read));
                    Assert.AreEqual(0, await stream.ReadAsync(buffer, 0, buffer.Length));
                    Assert.Throws<IOException>(() => stream.Seek(1, SeekOrigin.End));
                }
            });
        }

        [UnityTest]
        public IEnumerator WriteStreamAsync_WhenSourceThrows_PreservesPreviousVersion()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var path = UniquePath("stream-failure");
                var previous = Encoding.UTF8.GetBytes("previous-version");
                await module.WriteAsync(path, "v1", previous);

                using (var source = new ThrowingReadStream(new byte[VfsConstants.DefaultThreshold * 2]))
                {
                    await ThrowsAsync<IOException>(async () =>
                    {
                        await module.WriteAsync(path, "v2", source);
                    });
                }

                Assert.IsTrue(module.Exists(path, "v1"));
                Assert.IsFalse(module.Exists(path, "v2"));
                CollectionAssert.AreEqual(previous, await module.ReadAsync(path));
            });
        }

        [UnityTest]
        public IEnumerator PrepareShutdown_WhenReadStreamIsOpen_ClosesLeaseBeforeShutdown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var path = UniquePath("open-read-shutdown");
                await module.WriteAsync(path, "1", Encoding.UTF8.GetBytes("lease"));
                var stream = await module.OpenReadAsync(path);

                await ((IAsyncShutdownParticipant)module).PrepareShutdownAsync();

                Assert.IsFalse(stream.CanRead);
                Assert.Throws<ObjectDisposedException>(() => stream.ReadByte());
                module.Shutdown();
                m_Module = null;
            });
        }

        [UnityTest]
        public IEnumerator SourceReadStreams_ExternalAndPackaged_AreTrackedUntilShutdown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var externalPath = Path.Combine(module.RootPath, "external-source.bin");
                var packagedRelativePath = $"gdk-tests/{Guid.NewGuid():N}.bin";
                var packagedPath = Path.Combine(
                    UnityEngine.Application.streamingAssetsPath,
                    packagedRelativePath);
                var packagedDirectory = Path.GetDirectoryName(packagedPath);
                var externalData = Encoding.UTF8.GetBytes("external");
                var packagedData = Encoding.UTF8.GetBytes("packaged");
                System.IO.File.WriteAllBytes(externalPath, externalData);
                Directory.CreateDirectory(packagedDirectory);
                System.IO.File.WriteAllBytes(packagedPath, packagedData);

                try
                {
                    var external = await module.OpenExternalReadAsync(externalPath);
                    var packaged = await module.OpenPackagedReadAsync(packagedRelativePath);
                    Assert.IsTrue(module.TryReadPackagedBytes(packagedRelativePath, out var packagedBytes));
                    CollectionAssert.AreEqual(packagedData, packagedBytes);
                    CollectionAssert.AreEqual(externalData, await ReadStreamAsync(external));
                    external.Position = 0;
                    CollectionAssert.AreEqual(packagedData, await ReadStreamAsync(packaged));
                    packaged.Position = 0;

                    await ((IAsyncShutdownParticipant)module).PrepareShutdownAsync();

                    Assert.IsFalse(external.CanRead);
                    Assert.IsFalse(packaged.CanRead);
                    module.Shutdown();
                    m_Module = null;
                }
                finally
                {
                    if (System.IO.File.Exists(packagedPath))
                    {
                        System.IO.File.Delete(packagedPath);
                    }

                    if (!string.IsNullOrEmpty(packagedDirectory) &&
                        Directory.Exists(packagedDirectory) &&
                        Directory.GetFileSystemEntries(packagedDirectory).Length == 0)
                    {
                        Directory.Delete(packagedDirectory);
                    }
                }
            });
        }

        [UnityTest]
        public IEnumerator TemporaryHandle_WriteReadDeleteAndShutdownCleanup_OwnsPhysicalLifetime()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var handle = module.CreateTemporaryFile("tests", "lifetime");
                var nativePath = handle.NativePath;
                var data = Encoding.UTF8.GetBytes("temporary-payload");

                await WriteTemporaryAsync(handle, data);
                Assert.IsTrue(handle.Exists);
                Assert.AreEqual(data.Length, handle.Length);
                using (var stream = await handle.OpenReadAsync())
                {
                    var actual = new byte[data.Length];
                    Assert.AreEqual(data.Length, await stream.ReadAsync(actual, 0, actual.Length));
                    CollectionAssert.AreEqual(data, actual);
                }

                await handle.DeleteAsync();
                Assert.IsFalse(handle.Exists);
                await WriteTemporaryAsync(handle, data);

                await ((IAsyncShutdownParticipant)module).PrepareShutdownAsync();

                Assert.IsFalse(System.IO.File.Exists(nativePath));
                Assert.Throws<ObjectDisposedException>(() => _ = handle.Exists);
                module.Shutdown();
                m_Module = null;
            });
        }

        [UnityTest]
        public IEnumerator PrepareShutdown_WhenTemporaryDeleteFails_CanRetryAfterFileIsReleased()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var handle = module.CreateTemporaryFile("tests", "retry-cleanup");
                await WriteTemporaryAsync(handle, Encoding.UTF8.GetBytes("locked"));
                var nativePath = handle.NativePath;

                using (new FileStream(nativePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    await ThrowsAsync<AggregateException>(async () =>
                    {
                        await ((IAsyncShutdownParticipant)module).PrepareShutdownAsync();
                    });

                    Assert.IsTrue(handle.Exists);
                }

                await ((IAsyncShutdownParticipant)module).PrepareShutdownAsync();

                Assert.IsFalse(System.IO.File.Exists(nativePath));
                Assert.Throws<ObjectDisposedException>(() => _ = handle.Exists);
                module.Shutdown();
                m_Module = null;
            });
        }

        [UnityTest]
        public IEnumerator TemporaryHandle_MergeFromAsync_ConcatenatesAndDeletesParts()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var destination = module.CreateTemporaryFile("tests", "merged");
                var first = module.CreateTemporaryFile("tests", "part-0");
                var second = module.CreateTemporaryFile("tests", "part-1");
                var firstData = Encoding.UTF8.GetBytes("first-");
                var secondData = Encoding.UTF8.GetBytes("second");
                await WriteTemporaryAsync(first, firstData);
                await WriteTemporaryAsync(second, secondData);

                await destination.MergeFromAsync(
                    new[]
                    {
                        (first, (long)firstData.Length),
                        (second, (long)secondData.Length)
                    },
                    CancellationToken.None);

                Assert.IsFalse(first.Exists);
                Assert.IsFalse(second.Exists);
                using (var stream = await destination.OpenReadAsync())
                using (var memory = new MemoryStream())
                {
                    await stream.CopyToAsync(memory);
                    CollectionAssert.AreEqual(
                        Encoding.UTF8.GetBytes("first-second"),
                        memory.ToArray());
                }

                await first.ReleaseAsync();
                await second.ReleaseAsync();
                await destination.ReleaseAsync();
            });
        }

        [UnityTest]
        public IEnumerator TemporaryHandle_MergeFromAsync_WhenPartIsShort_ThrowsAndRetainsPart()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var destination = module.CreateTemporaryFile("tests", "short-merge");
                var part = module.CreateTemporaryFile("tests", "short-part");
                var data = new byte[] { 1, 2, 3 };
                await WriteTemporaryAsync(part, data);

                await ThrowsAsync<EndOfStreamException>(async () =>
                {
                    await destination.MergeFromAsync(
                        new[] { (part, 4L) },
                        CancellationToken.None);
                });

                Assert.IsTrue(part.Exists);
                CollectionAssert.AreEqual(data, await ReadTemporaryAsync(part));
                await part.ReleaseAsync();
                await destination.ReleaseAsync();
            });
        }

        [UnityTest]
        public IEnumerator ImportTemporaryAsync_VerifiesBeforePublishingCandidate()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var path = UniquePath("verified-import");
                var previous = Encoding.UTF8.GetBytes("previous");
                var replacement = Encoding.UTF8.GetBytes("replacement");
                await module.WriteAsync(path, "v1", previous);
                var source = module.CreateTemporaryFile("tests", "verified-source");
                await WriteTemporaryAsync(source, replacement);

                await module.ImportTemporaryAsync(source, path, "v2", async stream =>
                {
                    Assert.IsTrue(module.Exists(path, "v1"));
                    Assert.IsFalse(module.Exists(path, "v2"));
                    var actual = new byte[replacement.Length];
                    Assert.AreEqual(actual.Length, await stream.ReadAsync(actual, 0, actual.Length));
                    CollectionAssert.AreEqual(replacement, actual);
                });

                Assert.IsTrue(module.Exists(path, "v2"));
                Assert.IsTrue(source.Exists);
                CollectionAssert.AreEqual(replacement, await module.ReadAsync(path));
                await source.ReleaseAsync();
            });
        }

        [UnityTest]
        public IEnumerator ImportTemporaryAsync_WhenVerifierFails_PreservesPreviousVersionAndSource()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var path = UniquePath("rejected-import");
                var previous = Encoding.UTF8.GetBytes("previous");
                await module.WriteAsync(path, "v1", previous);
                var filesBeforeFailure = Directory.GetFiles(module.RootPath);
                var source = module.CreateTemporaryFile("tests", "rejected-source");
                await WriteTemporaryAsync(source, Encoding.UTF8.GetBytes("rejected"));

                await ThrowsAsync<InvalidDataException>(async () =>
                {
                    await module.ImportTemporaryAsync(source, path, "v2", stream =>
                    {
                        throw new InvalidDataException("Injected verifier failure.");
                    });
                });

                Assert.IsTrue(module.Exists(path, "v1"));
                Assert.IsFalse(module.Exists(path, "v2"));
                Assert.IsTrue(source.Exists);
                CollectionAssert.AreEqual(previous, await module.ReadAsync(path));
                CollectionAssert.AreEquivalent(filesBeforeFailure, Directory.GetFiles(module.RootPath));
                await source.ReleaseAsync();
            });
        }

        [UnityTest]
        public IEnumerator ImportTemporaryAsync_WhenManifestCommitFails_PreservesPreviousVersionAndSource()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var path = UniquePath("failed-import-commit");
                var previous = Encoding.UTF8.GetBytes("previous");
                await module.WriteAsync(path, "v1", previous);
                var filesBeforeFailure = Directory.GetFiles(module.RootPath);
                var source = module.CreateTemporaryFile("tests", "failed-commit-source");
                await WriteTemporaryAsync(source, Encoding.UTF8.GetBytes("candidate"));
                var manifestPath = Path.Combine(module.RootPath, VfsConstants.ManifestFileName);

                using (new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    await ThrowsAsync<IOException>(async () =>
                    {
                        await module.ImportTemporaryAsync(
                            source,
                            path,
                            "v2",
                            stream => UniTask.CompletedTask);
                    });
                }

                Assert.IsTrue(module.Exists(path, "v1"));
                Assert.IsFalse(module.Exists(path, "v2"));
                Assert.IsTrue(source.Exists);
                CollectionAssert.AreEqual(previous, await module.ReadAsync(path));
                CollectionAssert.AreEquivalent(filesBeforeFailure, Directory.GetFiles(module.RootPath));
                await source.ReleaseAsync();
            });
        }

        [UnityTest]
        public IEnumerator WriteAsync_WhenManifestCommitFails_PreservesPreviousVersionAndData()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var path = UniquePath("failed-overwrite");
                var previousData = Encoding.UTF8.GetBytes("last-known-good");
                await module.WriteAsync(path, "1.0.0", previousData);
                var filesBeforeFailure = Directory.GetFiles(module.RootPath);

                var manifestPath = Path.Combine(module.RootPath, VfsConstants.ManifestFileName);
                using (new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    await ThrowsAsync<IOException>(async () =>
                    {
                        await module.WriteAsync(path, "2.0.0", new byte[VfsConstants.DefaultThreshold * 2]);
                    });
                }

                Assert.IsTrue(module.Exists(path, "1.0.0"));
                Assert.IsFalse(module.Exists(path, "2.0.0"));
                CollectionAssert.AreEqual(previousData, await module.ReadAsync(path));
                Assert.AreEqual(0, Directory.GetFiles(module.RootPath, ".vfs_manifest.*.tmp").Length);
                CollectionAssert.AreEquivalent(filesBeforeFailure, Directory.GetFiles(module.RootPath));

                module = RestartIsolatedModule(module);
                Assert.IsTrue(module.Exists(path, "1.0.0"));
                CollectionAssert.AreEqual(previousData, await module.ReadAsync(path));
            });
        }

        [UnityTest]
        public IEnumerator WriteAsync_WhenMutationsRunConcurrently_CommitsDistinctEntriesAndSurvivesRestart()
        {
            return UniTask.ToCoroutine(async () =>
            {
                const int fileCount = 16;
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var paths = new string[fileCount];
                var expected = new byte[fileCount][];
                var writes = new UniTask[fileCount];
                for (var index = 0; index < fileCount; index++)
                {
                    paths[index] = UniquePath($"concurrent-{index}");
                    expected[index] = Encoding.UTF8.GetBytes($"payload-{index}");
                    writes[index] = module.WriteAsync(paths[index], "1.0.0", expected[index]);
                }

                await UniTask.WhenAll(writes);
                Assert.AreEqual(fileCount, CountFiles(module));

                module = RestartIsolatedModule(module);
                Assert.AreEqual(fileCount, CountFiles(module));
                for (var index = 0; index < fileCount; index++)
                {
                    CollectionAssert.AreEqual(expected[index], await module.ReadAsync(paths[index]));
                }
            });
        }

        [UnityTest]
        public IEnumerator Startup_WhenUncommittedBundleRemains_RemovesOrphanAndKeepsCommittedData()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var path = UniquePath("orphan-recovery");
                var data = Encoding.UTF8.GetBytes("committed");
                await module.WriteAsync(path, "1.0.0", data);
                var orphanPath = Path.Combine(module.RootPath, Guid.NewGuid().ToString("N"));
                System.IO.File.WriteAllBytes(orphanPath, new byte[] { 1, 2, 3 });

                module = RestartIsolatedModule(module);

                Assert.IsFalse(System.IO.File.Exists(orphanPath));
                CollectionAssert.AreEqual(data, await module.ReadAsync(path));
            });
        }

        [UnityTest]
        public IEnumerator PrepareShutdown_WhenWriteIsPending_WaitsAndRejectsNewIoBeforeShutdown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var mutationGate = GetField<SemaphoreSlim>(module, "m_MutationGate");
                var gateHeld = true;
                mutationGate.Wait();
                try
                {
                    var path = UniquePath("shutdown-pending-write");
                    var data = Encoding.UTF8.GetBytes("committed-before-shutdown");
                    var writeTask = module.WriteAsync(path, "1.0.0", data);
                    await UniTask.Yield();
                    var prepareTask = ((IAsyncShutdownParticipant)module).PrepareShutdownAsync();
                    await UniTask.Yield();

                    Assert.AreEqual(UniTaskStatus.Pending, prepareTask.Status);
                    Assert.Throws<GameException>(() => module.ReadAsync(path).GetAwaiter().GetResult());

                    mutationGate.Release();
                    gateHeld = false;
                    await writeTask;
                    await prepareTask;
                    module.Shutdown();

                    var restarted = new FileModule(m_RootPath);
                    restarted.Startup();
                    m_Module = restarted;
                    CollectionAssert.AreEqual(data, await restarted.ReadAsync(path));
                }
                finally
                {
                    if (gateHeld)
                    {
                        mutationGate.Release();
                    }
                }
            });
        }

        [UnityTest]
        public IEnumerator MoveToAsync_WhenSourceExists_CommitsVirtualRenameAndSurvivesRestart()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var sourcePath = UniquePath("move-source");
                var destinationPath = UniquePath("move-destination");
                var data = Encoding.UTF8.GetBytes("move-payload");
                await module.WriteAsync(sourcePath, "1.0.0", data);
                Assert.IsTrue(module.TryGetFileInfo(sourcePath, out var sourceEntry));

                await module.MoveToAsync(sourcePath, destinationPath);

                Assert.IsFalse(module.Exists(sourcePath));
                Assert.IsTrue(module.TryGetFileInfo(destinationPath, out var destinationEntry));
                Assert.AreEqual(sourceEntry.BundlePath, destinationEntry.BundlePath);
                Assert.AreEqual(sourceEntry.Offset, destinationEntry.Offset);
                CollectionAssert.AreEqual(data, await module.ReadAsync(destinationPath));

                module = RestartIsolatedModule(module);
                Assert.IsFalse(module.Exists(sourcePath));
                Assert.IsTrue(module.Exists(destinationPath, "1.0.0"));
                CollectionAssert.AreEqual(data, await module.ReadAsync(destinationPath));
            });
        }

        [UnityTest]
        public IEnumerator MoveToAsync_WhenManifestCommitFails_PreservesSourcePath()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var sourcePath = UniquePath("failed-move-source");
                var destinationPath = UniquePath("failed-move-destination");
                var data = Encoding.UTF8.GetBytes("move-last-known-good");
                await module.WriteAsync(sourcePath, "1.0.0", data);

                var manifestPath = Path.Combine(module.RootPath, VfsConstants.ManifestFileName);
                using (new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    await ThrowsAsync<IOException>(async () =>
                    {
                        await module.MoveToAsync(sourcePath, destinationPath);
                    });
                }

                Assert.IsTrue(module.Exists(sourcePath));
                Assert.IsFalse(module.Exists(destinationPath));
                CollectionAssert.AreEqual(data, await module.ReadAsync(sourcePath));
            });
        }

        [UnityTest]
        public IEnumerator MoveToAsync_WhenDestinationExists_RejectsRenameAndPreservesBothFiles()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;
                var sourcePath = UniquePath("move-conflict-source");
                var destinationPath = UniquePath("move-conflict-destination");
                var sourceData = Encoding.UTF8.GetBytes("source");
                var destinationData = Encoding.UTF8.GetBytes("destination");
                await module.WriteAsync(sourcePath, "1.0.0", sourceData);
                await module.WriteAsync(destinationPath, "1.0.0", destinationData);

                await ThrowsAsync<IOException>(async () =>
                {
                    await module.MoveToAsync(sourcePath, destinationPath);
                });

                CollectionAssert.AreEqual(sourceData, await module.ReadAsync(sourcePath));
                CollectionAssert.AreEqual(destinationData, await module.ReadAsync(destinationPath));
            });
        }

        [UnityTest]
        public IEnumerator WriteAsync_WhenDataIsNull_Throws()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = await CreateIsolatedStartedModuleAsync();
                m_Module = module;

                var exception = await ThrowsAsync<ArgumentNullException>(async () => { await module.WriteAsync(UniquePath("null"), "1", (byte[])null); });
                Assert.AreEqual("data", exception.ParamName);
            });
        }

        [UnityTest]
        public IEnumerator VFSteaming_ReadAsync_WhenStreamEndsEarly_ThrowsEndOfStreamException()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var steaming = CreateIsolatedSteaming();
                try
                {
                    await steaming.WriteAsync(0, new byte[] { 1, 2, 3 });

                    var exception = await ThrowsAsync<EndOfStreamException>(async () =>
                    {
                        await steaming.ReadAsync(0, 4);
                    });

                    StringAssert.Contains("Read 3 bytes", exception.Message);
                }
                finally
                {
                    steaming.Dispose();
                }
            });
        }

        [UnityTest]
        public IEnumerator VFSteaming_WhenOffsetOperationsRunConcurrently_PreservesEveryRegion()
        {
            return UniTask.ToCoroutine(async () =>
            {
                const int regionCount = 16;
                const int regionSize = 1024 * 1024;
                var steaming = CreateIsolatedSteaming();
                try
                {
                    var expected = new byte[regionCount][];
                    var writes = new UniTask[regionCount];
                    for (var regionIndex = 0; regionIndex < regionCount; regionIndex++)
                    {
                        var data = new byte[regionSize];
                        for (var byteIndex = 0; byteIndex < data.Length; byteIndex++)
                        {
                            data[byteIndex] = (byte)(regionIndex * 31 + byteIndex);
                        }

                        expected[regionIndex] = data;
                        writes[regionIndex] = steaming.WriteAsync((long)regionIndex * regionSize, data);
                    }

                    await UniTask.WhenAll(writes);

                    var mixedOperations = new UniTask[regionCount];
                    for (var regionIndex = 0; regionIndex < regionCount; regionIndex++)
                    {
                        if (regionIndex % 2 == 0)
                        {
                            var replacement = new byte[regionSize];
                            for (var byteIndex = 0; byteIndex < replacement.Length; byteIndex++)
                            {
                                replacement[byteIndex] = (byte)(255 - regionIndex * 17 - byteIndex);
                            }

                            expected[regionIndex] = replacement;
                            mixedOperations[regionIndex] = steaming.WriteAsync((long)regionIndex * regionSize, replacement);
                        }
                        else
                        {
                            mixedOperations[regionIndex] = AssertRegionAsync(
                                steaming,
                                (long)regionIndex * regionSize,
                                expected[regionIndex]);
                        }
                    }

                    await UniTask.WhenAll(mixedOperations);

                    var reads = new UniTask<byte[]>[regionCount];
                    for (var regionIndex = 0; regionIndex < regionCount; regionIndex++)
                    {
                        reads[regionIndex] = steaming.ReadAsync((long)regionIndex * regionSize, regionSize);
                    }

                    var actual = await UniTask.WhenAll(reads);
                    for (var regionIndex = 0; regionIndex < regionCount; regionIndex++)
                    {
                        CollectionAssert.AreEqual(expected[regionIndex], actual[regionIndex]);
                    }
                }
                finally
                {
                    steaming.Dispose();
                }
            });
        }

        private static async UniTask AssertRegionAsync(VFSteaming steaming, long offset, byte[] expected)
        {
            var actual = await steaming.ReadAsync(offset, expected.Length);
            CollectionAssert.AreEqual(expected, actual);
        }

        private static async UniTask WriteTemporaryAsync(FileTemporaryHandle handle, byte[] data)
        {
            using (var stream = await handle.OpenWriteAsync(false))
            {
                await stream.WriteAsync(data, 0, data.Length);
            }
        }

        private static async UniTask<byte[]> ReadTemporaryAsync(FileTemporaryHandle handle)
        {
            using (var stream = await handle.OpenReadAsync())
            using (var memory = new MemoryStream())
            {
                await stream.CopyToAsync(memory);
                return memory.ToArray();
            }
        }

        private static async UniTask<byte[]> ReadStreamAsync(Stream stream)
        {
            using (var memory = new MemoryStream())
            {
                await stream.CopyToAsync(memory);
                return memory.ToArray();
            }
        }

        internal static UniTask<FileModule> CreateStartedModuleAsync()
        {
            var module = new FileModule();
            module.Startup();
            return UniTask.FromResult(module);
        }

        private UniTask<FileModule> CreateIsolatedStartedModuleAsync()
        {
            m_RootPath = Path.Combine(UnityEngine.Application.temporaryCachePath, "vfs-tests", Guid.NewGuid().ToString("N"));
            var module = new FileModule(m_RootPath);
            module.Startup();
            return UniTask.FromResult(module);
        }

        private FileModule RestartIsolatedModule(FileModule module)
        {
            module.Shutdown();
            var restarted = new FileModule(m_RootPath);
            restarted.Startup();
            m_Module = restarted;
            return restarted;
        }

        private VFSteaming CreateIsolatedSteaming()
        {
            m_RootPath = Path.Combine(UnityEngine.Application.temporaryCachePath, "vfs-stream-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_RootPath);
            return new VFSteaming(Path.Combine(m_RootPath, "stream.bundle"));
        }

        internal static string UniquePath(string prefix)
        {
            return $"tests/{prefix}-{Guid.NewGuid():N}.bin";
        }

        private static string GetBundlePath(FileModule module, string bundlePath)
        {
            return Path.Combine(module.RootPath, bundlePath);
        }

        private static bool BundlePathExists(FileModule module, string bundlePath)
        {
            foreach (var entry in module.ListFiles())
            {
                if (entry.BundlePath == bundlePath)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountFiles(FileModule module)
        {
            var count = 0;
            foreach (var unused in module.ListFiles())
            {
                count++;
            }

            return count;
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            return (T)field.GetValue(target);
        }

        private static bool ManifestBundlePathExists(FileModule module, string bundlePath)
        {
            var manifestPath = Path.Combine(module.RootPath, VfsConstants.ManifestFileName);
            return System.IO.File.ReadAllText(manifestPath).Contains($"\"BundlePath\": \"{bundlePath}\"");
        }

        private static async UniTask<TException> ThrowsAsync<TException>(Func<UniTask> action)
            where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException exception)
            {
                return exception;
            }

            Assert.Fail($"Expected exception of type {typeof(TException).FullName}.");
            return null;
        }

        private void WriteManifest(params VFSMeta[] entries)
        {
            var manifestPath = Path.Combine(m_RootPath, VfsConstants.ManifestFileName);
            System.IO.File.WriteAllText(manifestPath, JsonConvert.SerializeObject(entries));
        }

        private sealed class NonSeekableReadStream : Stream
        {
            private readonly MemoryStream m_Stream;

            public NonSeekableReadStream(byte[] data)
            {
                m_Stream = new MemoryStream(data, false);
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return m_Stream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    m_Stream.Dispose();
                }

                base.Dispose(disposing);
            }
        }

        private sealed class ThrowingReadStream : Stream
        {
            private readonly byte[] m_Data;
            private bool m_ReadOnce;

            public ThrowingReadStream(byte[] data)
            {
                m_Data = data;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (m_ReadOnce)
                {
                    throw new IOException("Injected stream read failure.");
                }

                m_ReadOnce = true;
                var read = Math.Min(count, m_Data.Length);
                Array.Copy(m_Data, 0, buffer, offset, read);
                return read;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
