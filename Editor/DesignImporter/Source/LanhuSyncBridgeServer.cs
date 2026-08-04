using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.DesignImporter
{
    internal static class LanhuBridgeInstaller
    {
        private const string ExtensionPath = "Tools/Lanhu/BrowserExtension";

        public static void OpenExtensionFolder()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            var assetPath = GameDeveloperKitEditorPaths.PackageAssetPath(ExtensionPath);
            var absolute = Path.GetFullPath(Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
            EditorGUIUtility.systemCopyBuffer = absolute;
            EditorUtility.RevealInFinder(absolute);
            EditorUtility.DisplayDialog(
                "安装蓝湖同步桥",
                "已打开并复制扩展目录。\n\n在 Chrome/Edge 扩展管理页开启开发者模式，选择“加载已解压的扩展程序”，然后选择该目录。只需安装一次。",
                "确定");
        }
    }

    internal sealed class LanhuSyncBridgeServer : IDisposable
    {
        private const int Port = 18766;
        private const int MaximumBodyBytes = 32 * 1024 * 1024;
        private readonly object m_Gate = new object();
        private readonly CancellationTokenSource m_Stop = new CancellationTokenSource();
        private TcpListener m_Listener;
        private PendingJob m_Pending;

        public async Task<string> RequestManifestAsync(
            LanhuProjectAddress address,
            CancellationToken cancellationToken)
        {
            EnsureStarted();
            var job = new PendingJob
            {
                Id = Guid.NewGuid().ToString("N"),
                Address = address,
                Completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously)
            };
            lock (m_Gate)
            {
                if (m_Pending != null && !m_Pending.Completion.Task.IsCompleted)
                {
                    throw new InvalidOperationException("已有蓝湖同步任务正在运行。");
                }

                m_Pending = job;
            }

            using var registration = cancellationToken.Register(() =>
            {
                job.Completion.TrySetCanceled();
                lock (m_Gate)
                {
                    if (ReferenceEquals(m_Pending, job)) m_Pending = null;
                }
            });
            return await job.Completion.Task;
        }

        public void Dispose()
        {
            m_Stop.Cancel();
            m_Listener?.Stop();
            lock (m_Gate)
            {
                m_Pending?.Completion.TrySetCanceled();
                m_Pending = null;
            }
            m_Stop.Dispose();
        }

        private void EnsureStarted()
        {
            if (m_Listener != null)
            {
                return;
            }

            lock (m_Gate)
            {
                if (m_Listener != null)
                {
                    return;
                }

                try
                {
                    m_Listener = new TcpListener(IPAddress.Loopback, Port);
                    m_Listener.Start(8);
                    _ = Task.Run(AcceptLoopAsync);
                }
                catch (SocketException exception)
                {
                    throw new InvalidOperationException(
                        $"无法启动蓝湖同步桥端口 {Port}，请关闭占用该端口的程序后重试。", exception);
                }
            }
        }

        private async Task AcceptLoopAsync()
        {
            while (!m_Stop.IsCancellationRequested)
            {
                try
                {
                    var client = await m_Listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException) when (m_Stop.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    var request = await ReadRequestAsync(client.GetStream(), m_Stop.Token);
                    var response = Handle(request);
                    await WriteResponseAsync(client.GetStream(), response.Status, response.Body, m_Stop.Token);
                }
                catch (Exception exception)
                {
                    try
                    {
                        await WriteResponseAsync(client.GetStream(), 500, exception.Message, m_Stop.Token);
                    }
                    catch (Exception responseException)
                    {
                        throw new AggregateException(exception, responseException);
                    }
                }
            }
        }

        private Response Handle(Request request)
        {
            if (request.Method == "OPTIONS")
            {
                return new Response(204, string.Empty);
            }

            if (request.Method == "GET" && request.Path == "/gdk-lanhu/jobs/next")
            {
                PendingJob job;
                lock (m_Gate) job = m_Pending;
                if (job == null || job.Completion.Task.IsCompleted)
                {
                    return new Response(204, string.Empty);
                }

                return new Response(200, JsonConvert.SerializeObject(new
                {
                    jobId = job.Id,
                    url = job.Address.Url,
                    projectId = job.Address.ProjectId,
                    teamId = job.Address.TeamId
                }));
            }

            if (request.Method == "POST" &&
                (request.Path == "/gdk-lanhu/complete" || request.Path == "/gdk-lanhu/error"))
            {
                var payload = JObject.Parse(request.Body);
                var jobId = (string)payload["jobId"];
                PendingJob job;
                lock (m_Gate) job = m_Pending;
                if (job == null || !string.Equals(job.Id, jobId, StringComparison.Ordinal))
                {
                    return new Response(409, "同步任务已经结束或不匹配。");
                }

                if (request.Path.EndsWith("/error", StringComparison.Ordinal))
                {
                    job.Completion.TrySetException(new InvalidOperationException(
                        (string)payload["error"] ?? "蓝湖同步失败。"));
                }
                else
                {
                    var manifest = payload["manifest"];
                    if (manifest == null)
                    {
                        job.Completion.TrySetException(new InvalidDataException("蓝湖同步结果缺少 manifest。"));
                    }
                    else
                    {
                        job.Completion.TrySetResult(manifest.ToString(Formatting.None));
                    }
                }

                lock (m_Gate)
                {
                    if (ReferenceEquals(m_Pending, job)) m_Pending = null;
                }
                return new Response(200, "{\"ok\":true}");
            }

            return new Response(404, "Not Found");
        }

        private static async Task<Request> ReadRequestAsync(NetworkStream stream, CancellationToken token)
        {
            var headerBytes = new MemoryStream();
            var marker = 0;
            while (headerBytes.Length < 64 * 1024)
            {
                var next = new byte[1];
                if (await stream.ReadAsync(next, 0, 1, token) == 0)
                {
                    throw new EndOfStreamException();
                }

                headerBytes.WriteByte(next[0]);
                marker = marker switch
                {
                    0 when next[0] == '\r' => 1,
                    1 when next[0] == '\n' => 2,
                    2 when next[0] == '\r' => 3,
                    3 when next[0] == '\n' => 4,
                    _ when next[0] == '\r' => 1,
                    _ => 0
                };
                if (marker == 4) break;
            }

            var header = Encoding.ASCII.GetString(headerBytes.ToArray());
            var lines = header.Split(new[] { "\r\n" }, StringSplitOptions.None);
            var first = lines[0].Split(' ');
            if (first.Length < 2) throw new InvalidDataException("无效 HTTP 请求。");
            var length = 0;
            foreach (var line in lines)
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(line.Substring("Content-Length:".Length).Trim(), out length);
                }
            }

            if (length < 0 || length > MaximumBodyBytes)
            {
                throw new InvalidDataException("蓝湖同步数据超过 32 MB 限制。");
            }

            var body = new byte[length];
            var read = 0;
            while (read < length)
            {
                var count = await stream.ReadAsync(body, read, length - read, token);
                if (count == 0) throw new EndOfStreamException();
                read += count;
            }

            return new Request(first[0].ToUpperInvariant(), first[1], Encoding.UTF8.GetString(body));
        }

        private static async Task WriteResponseAsync(
            NetworkStream stream,
            int status,
            string body,
            CancellationToken token)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
            var statusText = status switch { 200 => "OK", 204 => "No Content", 404 => "Not Found", 409 => "Conflict", _ => "Error" };
            var header = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {status} {statusText}\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "Access-Control-Allow-Private-Network: true\r\n" +
                "Access-Control-Allow-Headers: Content-Type\r\n" +
                "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                "Connection: close\r\n" +
                $"Content-Length: {bodyBytes.Length}\r\n\r\n");
            await stream.WriteAsync(header, 0, header.Length, token);
            if (bodyBytes.Length > 0)
            {
                await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length, token);
            }
        }

        private sealed class PendingJob
        {
            public string Id;
            public LanhuProjectAddress Address;
            public TaskCompletionSource<string> Completion;
        }

        private readonly struct Request
        {
            public Request(string method, string path, string body)
            {
                Method = method;
                Path = path;
                Body = body;
            }

            public string Method { get; }
            public string Path { get; }
            public string Body { get; }
        }

        private readonly struct Response
        {
            public Response(int status, string body)
            {
                Status = status;
                Body = body;
            }

            public int Status { get; }
            public string Body { get; }
        }
    }
}
