using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityProjectSkill : IUnityBridgeSkill
    {
        public string Name => "unity-project";

        public string Description =>
            "Project-wide operations: refresh AssetDatabase, search/find assets, delete/move assets, list packages, import assets.";

        public string Trigger =>
            "Use when the user asks to refresh the project, search for assets, delete or move assets, check installed packages, or perform project-level operations.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("POST", "/project/refresh", "Calls AssetDatabase.Refresh()."),
            new UnityBridgeSkillEndpoint("GET", "/project/assets?type=Script&search=MyClass", "Searches assets by type and/or name."),
            new UnityBridgeSkillEndpoint("GET", "/project/packages", "Lists installed Unity packages."),
            new UnityBridgeSkillEndpoint("POST", "/project/delete", "Deletes an asset by path.", "{\"path\":\"Assets/Old/File.asset\"}"),
            new UnityBridgeSkillEndpoint("POST", "/project/move", "Moves/renames an asset.", "{\"source\":\"Assets/Old.txt\",\"dest\":\"Assets/New.txt\"}"),
            new UnityBridgeSkillEndpoint("GET", "/project/info", "Returns project root path and product name.")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js project-refresh`",
            "`node scripts/unity-bridge.js project-assets --type Script --search Player`",
            "`node scripts/unity-bridge.js project-packages`",
            "`node scripts/unity-bridge.js project-delete --path Assets/Temp.asset`",
            "`node scripts/unity-bridge.js project-move --source Assets/A --dest Assets/B`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Asset search type can be: Script, Texture, Material, Prefab, Scene, Audio, Folder, or empty for all.",
            "Deleting assets is irreversible; consider using Unity's undo instead."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return (request.Method == "POST" && path == "project/refresh")
                || (request.Method == "GET" && path == "project/assets")
                || (request.Method == "GET" && path == "project/packages")
                || (request.Method == "POST" && path == "project/delete")
                || (request.Method == "POST" && path == "project/move")
                || (request.Method == "GET" && path == "project/info");
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');

            return path switch
            {
                "project/refresh" => Refresh(),
                "project/assets" => SearchAssets(request),
                "project/packages" => ListPackages(),
                "project/delete" => DeleteAsset(request),
                "project/move" => MoveAsset(request),
                "project/info" => ProjectInfo(),
                _ => UnityBridgeSkillResponse.Error(404, $"Unknown endpoint: /{path}")
            };
        }

        private static UnityBridgeSkillResponse Refresh()
        {
            AssetDatabase.Refresh();
            return UnityBridgeSkillResponse.Success("{\"refreshed\":true}");
        }

        private static UnityBridgeSkillResponse SearchAssets(UnityBridgeSkillRequest request)
        {
            var query = ParseQuery(request.QueryString);
            var typeFilter = query.GetValueOrDefault("type", "");
            var search = query.GetValueOrDefault("search", "");
            var countStr = query.GetValueOrDefault("count", "50");

            if (!int.TryParse(countStr, out var maxCount) || maxCount <= 0) maxCount = 50;
            maxCount = Math.Min(maxCount, 200);

            var filter = string.IsNullOrWhiteSpace(search) ? "t:" + MapAssetType(typeFilter) : $"{search} t:{MapAssetType(typeFilter)}";
            var guids = AssetDatabase.FindAssets(filter);
            var results = guids.Take(maxCount)
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => $"{{\"path\":\"{Esc(p)}\",\"type\":\"{Esc(AssetDatabase.GetMainAssetTypeAtPath(p)?.Name ?? "")}\"}}")
                .ToArray();

            return UnityBridgeSkillResponse.Success($"{{\"assets\":[{string.Join(",", results)}],\"count\":{results.Length}}}");
        }

        private static UnityBridgeSkillResponse ListPackages()
        {
            var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            var parts = packages.Select(p => "{"
                + $"\"name\":\"{Esc(p.name)}\","
                + $"\"version\":\"{Esc(p.version)}\","
                + $"\"source\":\"{Esc(p.source.ToString())}\""
                + "}").ToArray();

            return UnityBridgeSkillResponse.Success($"{{\"packages\":[{string.Join(",", parts)}],\"count\":{parts.Length}}}");
        }

        private static UnityBridgeSkillResponse DeleteAsset(UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var assetPath = body?.GetValueOrDefault("path", "");
            if (string.IsNullOrWhiteSpace(assetPath))
                return UnityBridgeSkillResponse.Error(400, "Missing 'path' field");

            if (!AssetDatabase.DeleteAsset(assetPath))
                return UnityBridgeSkillResponse.Error(404, $"Asset not found or could not be deleted: {assetPath}");

            return UnityBridgeSkillResponse.Success($"{{\"deleted\":true,\"path\":\"{Esc(assetPath)}\"}}");
        }

        private static UnityBridgeSkillResponse MoveAsset(UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var source = body?.GetValueOrDefault("source", "");
            var dest = body?.GetValueOrDefault("dest", "");
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(dest))
                return UnityBridgeSkillResponse.Error(400, "Missing 'source' or 'dest' field");

            var error = AssetDatabase.MoveAsset(source, dest);
            if (!string.IsNullOrWhiteSpace(error))
                return UnityBridgeSkillResponse.Error(500, $"Move failed: {error}");

            AssetDatabase.Refresh();
            return UnityBridgeSkillResponse.Success($"{{\"moved\":true,\"source\":\"{Esc(source)}\",\"dest\":\"{Esc(dest)}\"}}");
        }

        private static UnityBridgeSkillResponse ProjectInfo()
        {
            var json = "{"
                + $"\"dataPath\":\"{Esc(Application.dataPath)}\","
                + $"\"productName\":\"{Esc(Application.productName)}\","
                + $"\"unityVersion\":\"{Esc(Application.unityVersion)}\","
                + $"\"platform\":\"{Esc(Application.platform.ToString())}\""
                + "}";
            return UnityBridgeSkillResponse.Success(json);
        }

        private static string MapAssetType(string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return "";
            return type.ToLowerInvariant() switch
            {
                "script" => "MonoScript",
                "texture" => "Texture2D",
                "material" => "Material",
                "prefab" => "Prefab",
                "scene" => "Scene",
                "audio" => "AudioClip",
                "folder" => "Folder",
                "animator" => "AnimatorController",
                "animation" => "AnimationClip",
                "shader" => "Shader",
                _ => type
            };
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(query)) return result;
            query = query.TrimStart('?');
            foreach (var pair in query.Split('&'))
            {
                var parts = pair.Split('=');
                if (parts.Length == 2)
                    result[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
            }
            return result;
        }

        private static Dictionary<string, string> ParseBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                var obj = JsonUtility.FromJson<ProjectBody>(body);
                if (obj == null) return null;
                var d = new Dictionary<string, string>();
                if (obj.path != null) d["path"] = obj.path;
                if (obj.source != null) d["source"] = obj.source;
                if (obj.dest != null) d["dest"] = obj.dest;
                return d;
            }
            catch { return null; }
        }

        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        [Serializable]
        private class ProjectBody
        {
            public string path;
            public string source;
            public string dest;
        }
    }
}
