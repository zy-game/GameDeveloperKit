using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityMenuCommandSkill : IUnityBridgeSkill
    {
        public string Name => "unity-menu-command";

        public string Description =>
            "Execute Unity Editor menu commands by menu path.";

        public string Trigger =>
            "Use when the user asks to run a Unity menu item, open a tool window, or trigger editor automation.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("POST", "/execute",
                "Executes a Unity Editor menu command.",
                "{\"menuPath\":\"GameDeveloperKit/Unity Bridge\"}")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js execute --menuPath \"GameDeveloperKit/Unity Bridge\"`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Prefer menu commands over direct code evaluation when available."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            return request.Method == "POST" && request.Path.Trim('/') == "execute";
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            var body = DeserializeBody(request.Body);
            if (body == null || !body.ContainsKey("menuPath"))
            {
                return UnityBridgeSkillResponse.Error(400, "Missing 'menuPath' field in request body");
            }

            var menuPath = body["menuPath"];
            if (string.IsNullOrWhiteSpace(menuPath))
            {
                return UnityBridgeSkillResponse.Error(400, "'menuPath' is empty");
            }

            var executed = EditorApplication.ExecuteMenuItem(menuPath);
            if (executed)
            {
                return UnityBridgeSkillResponse.Success(
                    $"{{\"executed\":true,\"menuPath\":\"{Esc(menuPath)}\"}}");
            }

            return UnityBridgeSkillResponse.Error(404,
                $"Menu item not found or could not be executed: {menuPath}");
        }

        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static Dictionary<string, string> DeserializeBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;

            try
            {
                return JsonUtility.FromJson<SimpleBody>(body)?.ToDictionary();
            }
            catch
            {
                return null;
            }
        }

        [Serializable]
        private class SimpleBody
        {
            public string menuPath;

            public Dictionary<string, string> ToDictionary()
            {
                var d = new Dictionary<string, string>();
                if (menuPath != null) d["menuPath"] = menuPath;
                return d;
            }
        }
    }
}
