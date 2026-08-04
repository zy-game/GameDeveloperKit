using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using IOFile = System.IO.File;
using IOFileInfo = System.IO.FileInfo;
using IOMemoryStream = System.IO.MemoryStream;

namespace GameDeveloperKit.EditorCloud
{
    public sealed class CloudHttpTransport : ICloudHttpTransport, ICloudHttpReadTransport, IDisposable
    {
        private const int MaximumResponseBodyBytes = 4 * 1024 * 1024;
        private static readonly TimeSpan s_DefaultTimeout = TimeSpan.FromSeconds(100);
        private readonly HttpClient m_Client;
        private readonly TimeSpan m_Timeout;

        public CloudHttpTransport()
            : this(CreateDefaultHandler(), s_DefaultTimeout)
        {
        }

        internal CloudHttpTransport(HttpMessageHandler handler, TimeSpan timeout)
        {
            m_Client = new HttpClient(handler ?? throw new ArgumentNullException(nameof(handler)), true)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            m_Timeout = timeout > TimeSpan.Zero ? timeout : throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        public async UniTask<CloudHttpResponse> SendAsync(
            CloudHttpRequest request,
            CloudObjectUploadRequest upload,
            IProgress<CloudUploadProgress> progress,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (upload == null)
            {
                throw new ArgumentNullException(nameof(upload));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (IOFile.Exists(upload.LocalFilePath) is false)
            {
                throw new CloudException(
                    CloudFailureKind.LocalFile,
                    $"Local upload file does not exist: {upload.LocalFilePath}");
            }

            using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellation.CancelAfter(m_Timeout);
            using var message = new HttpRequestMessage(HttpMethod.Put, request.Uri);
            using var content = new ProgressStreamContent(
                upload,
                progress,
                timeoutCancellation.Token);
            if (string.IsNullOrWhiteSpace(request.ContentType) is false)
            {
                try
                {
                    content.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType);
                }
                catch (FormatException exception)
                {
                    throw new CloudException(
                        CloudFailureKind.InvalidConfiguration,
                        "Cloud upload content type is invalid.",
                        innerException: exception);
                }
            }

            foreach (var header in request.Headers)
            {
                if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (message.Headers.TryAddWithoutValidation(header.Key, header.Value) is false &&
                    content.Headers.TryAddWithoutValidation(header.Key, header.Value) is false)
                {
                    throw new CloudException(
                        CloudFailureKind.InvalidConfiguration,
                        $"Cloud upload header '{header.Key}' is invalid.");
                }
            }

            message.Content = content;
            try
            {
                using var response = await m_Client.SendAsync(
                    message,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutCancellation.Token);
                var headers = ReadHeaders(response);
                var body = await ReadBodyAsync(response.Content, timeoutCancellation.Token);
                return new CloudHttpResponse((int)response.StatusCode, headers, body);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException exception)
            {
                throw new CloudException(
                    CloudFailureKind.Network,
                    "Cloud upload timed out.",
                    innerException: exception);
            }
            catch (HttpRequestException exception)
            {
                throw new CloudException(
                    CloudFailureKind.Network,
                    "Cloud upload failed because of a network error.",
                    innerException: exception);
            }
            catch (IOException exception)
            {
                throw new CloudException(
                    CloudFailureKind.Network,
                    "Cloud upload failed while streaming the local file.",
                    innerException: exception);
            }
        }

        public async UniTask<CloudHttpResponse> SendAsync(
            CloudHttpRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellation.CancelAfter(m_Timeout);
            using var message = new HttpRequestMessage(ToHttpMethod(request.Method), request.Uri);
            foreach (var header in request.Headers)
            {
                if (message.Headers.TryAddWithoutValidation(header.Key, header.Value) is false)
                {
                    throw new CloudException(
                        CloudFailureKind.InvalidConfiguration,
                        $"Cloud {request.Method.ToString().ToUpperInvariant()} header '{header.Key}' is invalid.");
                }
            }

            try
            {
                using var response = await m_Client.SendAsync(
                    message,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutCancellation.Token);
                var headers = ReadHeaders(response);
                var body = await ReadBodyAsync(response.Content, timeoutCancellation.Token);
                return new CloudHttpResponse((int)response.StatusCode, headers, body);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException exception)
            {
                throw new CloudException(
                    CloudFailureKind.Network,
                    $"Cloud {request.Method.ToString().ToUpperInvariant()} timed out.",
                    innerException: exception);
            }
            catch (HttpRequestException exception)
            {
                throw new CloudException(
                    CloudFailureKind.Network,
                    $"Cloud {request.Method.ToString().ToUpperInvariant()} failed because of a network error.",
                    innerException: exception);
            }
        }

        private static HttpMethod ToHttpMethod(CloudHttpMethod method)
        {
            return method switch
            {
                CloudHttpMethod.Get => HttpMethod.Get,
                CloudHttpMethod.Delete => HttpMethod.Delete,
                CloudHttpMethod.Put => HttpMethod.Put,
                _ => throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    $"Cloud HTTP method '{method}' is unsupported.")
            };
        }

        public void Dispose()
        {
            m_Client.Dispose();
        }

        private static HttpMessageHandler CreateDefaultHandler()
        {
            return new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
        }

        private static IReadOnlyDictionary<string, string> ReadHeaders(HttpResponseMessage response)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in response.Headers)
            {
                result[header.Key] = string.Join(",", header.Value);
            }

            if (response.Content != null)
            {
                foreach (var header in response.Content.Headers)
                {
                    result[header.Key] = string.Join(",", header.Value);
                }
            }

            return result;
        }

        private static async Task<string> ReadBodyAsync(
            HttpContent content,
            CancellationToken cancellationToken)
        {
            if (content == null)
            {
                return string.Empty;
            }

            using var stream = await content.ReadAsStreamAsync();
            using var body = new IOMemoryStream();
            var buffer = new byte[8192];
            while (body.Length < MaximumResponseBodyBytes)
            {
                var remaining = (int)Math.Min(buffer.Length, MaximumResponseBodyBytes - body.Length);
                var read = await stream.ReadAsync(buffer, 0, remaining, cancellationToken);
                if (read <= 0)
                {
                    break;
                }

                await body.WriteAsync(buffer, 0, read);
            }

            if (body.Length >= MaximumResponseBodyBytes && stream.ReadByte() >= 0)
            {
                throw new CloudException(
                    CloudFailureKind.ResponseTooLarge,
                    $"Cloud response exceeds {MaximumResponseBodyBytes} bytes.");
            }

            return Encoding.UTF8.GetString(body.ToArray());
        }

        private sealed class ProgressStreamContent : HttpContent
        {
            private const int BufferSize = 64 * 1024;
            private readonly CloudObjectUploadRequest m_Upload;
            private readonly IProgress<CloudUploadProgress> m_Progress;
            private readonly CancellationToken m_CancellationToken;
            private readonly long m_Length;

            public ProgressStreamContent(
                CloudObjectUploadRequest upload,
                IProgress<CloudUploadProgress> progress,
                CancellationToken cancellationToken)
            {
                m_Upload = upload;
                m_Progress = progress;
                m_CancellationToken = cancellationToken;
                m_Length = new IOFileInfo(upload.LocalFilePath).Length;
            }

            protected override async Task SerializeToStreamAsync(
                Stream stream,
                TransportContext context)
            {
                var buffer = new byte[BufferSize];
                long sent = 0;
                using var source = new FileStream(
                    m_Upload.LocalFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    BufferSize,
                    true);
                while (true)
                {
                    m_CancellationToken.ThrowIfCancellationRequested();
                    var read = await source.ReadAsync(
                        buffer,
                        0,
                        buffer.Length,
                        m_CancellationToken);
                    if (read <= 0)
                    {
                        break;
                    }

                    await stream.WriteAsync(
                        buffer,
                        0,
                        read,
                        m_CancellationToken);
                    sent += read;
                    m_Progress?.Report(new CloudUploadProgress(
                        m_Upload.ObjectKey,
                        sent,
                        m_Length,
                        sent,
                        m_Length));
                }
            }

            protected override bool TryComputeLength(out long length)
            {
                length = m_Length;
                return true;
            }
        }
    }
}
