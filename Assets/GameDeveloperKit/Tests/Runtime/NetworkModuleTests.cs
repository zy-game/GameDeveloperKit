using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Network;
using MemoryPack;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class NetworkModuleTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                try
                {
                    await App.Shutdown();
                }
                catch (GameException)
                {
                }
            });
        }

        [Test]
        public void Register_WhenNetworkModuleIsRegistered_ReturnsNetwork()
        {
            App.Register<NetworkModule>().GetAwaiter().GetResult();

            Assert.IsNotNull(App.Network);
        }

        [UnityTest]
        public IEnumerator Startup_WhenDefaultModulesRegistered_ReturnsNetwork()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Startup();

                Assert.IsNotNull(App.Network);
            });
        }

        [Test]
        public void CreateChannel_WhenNameIsUnique_StoresChannel()
        {
            var module = new NetworkModule();
            var endpoint = CreateEndpoint();

            var channel = module.CreateChannel("game", endpoint);

            Assert.AreEqual("game", channel.Name);
            Assert.AreSame(endpoint, channel.Endpoint);
            Assert.IsTrue(module.TryGetChannel("game", out var stored));
            Assert.AreSame(channel, stored);
        }

        [Test]
        public void CreateChannel_WhenNameIsDuplicate_ThrowsWithoutReplacing()
        {
            var module = new NetworkModule();
            var first = module.CreateChannel("game", CreateEndpoint());

            Assert.Throws<GameException>(() => module.CreateChannel("game", CreateEndpoint()));

            Assert.IsTrue(module.TryGetChannel("game", out var stored));
            Assert.AreSame(first, stored);
        }

        [Test]
        public void CreateChannel_WhenArgumentsAreInvalid_Throws()
        {
            var module = new NetworkModule();

            Assert.Throws<ArgumentNullException>(() => module.CreateChannel(null, CreateEndpoint()));
            Assert.Throws<ArgumentException>(() => module.CreateChannel(" ", CreateEndpoint()));
            Assert.Throws<ArgumentNullException>(() => module.CreateChannel("game", null));
        }

        [Test]
        public void ConnectAsync_WhenTransportSucceeds_SetsConnected()
        {
            var transport = new TestTransport();
            var channel = CreateChannel(transport: transport);

            channel.ConnectAsync().GetAwaiter().GetResult();

            Assert.AreEqual(NetworkChannelStatus.Connected, channel.Status);
            Assert.IsTrue(transport.ConnectCalled);
        }

        [Test]
        public void ConnectAsync_WhenTransportFails_SetsFailedAndRecordsError()
        {
            var transport = new TestTransport
            {
                ConnectException = new InvalidOperationException("connect failed")
            };
            var channel = CreateChannel(transport: transport);

            var exception = Assert.Throws<NetworkException>(() => channel.ConnectAsync().GetAwaiter().GetResult());

            Assert.AreEqual(NetworkFailureKind.Connection, exception.FailureKind);
            Assert.AreEqual(NetworkChannelStatus.Failed, channel.Status);
            Assert.IsNotNull(channel.LastException);
        }

        [Test]
        public void SendAsync_WhenNotConnected_ThrowsWithoutPendingSlot()
        {
            var channel = CreateChannel();
            var request = new PingRequest();

            Assert.Throws<GameException>(() => channel.SendAsync(request).GetAwaiter().GetResult());

            Assert.AreEqual(0L, request.SequenceId);
            Assert.AreEqual(0, channel.PendingResponseCount);
        }

        [Test]
        public void WaitAsync_WhenNotConnected_ThrowsWithoutPendingSlot()
        {
            var channel = CreateChannel();
            var request = new PingRequest();

            Assert.Throws<GameException>(() => channel.WaitAsync<PingResponse>(request).GetAwaiter().GetResult());

            Assert.AreEqual(0L, request.SequenceId);
            Assert.AreEqual(0, channel.PendingResponseCount);
        }

        [Test]
        public void SendAsync_WhenConnected_SendsWithoutPendingSlot()
        {
            var channel = CreateConnectedChannel();
            var request = new PingRequest();

            channel.SendAsync(request).GetAwaiter().GetResult();

            Assert.Greater(request.SequenceId, 0L);
            Assert.AreEqual(0, channel.PendingResponseCount);
        }

        [Test]
        public void WaitAsync_WhenResponseArrives_ReturnsTypedResponse()
        {
            var channel = CreateConnectedChannel();
            var request = new PingRequest();

            var wait = channel.WaitAsync<PingResponse>(request);
            var response = new PingResponse { SequenceId = request.SequenceId };
            channel.Receive(response);
            var result = wait.GetAwaiter().GetResult();

            Assert.AreSame(response, result);
            Assert.Greater(request.SequenceId, 0L);
            Assert.AreEqual(0, channel.PendingResponseCount);
        }

        [Test]
        public void WaitAsync_WhenTransportLoopsRequest_DoesNotCompletePendingResponse()
        {
            var codec = new TestCodec();
            var transport = new TestTransport
            {
                EchoSentData = true
            };
            var channel = CreateConnectedChannel(codec, transport);
            var request = new PingRequest();
            codec.EnqueueDecode(request);

            var wait = channel.WaitAsync<PingResponse>(request);
            Assert.AreEqual(1, channel.PendingResponseCount);

            var response = new PingResponse { SequenceId = request.SequenceId };
            channel.Receive(response);

            Assert.AreSame(response, wait.GetAwaiter().GetResult());
            Assert.AreEqual(0, channel.PendingResponseCount);
        }

        [Test]
        public void MemoryPackNetworkCodec_WhenMessageHasOpcode_RoundTripsPacket()
        {
            var codec = new MemoryPackNetworkCodec();
            var request = new RegistryRequest
            {
                SequenceId = 42L
            };

            var encoded = codec.Encode(request);
            var packet = MemoryPackSerializer.Deserialize<NetworkPacket>(encoded);
            var decoded = codec.Decode(encoded);

            Assert.AreEqual(1001, packet.Opcode);
            Assert.AreEqual(42L, packet.SequenceId);
            Assert.Greater(packet.Payload.Length, 0);
            Assert.AreEqual(1001, decoded.MessageId);
            Assert.AreEqual(42L, decoded.SequenceId);
            Assert.IsInstanceOf<RegistryRequest>(decoded);
        }

        [Test]
        public void MemoryPackNetworkCodec_WhenMessageMissingOpcode_Throws()
        {
            var codec = new MemoryPackNetworkCodec();

            var exception = Assert.Throws<NetworkException>(() => codec.Encode(new MissingOpcodeMessage()));

            Assert.AreEqual(NetworkFailureKind.InvalidResponse, exception.FailureKind);
        }

        [Test]
        public void MemoryPackNetworkCodec_WhenOpcodeIsUnknown_ThrowsDecodeFailure()
        {
            var packet = new NetworkPacket
            {
                Opcode = 999999,
                Payload = MemoryPackSerializer.Serialize(new RegistryRequest())
            };
            var codec = new MemoryPackNetworkCodec();

            var exception = Assert.Throws<NetworkException>(() => codec.Decode(MemoryPackSerializer.Serialize(packet)));

            Assert.AreEqual(NetworkFailureKind.Decode, exception.FailureKind);
        }

        [Test]
        public void CreateChannel_WhenCodecIsOmitted_UsesMemoryPackCodecByDefault()
        {
            var module = new NetworkModule();
            var transport = new TestTransport();
            var channel = module.CreateChannel("game", CreateEndpoint(), null, transport);
            channel.ConnectAsync().GetAwaiter().GetResult();
            var request = new RegistryRequest();

            var wait = channel.WaitAsync<RegistryResponse>(request);
            var requestPacket = MemoryPackSerializer.Deserialize<NetworkPacket>(transport.LastSentData);
            var responseCodec = new MemoryPackNetworkCodec();
            transport.Emit(responseCodec.Encode(new RegistryResponse { SequenceId = request.SequenceId }));
            var result = wait.GetAwaiter().GetResult();

            Assert.AreEqual(1001, requestPacket.Opcode);
            Assert.AreEqual(request.SequenceId, requestPacket.SequenceId);
            Assert.Greater(requestPacket.Payload.Length, 0);
            Assert.AreEqual(request.SequenceId, result.SequenceId);
            Assert.AreEqual(0, channel.PendingResponseCount);
        }

        [UnityTest]
        public IEnumerator WaitAsync_WhenSendFails_RemovesPendingSlot()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var transport = new TestTransport
                {
                    SendException = new InvalidOperationException("send failed")
                };
                var channel = CreateConnectedChannel(transport: transport);
                var request = new PingRequest();

                var exception = await AssertThrowsAsync<NetworkException>(async () =>
                {
                    await channel.WaitAsync<PingResponse>(request);
                });

                Assert.AreEqual(NetworkFailureKind.Send, exception.FailureKind);
                Assert.AreEqual(0, channel.PendingResponseCount);
            });
        }

        [UnityTest]
        public IEnumerator WaitAsync_WhenResponseTimesOut_RemovesPendingSlot()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var channel = CreateConnectedChannel();
                channel.ResponseTimeout = TimeSpan.FromMilliseconds(10d);
                var request = new PingRequest();

                var wait = channel.WaitAsync<PingResponse>(request);
                await UniTask.Delay(TimeSpan.FromMilliseconds(30d));
                var exception = Assert.Throws<NetworkException>(() => wait.GetAwaiter().GetResult());

                Assert.AreEqual(NetworkFailureKind.Timeout, exception.FailureKind);
                Assert.AreEqual(0, channel.PendingResponseCount);
            });
        }

        [Test]
        public void Receive_WhenPushMessageArrives_DispatchesHandlersAndCallbacks()
        {
            var channel = CreateConnectedChannel();
            var handle = new ChatHandle();
            var typedCount = 0;
            Message globalMessage = null;
            var message = new ChatMessage { Text = "hello" };

            channel.Register(handle);
            channel.Subscribe<ChatMessage>(_ => typedCount++);
            channel.Subscribe(value => globalMessage = value);

            channel.Receive(message);

            Assert.AreSame(message, handle.LastMessage);
            Assert.AreEqual(1, typedCount);
            Assert.AreSame(message, globalMessage);
        }

        [Test]
        public void MessageSubscription_WhenCanceled_StopsCallback()
        {
            var channel = CreateConnectedChannel();
            var count = 0;
            var subscription = channel.Subscribe<ChatMessage>(_ => count++);

            subscription.Cancel();
            channel.Receive(new ChatMessage());

            Assert.IsFalse(subscription.IsActive);
            Assert.AreEqual(0, count);
        }

        [Test]
        public void Receive_WhenHandlerThrows_RecordsExceptionAndContinuesDispatch()
        {
            var channel = CreateConnectedChannel();
            var count = 0;
            channel.Subscribe<ChatMessage>(_ => throw new InvalidOperationException("handler failed"));
            channel.Subscribe<ChatMessage>(_ => count++);

            channel.Receive(new ChatMessage());

            Assert.AreEqual(1, count);
            Assert.IsNotNull(channel.LastException);
            Assert.AreEqual(NetworkChannelStatus.Connected, channel.Status);
        }

        [Test]
        public void CloseAsync_WhenPendingWaitExists_CancelsWaitAndCloses()
        {
            var channel = CreateConnectedChannel();
            var request = new PingRequest();
            var wait = channel.WaitAsync<PingResponse>(request);

            channel.CloseAsync().GetAwaiter().GetResult();
            var exception = Assert.Throws<NetworkException>(() => wait.GetAwaiter().GetResult());

            Assert.AreEqual(NetworkFailureKind.Canceled, exception.FailureKind);
            Assert.AreEqual(NetworkChannelStatus.Closed, channel.Status);
            Assert.AreEqual(0, channel.PendingResponseCount);
        }

        [Test]
        public void Shutdown_WhenChannelsExist_ClosesChannelsAndClearsRegistry()
        {
            var module = new NetworkModule();
            var channel = CreateChannel(module);
            channel.ConnectAsync().GetAwaiter().GetResult();
            channel.Subscribe<ChatMessage>(_ => { });
            var wait = channel.WaitAsync<PingResponse>(new PingRequest());

            module.Shutdown();
            var exception = Assert.Throws<NetworkException>(() => wait.GetAwaiter().GetResult());

            Assert.AreEqual(NetworkFailureKind.Canceled, exception.FailureKind);
            Assert.IsFalse(module.TryGetChannel("game", out _));
            Assert.AreEqual(NetworkChannelStatus.Closed, channel.Status);
            Assert.AreEqual(0, channel.PendingResponseCount);
            Assert.AreEqual(0, channel.ListenerCount);
        }

        [Test]
        public void SubscribeAndRegister_WhenArgumentIsNull_Throw()
        {
            var channel = CreateConnectedChannel();

            Assert.Throws<ArgumentNullException>(() => channel.Subscribe<ChatMessage>(null));
            Assert.Throws<ArgumentNullException>(() => channel.Subscribe(null));
            Assert.Throws<ArgumentNullException>(() => channel.Register<ChatMessage>(null));
        }

        [Test]
        public void CloseAndShutdown_WhenCalledRepeatedly_AreNoOps()
        {
            var module = new NetworkModule();
            var channel = CreateChannel(module);
            channel.ConnectAsync().GetAwaiter().GetResult();

            Assert.DoesNotThrow(() => channel.CloseAsync().GetAwaiter().GetResult());
            Assert.DoesNotThrow(() => channel.CloseAsync().GetAwaiter().GetResult());
            Assert.DoesNotThrow(() => module.Shutdown());
            Assert.DoesNotThrow(() => module.Shutdown());
        }

        [Test]
        public void TransportReceive_WhenDecodeFails_RecordsErrorAndKeepsChannelUsable()
        {
            var codec = new TestCodec();
            var transport = new TestTransport();
            var channel = CreateConnectedChannel(codec, transport);
            var count = 0;
            channel.Subscribe<ChatMessage>(_ => count++);

            codec.DecodeException = new InvalidOperationException("decode failed");
            transport.Emit(new byte[] { 1 });

            codec.DecodeException = null;
            codec.EnqueueDecode(new ChatMessage());
            transport.Emit(new byte[] { 2 });

            Assert.IsInstanceOf<NetworkException>(channel.LastException);
            Assert.AreEqual(1, count);
            Assert.AreEqual(NetworkChannelStatus.Connected, channel.Status);
        }

        [Test]
        public void SendHttpAsync_WhenUrlIsInvalid_Throws()
        {
            var module = new NetworkModule();

            Assert.Throws<ArgumentNullException>(() => module.SendHttpAsync(new HttpRequest(null)).GetAwaiter().GetResult());
            Assert.Throws<ArgumentException>(() => module.SendHttpAsync(new HttpRequest(" ")).GetAwaiter().GetResult());
            Assert.Throws<ArgumentException>(() => module.SendHttpAsync(new HttpRequest("file:///tmp/test.json")).GetAwaiter().GetResult());
        }

        [UnityTest]
        public IEnumerator SendHttpAsync_WhenGetReturns2xx_ReturnsResponse()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = new NetworkModule();
                var server = new LoopbackHttpServer(200, "get-ok");
                try
                {
                    var response = await module.SendHttpAsync(HttpRequest.Get(server.Url));

                    Assert.AreEqual(200L, response.StatusCode);
                    Assert.AreEqual("get-ok", response.Text);
                    Assert.IsNotNull(response.Headers);
                    Assert.IsTrue(server.WaitForRequest(TimeSpan.FromSeconds(1d)));
                    StringAssert.StartsWith("GET ", server.RequestText);
                }
                finally
                {
                    server.Dispose();
                }
            });
        }

        [UnityTest]
        public IEnumerator SendHttpAsync_WhenPostJsonReturns2xx_SendsBodyAndReturnsResponse()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = new NetworkModule();
                var server = new LoopbackHttpServer(200, "post-ok");
                try
                {
                    var response = await module.SendHttpAsync(HttpRequest.PostJson(server.Url, "{\"ok\":true}"));

                    Assert.AreEqual(200L, response.StatusCode);
                    Assert.AreEqual("post-ok", response.Text);
                    Assert.IsTrue(server.WaitForRequest(TimeSpan.FromSeconds(1d)));
                    StringAssert.StartsWith("POST ", server.RequestText);
                    StringAssert.Contains("{\"ok\":true}", server.RequestText);
                    StringAssert.Contains("application/json", server.RequestText);
                }
                finally
                {
                    server.Dispose();
                }
            });
        }

        [UnityTest]
        public IEnumerator SendHttpAsync_WhenHttpStatusFails_ThrowsNetworkException()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = new NetworkModule();
                var server = new LoopbackHttpServer(500, "server-error");
                try
                {
                    var exception = await AssertThrowsAsync<NetworkException>(async () =>
                    {
                        await module.SendHttpAsync(HttpRequest.Get(server.Url));
                    });

                    Assert.AreEqual(NetworkFailureKind.HttpStatus, exception.FailureKind);
                    Assert.AreEqual(500L, exception.StatusCode);
                }
                finally
                {
                    server.Dispose();
                }
            });
        }

        private static async UniTask<TException> AssertThrowsAsync<TException>(Func<UniTask> action) where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException exception)
            {
                return exception;
            }

            Assert.Fail($"Expected exception of type {typeof(TException).Name}.");
            return null;
        }

        private static NetworkEndpoint CreateEndpoint()
        {
            return new NetworkEndpoint("tcp://127.0.0.1:9000");
        }

        private static NetworkChannel CreateConnectedChannel()
        {
            return CreateConnectedChannel(null, null);
        }

        private static NetworkChannel CreateConnectedChannel(TestCodec codec = null, TestTransport transport = null)
        {
            var channel = CreateChannel(codec: codec, transport: transport);
            channel.ConnectAsync().GetAwaiter().GetResult();
            return channel;
        }

        private static NetworkChannel CreateChannel(NetworkModule module = null, TestCodec codec = null, TestTransport transport = null)
        {
            module ??= new NetworkModule();
            codec ??= new TestCodec();
            transport ??= new TestTransport();
            return module.CreateChannel("game", CreateEndpoint(), codec, transport);
        }

        private sealed class PingRequest : Message
        {
        }

        private sealed class PingResponse : Message
        {
            public override bool IsResponse => true;
        }

        private sealed class MissingOpcodeMessage : Message
        {
        }

        private sealed class ChatMessage : Message
        {
            public string Text;
        }

        private sealed class ChatHandle : MessageHandle<ChatMessage>
        {
            public ChatMessage LastMessage { get; private set; }

            public override void Handle(IChannel channel, ChatMessage message)
            {
                LastMessage = message;
            }
        }

        private sealed class TestCodec : INetworkCodec
        {
            private readonly Queue<Message> m_DecodeQueue = new Queue<Message>();

            public Exception DecodeException { get; set; }

            public byte[] Encode(Message message)
            {
                return new byte[] { 1 };
            }

            public Message Decode(byte[] data)
            {
                if (DecodeException != null)
                {
                    throw DecodeException;
                }

                if (m_DecodeQueue.Count == 0)
                {
                    throw new NetworkException("No decoded message queued.", NetworkFailureKind.Decode);
                }

                return m_DecodeQueue.Dequeue();
            }

            public void EnqueueDecode(Message message)
            {
                m_DecodeQueue.Enqueue(message);
            }
        }

        private sealed class TestTransport : INetworkTransport
        {
            public event Action<byte[]> Received;

            public bool ConnectCalled { get; private set; }

            public bool EchoSentData { get; set; }

            public byte[] LastSentData { get; private set; }

            public Exception ConnectException { get; set; }

            public Exception SendException { get; set; }

            public UniTask ConnectAsync(NetworkEndpoint endpoint)
            {
                ConnectCalled = true;
                if (ConnectException != null)
                {
                    throw ConnectException;
                }

                return UniTask.CompletedTask;
            }

            public UniTask SendAsync(byte[] data)
            {
                LastSentData = data;
                if (SendException != null)
                {
                    throw SendException;
                }

                if (EchoSentData)
                {
                    Received?.Invoke(data);
                }

                return UniTask.CompletedTask;
            }

            public UniTask CloseAsync()
            {
                return UniTask.CompletedTask;
            }

            public void Emit(byte[] data)
            {
                Received?.Invoke(data);
            }

            public void Release()
            {
                Received = null;
            }
        }

        private sealed class LoopbackHttpServer : IDisposable
        {
            private readonly TcpListener m_Listener;
            private readonly Thread m_Thread;
            private readonly int m_StatusCode;
            private readonly string m_ResponseBody;
            private readonly ManualResetEventSlim m_RequestReceived = new ManualResetEventSlim(false);

            public LoopbackHttpServer(int statusCode, string responseBody)
            {
                m_StatusCode = statusCode;
                m_ResponseBody = responseBody;
                m_Listener = new TcpListener(IPAddress.Loopback, 0);
                m_Listener.Start();
                var endpoint = (IPEndPoint)m_Listener.LocalEndpoint;
                Url = $"http://127.0.0.1:{endpoint.Port}/network-test";
                m_Thread = new Thread(Run)
                {
                    IsBackground = true
                };
                m_Thread.Start();
            }

            public string Url { get; }

            public string RequestText { get; private set; }

            public bool WaitForRequest(TimeSpan timeout)
            {
                return m_RequestReceived.Wait(timeout);
            }

            public void Dispose()
            {
                m_Listener.Stop();
                if (m_Thread.IsAlive)
                {
                    m_Thread.Join(100);
                }

                m_RequestReceived.Dispose();
            }

            private void Run()
            {
                try
                {
                    using (var client = m_Listener.AcceptTcpClient())
                    using (var stream = client.GetStream())
                    {
                        RequestText = ReadRequest(stream);
                        m_RequestReceived.Set();
                        WriteResponse(stream);
                    }
                }
                catch (SocketException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }

            private static string ReadRequest(NetworkStream stream)
            {
                var buffer = new byte[4096];
                var builder = new StringBuilder();
                while (true)
                {
                    var count = stream.Read(buffer, 0, buffer.Length);
                    if (count <= 0)
                    {
                        break;
                    }

                    builder.Append(Encoding.UTF8.GetString(buffer, 0, count));
                    var request = builder.ToString();
                    var headerEnd = request.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                    if (headerEnd < 0)
                    {
                        continue;
                    }

                    var contentLength = GetContentLength(request.Substring(0, headerEnd));
                    var bodyLength = Encoding.UTF8.GetByteCount(request.Substring(headerEnd + 4));
                    if (bodyLength >= contentLength)
                    {
                        break;
                    }
                }

                return builder.ToString();
            }

            private static int GetContentLength(string headers)
            {
                var lines = headers.Split(new[] { "\r\n" }, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    if (!line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (int.TryParse(line.Substring("Content-Length:".Length).Trim(), out var contentLength))
                    {
                        return contentLength;
                    }
                }

                return 0;
            }

            private void WriteResponse(NetworkStream stream)
            {
                var body = Encoding.UTF8.GetBytes(m_ResponseBody);
                var reason = m_StatusCode >= 200 && m_StatusCode < 300 ? "OK" : "Error";
                var headers = Encoding.UTF8.GetBytes(
                    $"HTTP/1.1 {m_StatusCode} {reason}\r\n" +
                    "Content-Type: text/plain\r\n" +
                    $"Content-Length: {body.Length}\r\n" +
                    "X-Test: network\r\n" +
                    "Connection: close\r\n\r\n");
                stream.Write(headers, 0, headers.Length);
                stream.Write(body, 0, body.Length);
            }
        }
    }

    [MemoryPackable]
    [Opcode(1001)]
    public partial class RegistryRequest : Message
    {
    }

    [MemoryPackable]
    [Opcode(1002)]
    public partial class RegistryResponse : Message
    {
        public override bool IsResponse => true;
    }
}
