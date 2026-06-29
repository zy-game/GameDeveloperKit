using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnitySelectionSkill : IUnityBridgeSkill
    {
        public string Name => "unity-selection";

        public string Description =>
            "Query and manipulate the Unity Editor selection: get selected objects, set selection, duplicate, delete, frame.";

        public string Trigger =>
            "Use when the user asks about selected objects, wants to select something, duplicate, delete, or frame an object.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("GET", "/selection", "Returns info about currently selected objects."),
            new UnityBridgeSkillEndpoint("POST", "/selection/set", "Sets the selection by asset path or GameObject name.", "{\"paths\":[\"Assets/My.prefab\"],\"names\":[\"Main Camera\"]}"),
            new UnityBridgeSkillEndpoint("POST", "/selection/duplicate", "Duplicates the current selection (Ctrl+D)."),
            new UnityBridgeSkillEndpoint("POST", "/selection/delete", "Deletes the current selection."),
            new UnityBridgeSkillEndpoint("POST", "/selection/frame", "Frames the current selection in the Scene View.")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js selection`",
            "`node scripts/unity-bridge.js selection-set --paths Assets/My.prefab`",
            "`node scripts/unity-bridge.js selection-duplicate`",
            "`node scripts/unity-bridge.js selection-delete`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Selection by path loads assets. Selection by name finds scene GameObjects.",
            "Deleting the selection cannot be undone via the CLI; use with caution."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return (request.Method == "GET" && path == "selection")
                || (request.Method == "POST" && path == "selection/set")
                || (request.Method == "POST" && path == "selection/duplicate")
                || (request.Method == "POST" && path == "selection/delete")
                || (request.Method == "POST" && path == "selection/frame");
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');

            return path switch
            {
                "selection" => GetSelection(),
                "selection/set" => SetSelection(request),
                "selection/duplicate" => Duplicate(),
                "selection/delete" => Delete(),
                "selection/frame" => Frame(),
                _ => UnityBridgeSkillResponse.Error(404, $"Unknown endpoint: /{path}")
            };
        }

        private static UnityBridgeSkillResponse GetSelection()
        {
            var objects = Selection.objects;
            if (objects == null || objects.Length == 0)
            {
                return UnityBridgeSkillResponse.Success("{\"objects\":[],\"count\":0}");
            }

            var parts = objects.Select(o =>
            {
                var path = AssetDatabase.GetAssetPath(o);
                return "{"
                    + $"\"name\":\"{Esc(o.name)}\","
                    + $"\"type\":\"{Esc(o.GetType().Name)}\","
                    + $"\"path\":\"{Esc(string.IsNullOrEmpty(path) ? "(scene)" : path)}\","
                    + $"\"instanceId\":{o.GetInstanceID()}"
                    + "}";
            }).ToArray();

            return UnityBridgeSkillResponse.Success($"{{\"objects\":[{string.Join(",", parts)}],\"count\":{parts.Length}}}");
        }

        private static UnityBridgeSkillResponse SetSelection(UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            if (body == null)
                return UnityBridgeSkillResponse.Error(400, "Invalid request body");

            var selected = new List<UnityEngine.Object>();

            if (body.TryGetValue("paths", out var pathsJson))
            {
                var pathList = ParseStringArray(pathsJson);
                foreach (var p in pathList)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p);
                    if (asset != null) selected.Add(asset);
                }
            }

            if (body.TryGetValue("names", out var namesJson))
            {
                var nameList = ParseStringArray(namesJson);
                var allGo = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (var n in nameList)
                {
                    var go = allGo.FirstOrDefault(g => string.Equals(g.name, n, StringComparison.OrdinalIgnoreCase));
                    if (go != null) selected.Add(go);
                }
            }

            if (selected.Count == 0)
                return UnityBridgeSkillResponse.Error(404, "No objects found matching the specified paths/names");

            Selection.objects = selected.ToArray();
            return UnityBridgeSkillResponse.Success($"{{\"set\":true,\"count\":{selected.Count}}}");
        }

        private static UnityBridgeSkillResponse Duplicate()
        {
            if (Selection.objects == null || Selection.objects.Length == 0)
                return UnityBridgeSkillResponse.Error(400, "No objects selected");

            EditorApplication.ExecuteMenuItem("Edit/Duplicate");
            return UnityBridgeSkillResponse.Success($"{{\"duplicated\":true,\"count\":{Selection.objects.Length}}}");
        }

        private static UnityBridgeSkillResponse Delete()
        {
            if (Selection.objects == null || Selection.objects.Length == 0)
                return UnityBridgeSkillResponse.Error(400, "No objects selected");

            var count = Selection.objects.Length;
            EditorApplication.ExecuteMenuItem("Edit/Delete");
            return UnityBridgeSkillResponse.Success($"{{\"deleted\":true,\"count\":{count}}}");
        }

        private static UnityBridgeSkillResponse Frame()
        {
            if (Selection.objects == null || Selection.objects.Length == 0)
                return UnityBridgeSkillResponse.Error(400, "No objects selected");

            EditorApplication.ExecuteMenuItem("Edit/Frame Selected");
            return UnityBridgeSkillResponse.Success("{\"framed\":true}");
        }

        private static List<string> ParseStringArray(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            // Support both JSON array ["a","b"] and comma-separated "a,b"
            if (json.TrimStart().StartsWith("["))
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<ArrayWrapper>($"{{\"items\":{json}}}");
                    return wrapper?.items?.ToList() ?? new List<string>();
                }
                catch { }
            }
            return json.Split(',').Select(s => s.Trim().Trim('"')).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }

        private static Dictionary<string, string> ParseBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                var obj = JsonUtility.FromJson<SelectionBody>(body);
                if (obj == null) return null;
                var d = new Dictionary<string, string>();
                if (obj.paths != null) d["paths"] = obj.paths;
                if (obj.names != null) d["names"] = obj.names;
                return d;
            }
            catch { return null; }
        }

        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        [Serializable]
        private class ArrayWrapper { public string[] items; }

        [Serializable]
        private class SelectionBody
        {
            public string paths;
            public string names;
        }
    }
}
