using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using GameDeveloperKit.Resource;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class NetworkAssetProviderTests : RuntimeTestBase
    {
        [UnityTest]
        public IEnumerator LoadAssetAsync_WhenSameUrlIsConcurrent_CoalescesAndRetainsPerCaller()
        {
            return UniTask.ToCoroutine(async () =>
            {
                using (var server = new LoopbackHttpServer(Response.Ok("shared")))
                {
                    var provider = new NetworkAssetProvider();
                    var firstTask = provider.LoadAssetAsync(server.Url);
                    var secondTask = provider.LoadAssetAsync(server.Url);
                    var first = await firstTask;
                    var second = await secondTask;

                    Assert.AreEqual(1, server.RequestCount);
                    Assert.AreSame(first, second);
                    Assert.AreEqual(2, first.ReferenceCount);
                    Assert.AreEqual("shared", first.GetAsset<TextAsset>().text);

                    await provider.UnloadAsset(first);
                    Assert.AreEqual(1, first.ReferenceCount);
                    await provider.UnloadAsset(second);
                    Assert.AreEqual(0, first.ReferenceCount);
                    Assert.AreEqual(ResourceStatus.Succeeded, first.Status);

                    var revived = await provider.LoadAssetAsync(server.Url);
                    Assert.AreSame(first, revived);
                    Assert.AreEqual(1, revived.ReferenceCount);
                    Assert.AreEqual(1, server.RequestCount);
                    await provider.UnloadAsset(revived);
                    await provider.UnloadUnusedAssetAsync();

                    Assert.AreEqual(ResourceStatus.Released, first.Status);
                    Assert.IsFalse(provider.HasLoadedAssets);
                }
            });
        }

        [UnityTest]
        public IEnumerator LoadRawAssetAsync_WhenSameUrlIsConcurrent_CoalescesAndRetainsPerCaller()
        {
            return UniTask.ToCoroutine(async () =>
            {
                using (var server = new LoopbackHttpServer(Response.Ok("raw")))
                {
                    var provider = new NetworkAssetProvider();
                    var firstTask = provider.LoadRawAssetAsync(server.Url);
                    var secondTask = provider.LoadRawAssetAsync(server.Url);
                    var first = await firstTask;
                    var second = await secondTask;

                    Assert.AreEqual(1, server.RequestCount);
                    Assert.AreSame(first, second);
                    Assert.AreEqual(2, first.ReferenceCount);
                    Assert.AreEqual("raw", first.GetString());

                    first.Release();
                    second.Release();
                    await provider.UnloadUnusedAssetAsync();

                    Assert.AreEqual(ResourceStatus.Released, first.Status);
                    Assert.IsEmpty(first.Data);
                }
            });
        }

        [UnityTest]
        public IEnumerator LoadAssetAndRawAsync_WhenUrlMatches_KeepHandleKindsIndependent()
        {
            return UniTask.ToCoroutine(async () =>
            {
                using (var server = new LoopbackHttpServer(Response.Ok("asset"), Response.Ok("raw")))
                {
                    var provider = new NetworkAssetProvider();
                    var assetTask = provider.LoadAssetAsync(server.Url);
                    var rawTask = provider.LoadRawAssetAsync(server.Url);
                    var asset = await assetTask;
                    var raw = await rawTask;

                    Assert.AreEqual(2, server.RequestCount);
                    Assert.AreEqual(ResourceStatus.Succeeded, asset.Status);
                    Assert.AreEqual(ResourceStatus.Succeeded, raw.Status);
                    Assert.AreEqual(1, asset.ReferenceCount);
                    Assert.AreEqual(1, raw.ReferenceCount);

                    await provider.UnloadAsset(asset);
                    await provider.UnloadRawAsset(raw);
                    await provider.UnloadUnusedAssetAsync();
                    Assert.IsFalse(provider.HasLoadedAssets);
                }
            });
        }

        [UnityTest]
        public IEnumerator LoadAssetAsync_WhenSharedRequestFails_ReturnsIndependentFailuresAndRetries()
        {
            return UniTask.ToCoroutine(async () =>
            {
                using (var server = new LoopbackHttpServer(Response.Error(), Response.Ok("retry")))
                {
                    var provider = new NetworkAssetProvider();
                    var firstTask = provider.LoadAssetAsync(server.Url);
                    var secondTask = provider.LoadAssetAsync(server.Url);
                    var first = await firstTask;
                    var second = await secondTask;

                    Assert.AreEqual(1, server.RequestCount);
                    Assert.AreEqual(ResourceStatus.Failed, first.Status);
                    Assert.AreEqual(ResourceStatus.Failed, second.Status);
                    Assert.AreNotSame(first, second);
                    Assert.AreSame(first.Error, second.Error);

                    var retry = await provider.LoadAssetAsync(server.Url);

                    Assert.AreEqual(2, server.RequestCount);
                    Assert.AreEqual(ResourceStatus.Succeeded, retry.Status);
                    first.Release();
                    second.Release();
                    retry.Release();
                    await provider.UnloadUnusedAssetAsync();
                }
            });
        }

        [UnityTest]
        public IEnumerator ResourceModuleUnload_WhenHandleIsNetworkOwned_RoutesToNetworkProvider()
        {
            return UniTask.ToCoroutine(async () =>
            {
                using (var server = new LoopbackHttpServer(Response.Ok("module"), Response.Ok("raw")))
                {
                    var module = CreateReadyModule();
                    var handle = await module.LoadAssetAsync(server.Url);
                    var rawHandle = await module.LoadRawAssetAsync(server.Url);

                    await module.UnloadAsset(handle);
                    await module.UnloadRawAsset(rawHandle);
                    Assert.AreEqual(0, handle.ReferenceCount);
                    Assert.AreEqual(0, rawHandle.ReferenceCount);
                    Assert.AreEqual(ResourceStatus.Succeeded, handle.Status);

                    await module.UnloadUnusedAssetAsync();

                    Assert.AreEqual(ResourceStatus.Released, handle.Status);
                    Assert.AreEqual(ResourceStatus.Released, rawHandle.Status);
                    Assert.IsNull(handle.Info);
                    Assert.IsNull(rawHandle.Info);
                    Assert.AreEqual(2, server.RequestCount);
                    module.Shutdown();
                }
            });
        }

        [UnityTest]
        public IEnumerator StopAndDrainPendingLoadsAsync_WhenRequestIsActive_RejectsNewLoadsAndDrains()
        {
            return UniTask.ToCoroutine(async () =>
            {
                using (var server = new LoopbackHttpServer(new Response(200, "active", 150)))
                {
                    var provider = new NetworkAssetProvider();
                    var activeTask = provider.LoadAssetAsync(server.Url);
                    var drainTask = provider.StopAndDrainPendingLoadsAsync();
                    var rejected = await provider.LoadRawAssetAsync(server.Url + "/rejected");

                    Assert.AreEqual(ResourceStatus.Failed, rejected.Status);
                    StringAssert.Contains("shutting down", rejected.Error.Message);

                    var active = await activeTask;
                    await drainTask;

                    Assert.AreEqual(ResourceStatus.Succeeded, active.Status);
                    rejected.Release();
                    provider.Release();
                    Assert.AreEqual(ResourceStatus.Released, active.Status);
                }
            });
        }

        private static ResourceModule CreateReadyModule()
        {
            var module = new ResourceModule();
            var index = new ResourceManifestIndex(new ManifestInfo
            {
                Version = "network",
                Packages = new List<PackageInfo>()
            });
            SetPrivateField(module, "_manifestIndex", index);
            SetPrivateField(module, "_setting", new ResourceSettings
            {
                Mode = ResourceMode.Offline,
                DefaultPackages = Array.Empty<string>()
            });
            SetPrivateField(module, "_initializeState", ResourceInitializeState.Initialized);
            return module;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(target, value);
        }

        private readonly struct Response
        {
            public int StatusCode { get; }
            public string Body { get; }
            public int DelayMilliseconds { get; }

            public Response(int statusCode, string body, int delayMilliseconds = 0)
            {
                StatusCode = statusCode;
                Body = body;
                DelayMilliseconds = delayMilliseconds;
            }

            public static Response Ok(string body)
            {
                return new Response(200, body);
            }

            public static Response Error()
            {
                return new Response(500, "failed");
            }
        }

        private sealed class LoopbackHttpServer : IDisposable
        {
            private readonly TcpListener m_Listener;
            private readonly Thread m_Thread;
            private readonly Response[] m_Responses;
            private int m_RequestCount;

            public LoopbackHttpServer(params Response[] responses)
            {
                m_Responses = responses ?? throw new ArgumentNullException(nameof(responses));
                if (m_Responses.Length == 0)
                {
                    throw new ArgumentException("At least one response is required.", nameof(responses));
                }

                m_Listener = new TcpListener(IPAddress.Loopback, 0);
                m_Listener.Start();
                var endpoint = (IPEndPoint)m_Listener.LocalEndpoint;
                Url = $"http://127.0.0.1:{endpoint.Port}/asset.txt";
                m_Thread = new Thread(Run) { IsBackground = true };
                m_Thread.Start();
            }

            public string Url { get; }

            public int RequestCount => Volatile.Read(ref m_RequestCount);

            public void Dispose()
            {
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
                    foreach (var response in m_Responses)
                    {
                        using (var client = m_Listener.AcceptTcpClient())
                        using (var stream = client.GetStream())
                        {
                            ReadRequest(stream);
                            Interlocked.Increment(ref m_RequestCount);
                            if (response.DelayMilliseconds > 0)
                            {
                                Thread.Sleep(response.DelayMilliseconds);
                            }

                            WriteResponse(stream, response);
                        }
                    }
                }
                catch (SocketException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }

            private static void ReadRequest(NetworkStream stream)
            {
                var buffer = new byte[2048];
                var builder = new StringBuilder();
                while (builder.ToString().Contains("\r\n\r\n") is false)
                {
                    var count = stream.Read(buffer, 0, buffer.Length);
                    if (count <= 0)
                    {
                        return;
                    }

                    builder.Append(Encoding.UTF8.GetString(buffer, 0, count));
                }
            }

            private static void WriteResponse(NetworkStream stream, Response response)
            {
                var body = Encoding.UTF8.GetBytes(response.Body ?? string.Empty);
                var reason = response.StatusCode >= 200 && response.StatusCode < 300 ? "OK" : "Error";
                var headers = Encoding.UTF8.GetBytes(
                    $"HTTP/1.1 {response.StatusCode} {reason}\r\n" +
                    "Content-Type: text/plain\r\n" +
                    $"Content-Length: {body.Length}\r\n" +
                    "Connection: close\r\n\r\n");
                stream.Write(headers, 0, headers.Length);
                stream.Write(body, 0, body.Length);
            }
        }
    }
}
