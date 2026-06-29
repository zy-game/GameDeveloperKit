using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityConsoleSkill : IUnityBridgeSkill
    {
        public string Name => "unity-console";

        public string Description =>
            "Read recent Unity Console logs with optional level filtering.";

        public string Trigger =>
            "Use when the user asks for Unity errors, warnings, console output, stack traces, or compilation logs.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("GET", "/console?level=error&count=50",
                "Returns recent Console entries. level: Error, Warning, Log, or empty for all.")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js console --level error --count 20`",
            "`node scripts/unity-bridge.js console --count 100`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Logs are captured in a ring buffer after UnityBridge starts."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            return request.Path.Trim('/') == "console";
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            var query = ParseQuery(request.QueryString);
            var levelStr = query.TryGetValue("level", out var lv) ? lv : null;
            var countStr = query.TryGetValue("count", out var ct) ? ct : "50";

            LogType? levelFilter = null;
            if (!string.IsNullOrWhiteSpace(levelStr)
                && Enum.TryParse<LogType>(levelStr, true, out var parsed))
            {
                levelFilter = parsed;
            }

            if (!int.TryParse(countStr, out var count) || count <= 0)
            {
                count = 50;
            }

            count = Math.Min(count, 500);

            var logs = UnityBridgeConsoleCapture.GetLogs(levelFilter, count);

            var entries = new List<string>(logs.Count);
            foreach (var log in logs)
            {
                entries.Add("{"
                    + $"\"message\":\"{Esc(log.Message)}\","
                    + $"\"stackTrace\":\"{Esc(log.StackTrace ?? "")}\","
                    + $"\"type\":\"{log.Type}\","
                    + $"\"timestamp\":\"{log.Timestamp:O}\""
                    + "}");
            }

            return UnityBridgeSkillResponse.Success(
                $"{{\"logs\":[{string.Join(",", entries)}],\"count\":{entries.Count}}}");
        }

        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(query)) return result;

            query = query.TrimStart('?');
            foreach (var pair in query.Split('&'))
            {
                var parts = pair.Split('=');
                if (parts.Length == 2)
                {
                    result[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
                }
            }

            return result;
        }
    }
}
