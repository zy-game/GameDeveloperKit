using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Download;
using GameDeveloperKit.File;
using GameDeveloperKit.Operation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class DownloadModuleTests : RuntimeTestBase
    {
        private const string TestDownloadUrl = "https://saltgame-1251268098.cos.ap-chengdu.myqcloud.com/common.game";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                try
                {
                    await App.Unregister<DownloadModule>();
                }
                catch (GameException)
                {
                }

                try
                {
                    await App.Unregister<OperationModule>();
                }
                catch (GameException)
                {
                }

                try
                {
                    await App.Unregister<FileModule>();
                }
                catch (GameException)
                {
                }
            });
        }

        [UnityTest]
        public IEnumerator Register_WhenDownloadModuleIsRegistered_ReturnsDownload()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await EnsureOperationAsync();
                App.Register<DownloadModule>();

                Assert.IsNotNull(App.Download);
            });
        }

        [UnityTest]
        public IEnumerator DownloadAsync_WhenSameUrlRequested_ReturnsCachedHandler()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await EnsureOperationAsync();
                var module = await CreateStartedModuleAsync();
                var url = "https://example.com/cached.bin";

                var first = module.DownloadAsync(url);
                var second = module.DownloadAsync(url);

                Assert.AreSame(first, second);
                Assert.IsTrue(module.HasDownload(url));
                Assert.AreSame(first, module.GetDownload(url));

                await module.Cancel(url);
            });
        }

        [UnityTest]
        public IEnumerator DownloadAsync_WhenCachedHandlerFailed_CreatesNewExecution()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await EnsureOperationAsync();
                var module = await CreateStartedModuleAsync();
                var url = "https://example.com/retry.bin";
                var failed = new DownloadHandler();
                failed.SetException(new InvalidOperationException("failed attempt"));
                ObserveCompletion(failed);
                GetDownloads(module).Add(url, failed);

                var retry = module.DownloadAsync(url);

                Assert.AreNotSame(failed, retry);
                Assert.AreEqual(OperationStatus.Running, retry.Status);
                await module.Cancel(url);
            });
        }

        [UnityTest]
        public IEnumerator DownloadAsync_WhenRemoteFileResponds_WritesDownloadedBytesIntoFileModule()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await EnsureOperationAsync();
                var download = await CreateStartedModuleAsync();
                var file = App.File;

                var handler = download.DownloadAsync(TestDownloadUrl);
                await handler.WaitCompletionAsync();

                Assert.AreEqual(OperationStatus.Succeeded, handler.Status);
                var downloaded = await handler.ReadAsync();
                Assert.Greater(downloaded.Length, 0);
                using (var stream = await handler.OpenReadAsync())
                {
                    Assert.AreEqual(downloaded.Length, stream.Length);
                }

                var vfsPath = System.IO.Path.GetFileName(TestDownloadUrl);
                await handler.SaveAsync(vfsPath, "downloaded");
                CollectionAssert.AreEqual(downloaded, await file.ReadAsync(vfsPath));

                await download.ReleaseAsync(handler);
                Assert.IsFalse(download.HasDownload(TestDownloadUrl));
                CollectionAssert.AreEqual(downloaded, await file.ReadAsync(vfsPath));
                await ThrowsAsync<GameException>(async () => { await handler.ReadAsync(); });
                await file.DeleteAsync(vfsPath);
            });
        }

        [UnityTest]
        public IEnumerator ReleaseAsync_WhenDownloadIsActive_RejectsWithoutChangingExecution()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await EnsureOperationAsync();
                var module = await CreateStartedModuleAsync();
                var url = "https://example.com/active-release.bin";
                var handler = module.DownloadAsync(url);

                var exception = await ThrowsAsync<GameException>(async () =>
                {
                    await module.ReleaseAsync(handler);
                });
                StringAssert.Contains("active download", exception.Message);
                Assert.AreSame(handler, module.GetDownload(url));

                await module.Cancel(url);
            });
        }

        [UnityTest]
        public IEnumerator ReleaseAsync_WhenDownloadCompletes_WaitsForObserversBeforeDeletingTemporaryFile()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await EnsureOperationAsync();
                var module = await CreateStartedModuleAsync();
                using (var server = new SlowDownloadServer())
                {
                    var handler = module.DownloadAsync(server.Url);
                    var temporary = GetTemporaryFile(handler);
                    var temporaryExistedDuringNotification = false;
                    handler.Completed += _ => temporaryExistedDuringNotification = temporary.Exists;

                    await handler.WaitCompletionAsync();
                    await module.ReleaseAsync(handler);

                    Assert.IsTrue(temporaryExistedDuringNotification);
                    Assert.Throws<ObjectDisposedException>(() =>
                    {
                        _ = temporary.Exists;
                    });
                    Assert.IsFalse(module.HasDownload(server.Url));
                }
            });
        }

        [UnityTest]
        public IEnumerator CancelAll_WhenDownloadsExist_CancelsAndClearsHandlers()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await EnsureOperationAsync();
                var module = await CreateStartedModuleAsync();
                var first = "https://example.com/a.bin";
                var second = "https://example.com/b.bin";

                module.DownloadAsync(first);
                module.DownloadAsync(second);

                await module.CancelAll();

                Assert.IsFalse(module.HasDownload(first));
                Assert.IsFalse(module.HasDownload(second));
            });
        }

        [UnityTest]
        public IEnumerator DownloadChunked_WhenPausedAndResumed_ContinuesSameExecutionAndPartHandles()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await EnsureOperationAsync();
                var module = await CreateStartedModuleAsync();
                using (var server = new RangeDownloadServer())
                {
                    var handler = module.DownloadAsync(server.Url);
                    await WaitUntilAsync(
                        () => GetDownloadChunks(handler).Exists(chunk =>
                            chunk.TemporaryFile.Exists && chunk.TemporaryFile.Length > 0),
                        TimeSpan.FromSeconds(10d));
                    var mainTemporary = GetTemporaryFile(handler);
                    var partHandles = GetDownloadChunks(handler)
                        .ConvertAll(chunk => chunk.TemporaryFile);

                    await handler.Pause();
                    await WaitUntilAsync(
                        () => GetActiveRequestCount(handler) == 0,
                        TimeSpan.FromSeconds(10d));
                    var partialLength = 0L;
                    foreach (var part in partHandles)
                    {
                        partialLength += part.Length;
                    }

                    Assert.Greater(partialLength, 0);
                    Assert.Less(partialLength, RangeDownloadServer.ContentLength);

                    await handler.Resume();
                    Assert.AreSame(handler, module.DownloadAsync(server.Url));
                    await handler.WaitCompletionAsync();

                    Assert.AreEqual(OperationStatus.Succeeded, handler.Status, handler.Error?.ToString());
                    Assert.IsTrue(handler.IsChunked);
                    Assert.AreEqual(handler.TotalChunkCount, handler.CompletedChunkCount);
                    Assert.AreSame(mainTemporary, GetTemporaryFile(handler));
                    CollectionAssert.AreEqual(
                        partHandles,
                        GetDownloadChunks(handler).ConvertAll(chunk => chunk.TemporaryFile));
                    Assert.IsTrue(server.HasResumedRange);
                    var bytes = await handler.ReadAsync();
                    Assert.AreEqual(RangeDownloadServer.ContentLength, bytes.Length);
                    for (var offset = 0; offset < bytes.Length; offset += 257 * 1024)
                    {
                        Assert.AreEqual(RangeDownloadServer.ExpectedByte(offset), bytes[offset]);
                    }

                    await module.Cancel(server.Url);
                }
            });
        }

        [UnityTest]
        public IEnumerator DownloadHandler_WhenResultIsNotAvailable_ReadOpenAndSaveThrow()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var pending = new DownloadHandler();

                Assert.Throws<GameException>(() => pending.OpenReadAsync());
                await ThrowsAsync<GameException>(async () => { await pending.ReadAsync(); });
                await ThrowsAsync<GameException>(async () =>
                {
                    await pending.SaveAsync("tests/unavailable.bin", "1");
                });

                pending.SetCancel();
                Assert.AreEqual(OperationStatus.Cancelled, pending.Status);
                Assert.Throws<GameException>(() => pending.OpenReadAsync());
            });
        }

        [UnityTest]
        public IEnumerator Cancel_WhenRequestIsWriting_AbortsAndReleasesBeforeDeletingTempAndCommittingTerminal()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await EnsureOperationAsync();
                var module = await CreateStartedModuleAsync();
                using (var server = new SlowDownloadServer())
                {
                    var handler = module.DownloadAsync(server.Url);
                    var temporary = GetTemporaryFile(handler);
                    var nativePath = temporary.NativePath;
                    var statusAtNotification = OperationStatus.None;
                    handler.Canceled += value => statusAtNotification = value.Status;
                    await WaitUntilAsync(() => server.GetStarted, TimeSpan.FromSeconds(5d));
                    await WaitUntilAsync(() => System.IO.File.Exists(nativePath), TimeSpan.FromSeconds(5d));

                    await module.Cancel(server.Url);

                    Assert.AreEqual(OperationStatus.Cancelled, handler.Status);
                    Assert.AreEqual(OperationStatus.Cancelled, statusAtNotification);
                    Assert.AreEqual(DownloadFailureKind.Canceled, handler.FailureKind);
                    Assert.IsFalse(System.IO.File.Exists(nativePath));
                    Assert.IsFalse(module.HasDownload(server.Url));
                }
            });
        }

        [UnityTest]
        public IEnumerator PrepareShutdownAsync_WhenRequestIsWriting_WaitsForCancellationCleanup()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await EnsureOperationAsync();
                var module = await CreateStartedModuleAsync();
                using (var server = new SlowDownloadServer())
                {
                    var handler = module.DownloadAsync(server.Url);
                    var nativePath = GetTemporaryFile(handler).NativePath;
                    await WaitUntilAsync(() => server.GetStarted, TimeSpan.FromSeconds(5d));

                    await ((IAsyncShutdownParticipant)module).PrepareShutdownAsync();
                    Assert.DoesNotThrow(() => module.Shutdown());

                    Assert.AreEqual(OperationStatus.Cancelled, handler.Status);
                    Assert.IsFalse(System.IO.File.Exists(nativePath));
                    Assert.IsFalse(module.HasDownload(server.Url));
                }
            });
        }

        [UnityTest]
        public IEnumerator PrepareShutdownAsync_WhenDownloadListIsActive_CancelsListWithoutStartingNextItem()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await EnsureOperationAsync();
                var module = await CreateStartedModuleAsync();
                using (var server = new SlowDownloadServer())
                {
                    var list = module.DownloadListAsync(server.Url, server.Url + "?second");
                    await WaitUntilAsync(() => server.GetStarted, TimeSpan.FromSeconds(5d));

                    await ((IAsyncShutdownParticipant)module).PrepareShutdownAsync();
                    Assert.DoesNotThrow(() => module.Shutdown());
                    await list.WaitCompletionAsync();

                    Assert.AreEqual(OperationStatus.Cancelled, list.Status);
                    Assert.AreEqual(1, list.Items.Count);
                    Assert.AreEqual(OperationStatus.Cancelled, list.Items[0].Status);
                    Assert.IsFalse(module.HasDownload(server.Url));
                    Assert.IsFalse(module.HasDownload(server.Url + "?second"));
                }
            });
        }

        [Test]
        public void DownloadAsync_WhenUrlInvalid_Throws()
        {
            var module = new DownloadModule();

            Assert.Throws<ArgumentNullException>(() => module.DownloadAsync(null));
            Assert.Throws<ArgumentException>(() => module.DownloadAsync(" "));
            Assert.Throws<ArgumentException>(() => module.DownloadAsync("file:///tmp/test.bin"));
        }

        [Test]
        public void Shutdown_WhenTerminalDownloadIsStillTracked_RequiresAsyncPreparation()
        {
            EnsureOperationAsync().GetAwaiter().GetResult();
            var module = CreateStartedModuleAsync().GetAwaiter().GetResult();
            var handler = new DownloadHandler();
            handler.SetResult();
            GetDownloads(module).Add("https://example.com/completed.bin", handler);

            Assert.Throws<GameException>(() => module.Shutdown());

            module.CancelAll().GetAwaiter().GetResult();
            Assert.DoesNotThrow(() => module.Shutdown());
        }

        private static UniTask EnsureOperationAsync()
        {
            try
            {
                App.Register<FileModule>();
            }
            catch (GameException)
            {
            }

            try
            {
                App.Register<OperationModule>();
            }
            catch (GameException)
            {
            }

            return UniTask.CompletedTask;
        }

        private static UniTask<DownloadModule> CreateStartedModuleAsync()
        {
            var module = new DownloadModule();
            module.Startup();
            return UniTask.FromResult(module);
        }

        private static Dictionary<string, DownloadHandler> GetDownloads(DownloadModule module)
        {
            var field = typeof(DownloadModule).GetField("m_Downloads", BindingFlags.Instance | BindingFlags.NonPublic);
            return (Dictionary<string, DownloadHandler>)field.GetValue(module);
        }

        private static FileTemporaryHandle GetTemporaryFile(DownloadHandler handler)
        {
            var field = typeof(DownloadHandler).GetField(
                "m_TemporaryFile",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return (FileTemporaryHandle)field.GetValue(handler);
        }

        private static List<DownloadChunk> GetDownloadChunks(DownloadHandler handler)
        {
            var field = typeof(DownloadHandler).GetField(
                "m_Chunks",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return (List<DownloadChunk>)field.GetValue(handler);
        }

        private static int GetActiveRequestCount(DownloadHandler handler)
        {
            var field = typeof(DownloadHandler).GetField(
                "m_ActiveRequests",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return ((ICollection)field.GetValue(handler)).Count;
        }

        private static void ObserveCompletion(OperationHandle operation)
        {
            try
            {
                operation.WaitCompletionAsync().GetAwaiter().GetResult();
            }
            catch
            {
            }
        }

        private static async UniTask WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!predicate())
            {
                if (DateTime.UtcNow >= deadline)
                {
                    Assert.Fail("Timed out waiting for download test condition.");
                }

                await UniTask.Yield();
            }
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

        private sealed class SlowDownloadServer : IDisposable
        {
            private const int ContentLength = 256 * 1024;
            private readonly TcpListener m_Listener;
            private readonly Thread m_Thread;
            private volatile bool m_Disposed;
            private volatile bool m_GetStarted;

            public SlowDownloadServer()
            {
                m_Listener = new TcpListener(IPAddress.Loopback, 0);
                m_Listener.Start();
                var endpoint = (IPEndPoint)m_Listener.LocalEndpoint;
                Url = $"http://127.0.0.1:{endpoint.Port}/slow-download.bin";
                m_Thread = new Thread(Run)
                {
                    IsBackground = true
                };
                m_Thread.Start();
            }

            public string Url { get; }

            public bool GetStarted => m_GetStarted;

            public void Dispose()
            {
                m_Disposed = true;
                m_Listener.Stop();
                if (m_Thread.IsAlive)
                {
                    m_Thread.Join(500);
                }
            }

            private void Run()
            {
                try
                {
                    while (!m_Disposed)
                    {
                        using (var client = m_Listener.AcceptTcpClient())
                        using (var stream = client.GetStream())
                        {
                            var request = ReadHeaders(stream);
                            if (request.StartsWith("HEAD ", StringComparison.Ordinal))
                            {
                                WriteHeaders(stream);
                                continue;
                            }

                            m_GetStarted = true;
                            WriteHeaders(stream);
                            WriteBody(stream);
                        }
                    }
                }
                catch (SocketException)
                {
                }
                catch (IOException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }

            private void WriteBody(NetworkStream stream)
            {
                var buffer = new byte[4096];
                var remaining = ContentLength;
                while (!m_Disposed && remaining > 0)
                {
                    var count = Math.Min(buffer.Length, remaining);
                    stream.Write(buffer, 0, count);
                    stream.Flush();
                    remaining -= count;
                    Thread.Sleep(10);
                }
            }

            private static string ReadHeaders(NetworkStream stream)
            {
                var buffer = new byte[1024];
                var builder = new StringBuilder();
                while (builder.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal) < 0)
                {
                    var count = stream.Read(buffer, 0, buffer.Length);
                    if (count <= 0)
                    {
                        break;
                    }

                    builder.Append(Encoding.ASCII.GetString(buffer, 0, count));
                }

                return builder.ToString();
            }

            private static void WriteHeaders(NetworkStream stream)
            {
                var headers = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
                    $"Content-Length: {ContentLength}\r\n" +
                    "Content-Type: application/octet-stream\r\n" +
                    "Connection: close\r\n\r\n");
                stream.Write(headers, 0, headers.Length);
                stream.Flush();
            }
        }

        private sealed class RangeDownloadServer : IDisposable
        {
            public const int ContentLength = 16 * 1024 * 1024;

            private readonly TcpListener m_Listener;
            private readonly Thread m_Thread;
            private readonly object m_RangeLock = new object();
            private readonly List<long> m_RangeStarts = new List<long>();
            private volatile bool m_Disposed;

            public RangeDownloadServer()
            {
                m_Listener = new TcpListener(IPAddress.Loopback, 0);
                m_Listener.Start();
                var endpoint = (IPEndPoint)m_Listener.LocalEndpoint;
                Url = $"http://127.0.0.1:{endpoint.Port}/range-download.bin";
                m_Thread = new Thread(Run)
                {
                    IsBackground = true
                };
                m_Thread.Start();
            }

            public string Url { get; }

            public bool HasResumedRange
            {
                get
                {
                    lock (m_RangeLock)
                    {
                        return m_RangeStarts.Exists(start => start % DownloadHandler.ChunkSize != 0);
                    }
                }
            }

            public static byte ExpectedByte(long offset)
            {
                return (byte)(offset % 251);
            }

            public void Dispose()
            {
                m_Disposed = true;
                m_Listener.Stop();
                if (m_Thread.IsAlive)
                {
                    m_Thread.Join(2000);
                }
            }

            private void Run()
            {
                while (!m_Disposed)
                {
                    try
                    {
                        using (var client = m_Listener.AcceptTcpClient())
                        using (var stream = client.GetStream())
                        {
                            var request = ReadRequestHeaders(stream);
                            if (request.StartsWith("HEAD ", StringComparison.Ordinal))
                            {
                                WriteHeadResponse(stream);
                                continue;
                            }

                            if (!request.StartsWith("GET ", StringComparison.Ordinal))
                            {
                                continue;
                            }

                            var (start, end) = ParseRange(request);
                            lock (m_RangeLock)
                            {
                                m_RangeStarts.Add(start);
                            }

                            WriteRangeResponse(stream, start, end);
                        }
                    }
                    catch (SocketException)
                    {
                        if (!m_Disposed)
                        {
                            continue;
                        }
                    }
                    catch (IOException)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
            }

            private static string ReadRequestHeaders(NetworkStream stream)
            {
                var buffer = new byte[2048];
                var builder = new StringBuilder();
                while (builder.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal) < 0)
                {
                    var count = stream.Read(buffer, 0, buffer.Length);
                    if (count <= 0)
                    {
                        break;
                    }

                    builder.Append(Encoding.ASCII.GetString(buffer, 0, count));
                }

                return builder.ToString();
            }

            private static (long Start, long End) ParseRange(string request)
            {
                foreach (var line in request.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!line.StartsWith("Range: bytes=", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var bounds = line.Substring("Range: bytes=".Length).Split('-');
                    return (long.Parse(bounds[0]), long.Parse(bounds[1]));
                }

                return (0, ContentLength - 1);
            }

            private static void WriteHeadResponse(NetworkStream stream)
            {
                WriteAscii(
                    stream,
                    "HTTP/1.1 200 OK\r\n" +
                    $"Content-Length: {ContentLength}\r\n" +
                    "Accept-Ranges: bytes\r\n" +
                    "Connection: close\r\n\r\n");
            }

            private static void WriteRangeResponse(NetworkStream stream, long start, long end)
            {
                var length = end - start + 1;
                WriteAscii(
                    stream,
                    "HTTP/1.1 206 Partial Content\r\n" +
                    $"Content-Length: {length}\r\n" +
                    $"Content-Range: bytes {start}-{end}/{ContentLength}\r\n" +
                    "Accept-Ranges: bytes\r\n" +
                    "Connection: close\r\n\r\n");
                var buffer = new byte[32 * 1024];
                var position = start;
                while (position <= end)
                {
                    var count = (int)Math.Min(buffer.Length, end - position + 1);
                    for (var index = 0; index < count; index++)
                    {
                        buffer[index] = ExpectedByte(position + index);
                    }

                    stream.Write(buffer, 0, count);
                    stream.Flush();
                    position += count;
                    Thread.Sleep(2);
                }
            }

            private static void WriteAscii(NetworkStream stream, string value)
            {
                var bytes = Encoding.ASCII.GetBytes(value);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }
        }
    }
}
