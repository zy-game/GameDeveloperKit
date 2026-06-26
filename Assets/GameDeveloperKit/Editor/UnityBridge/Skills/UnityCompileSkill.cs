using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityCompileSkill : IUnityBridgeSkill
    {
        public string Name => "unity-compile";

        public string Description =>
            "Get compilation status including error/warning counts, and trigger a project refresh that may cause recompilation.";

        public string Trigger =>
            "Use when the user asks whether Unity is compiling, what the compile errors are, how many errors exist, or wants to trigger a recompile.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("GET", "/compile", "Returns current compilation status: compiling, error count, warning count."),
            new UnityBridgeSkillEndpoint("POST", "/compile", "Triggers AssetDatabase.Refresh() which may cause recompilation if scripts have changed.")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js compile`",
            "`node scripts/unity-bridge.js compile-trigger`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Unity does not have a direct 'recompile' API. AssetDatabase.Refresh() will trigger compilation if scripts changed."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return path == "compile";
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            if (request.Method == "GET")
                return GetCompileStatus();

            if (request.Method == "POST")
                return TriggerRefresh();

            return UnityBridgeSkillResponse.Error(405, "Method not allowed");
        }

        private static UnityBridgeSkillResponse GetCompileStatus()
        {
            var isCompiling = EditorApplication.isCompiling;
            var errorCount = 0;
            var warningCount = 0;
            var errors = new List<string>();
            var warnings = new List<string>();

            // We can't directly access compile errors from Editor API easily.
            // But we can check the console capture for recent compile errors.
            var logs = UnityBridgeConsoleCapture.GetLogs(null, 50);

            foreach (var log in logs)
            {
                if (log.Type == LogType.Error || log.Type == LogType.Exception)
                {
                    errorCount++;
                    if (errors.Count < 10)
                        errors.Add(log.Message);
                }
                else if (log.Type == LogType.Warning)
                {
                    warningCount++;
                    if (warnings.Count < 10)
                        warnings.Add(log.Message);
                }
            }

            var errorParts = errors.Select(e => $"\"{Esc(e)}\"").ToArray();
            var warnParts = warnings.Select(w => $"\"{Esc(w)}\"").ToArray();

            return UnityBridgeSkillResponse.Success(
                $"{{\"isCompiling\":{(isCompiling ? "true" : "false")},\"errorCount\":{errorCount},\"warningCount\":{warningCount},\"errors\":[{string.Join(",", errorParts)}],\"warnings\":[{string.Join(",", warnParts)}]}}");
        }

        private static UnityBridgeSkillResponse TriggerRefresh()
        {
            AssetDatabase.Refresh();
            return UnityBridgeSkillResponse.Success(
                $"{{\"refreshed\":true,\"isCompiling\":{(EditorApplication.isCompiling ? "true" : "false")}}}");
        }

        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }
}
