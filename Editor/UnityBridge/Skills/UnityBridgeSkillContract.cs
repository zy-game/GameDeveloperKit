using System.Text;

namespace GameDeveloperKit.UnityBridge
{
    public sealed class UnityBridgeSkillRequest
    {
        public string Method { get; set; }
        public string Path { get; set; }
        public string QueryString { get; set; }
        public string Body { get; set; }
    }

    public sealed class UnityBridgeSkillResponse
    {
        public int StatusCode { get; }
        public string Json { get; }

        private UnityBridgeSkillResponse(int statusCode, string json)
        {
            StatusCode = statusCode;
            Json = json;
        }

        public static UnityBridgeSkillResponse Success(string json)
        {
            return new UnityBridgeSkillResponse(200, json);
        }

        public static UnityBridgeSkillResponse Error(int statusCode, string message)
        {
            return new UnityBridgeSkillResponse(statusCode, $"{{\"success\":false,\"error\":\"{Escape(message)}\",\"code\":{statusCode}}}");
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
