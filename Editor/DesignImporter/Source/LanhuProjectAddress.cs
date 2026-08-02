using System;
using System.Collections.Generic;

namespace GameDeveloperKit.DesignImporter
{
    internal readonly struct LanhuProjectAddress
    {
        public LanhuProjectAddress(string url, string projectId, string teamId)
        {
            Url = url;
            ProjectId = projectId;
            TeamId = teamId;
        }

        public string Url { get; }
        public string ProjectId { get; }
        public string TeamId { get; }

        public static LanhuProjectAddress Parse(string value)
        {
            if (!Uri.TryCreate((value ?? string.Empty).Trim(), UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                uri.Host.IndexOf("lanhuapp.com", StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new ArgumentException("请输入有效的蓝湖项目 URL。", nameof(value));
            }

            var query = uri.Query.TrimStart('?');
            var fragment = uri.Fragment.TrimStart('#');
            var question = fragment.IndexOf('?');
            if (question >= 0)
            {
                query += "&" + fragment.Substring(question + 1);
            }

            var values = ParseQuery(query);
            values.TryGetValue("pid", out var projectId);
            if (string.IsNullOrWhiteSpace(projectId))
            {
                values.TryGetValue("project_id", out projectId);
            }

            values.TryGetValue("teamId", out var teamId);
            if (string.IsNullOrWhiteSpace(teamId))
            {
                values.TryGetValue("tid", out teamId);
            }

            if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(teamId))
            {
                throw new ArgumentException("蓝湖 URL 缺少 pid 或 teamId/tid。", nameof(value));
            }

            return new LanhuProjectAddress(uri.AbsoluteUri, projectId, teamId);
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in (query ?? string.Empty).Split('&'))
            {
                if (string.IsNullOrWhiteSpace(pair))
                {
                    continue;
                }

                var separator = pair.IndexOf('=');
                var key = Uri.UnescapeDataString(separator < 0 ? pair : pair.Substring(0, separator));
                var value = Uri.UnescapeDataString(separator < 0 ? string.Empty : pair.Substring(separator + 1));
                result[key] = value;
            }

            return result;
        }
    }
}
