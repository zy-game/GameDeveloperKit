using System.Collections.Generic;
using System.Text;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// HTTP 响应描述。
    /// </summary>
    public readonly struct HttpResponse
    {
        public HttpResponse(long statusCode, IReadOnlyDictionary<string, string> headers, byte[] body)
        {
            StatusCode = statusCode;
            Headers = headers;
            Body = body;
        }

        public long StatusCode { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public byte[] Body { get; }

        public string Text => Body == null ? string.Empty : Encoding.UTF8.GetString(Body);
    }
}
