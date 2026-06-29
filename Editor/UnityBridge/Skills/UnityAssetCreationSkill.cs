using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SysIO = System.IO;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityAssetCreationSkill : IUnityBridgeSkill
    {
        public string Name => "unity-asset-creation";

        public string Description =>
            "Create Unity assets: prefabs, ScriptableObjects, Materials, Folders.";

        public string Trigger =>
            "Use when the user asks to create prefabs, ScriptableObject assets, folders, materials, or other project assets.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("POST", "/create/prefab",
                "Creates a prefab from a source GameObject or current selection.",
                "{\"sourcePath\":\"Assets/Source.prefab\",\"targetPath\":\"Assets/Prefabs/My.prefab\"}"),
            new UnityBridgeSkillEndpoint("POST", "/create/scriptableobject",
                "Creates a ScriptableObject asset by type name.",
                "{\"typeName\":\"MySettings\",\"path\":\"Assets/Settings/My.asset\"}"),
            new UnityBridgeSkillEndpoint("POST", "/create/asset",
                "Creates a generic asset. Supported: Material, Prefab, Folder.",
                "{\"assetType\":\"Material\",\"path\":\"Assets/Mat.mat\"}")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js create-scriptableobject --typeName MySettings --path Assets/S.asset`",
            "`node scripts/unity-bridge.js create-prefab --path Assets/Prefabs/P.prefab`",
            "`node scripts/unity-bridge.js create-asset --assetType Folder --path Assets/New`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Use project-relative 'Assets/...' paths.",
            "Avoid overwriting existing assets unless the user explicitly asks."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return request.Method == "POST"
                && (path == "create/prefab" || path == "create/scriptableobject" || path == "create/asset");
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');

            switch (path)
            {
                case "create/prefab":
                    return HandleCreatePrefab(request);
                case "create/scriptableobject":
                    return HandleCreateScriptableObject(request);
                case "create/asset":
                    return HandleCreateAsset(request);
                default:
                    return UnityBridgeSkillResponse.Error(404, $"Unknown path: /{path}");
            }
        }

        private UnityBridgeSkillResponse HandleCreatePrefab(UnityBridgeSkillRequest request)
        {
            var body = ParseSimpleBody(request.Body);
            if (body == null || !body.TryGetValue("targetPath", out var targetPath) || string.IsNullOrWhiteSpace(targetPath))
            {
                return UnityBridgeSkillResponse.Error(400, "Missing 'targetPath' field");
            }

            body.TryGetValue("sourcePath", out var sourcePath);

            GameObject source = null;
            if (!string.IsNullOrWhiteSpace(sourcePath))
            {
                source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                if (source == null)
                    return UnityBridgeSkillResponse.Error(404, $"Source not found: {sourcePath}");
            }
            else if (Selection.activeGameObject != null)
            {
                source = Selection.activeGameObject;
            }

            if (source == null)
            {
                return UnityBridgeSkillResponse.Error(400,
                    "No source GameObject. Provide 'sourcePath' or select a GameObject in the scene.");
            }

            if (!targetPath.EndsWith(".prefab")) targetPath += ".prefab";

            try
            {
                EnsureDirectory(targetPath);
                PrefabUtility.SaveAsPrefabAsset(source, targetPath);
                AssetDatabase.Refresh();
                return UnityBridgeSkillResponse.Success(
                    $"{{\"created\":true,\"path\":\"{Esc(targetPath)}\"}}");
            }
            catch (Exception ex)
            {
                return UnityBridgeSkillResponse.Error(500, $"Failed: {ex.Message}");
            }
        }

        private UnityBridgeSkillResponse HandleCreateScriptableObject(UnityBridgeSkillRequest request)
        {
            var body = ParseSimpleBody(request.Body);
            if (body == null
                || !body.TryGetValue("typeName", out var typeName) || string.IsNullOrWhiteSpace(typeName)
                || !body.TryGetValue("path", out var path) || string.IsNullOrWhiteSpace(path))
            {
                return UnityBridgeSkillResponse.Error(400, "Missing 'typeName' or 'path'");
            }

            var type = ResolveType(typeName);
            if (type == null)
                return UnityBridgeSkillResponse.Error(404, $"Type not found: {typeName}");

            if (!typeof(ScriptableObject).IsAssignableFrom(type))
                return UnityBridgeSkillResponse.Error(400, $"{typeName} is not a ScriptableObject");

            if (!path.EndsWith(".asset")) path += ".asset";

            try
            {
                EnsureDirectory(path);
                var instance = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(instance, path);
                AssetDatabase.Refresh();
                return UnityBridgeSkillResponse.Success(
                    $"{{\"created\":true,\"path\":\"{Esc(path)}\",\"type\":\"{Esc(typeName)}\"}}");
            }
            catch (Exception ex)
            {
                return UnityBridgeSkillResponse.Error(500, $"Failed: {ex.Message}");
            }
        }

        private UnityBridgeSkillResponse HandleCreateAsset(UnityBridgeSkillRequest request)
        {
            var body = ParseSimpleBody(request.Body);
            if (body == null || !body.TryGetValue("path", out var path) || string.IsNullOrWhiteSpace(path))
            {
                return UnityBridgeSkillResponse.Error(400, "Missing 'path' field");
            }

            body.TryGetValue("assetType", out var assetType);
            assetType = assetType ?? "";

            try
            {
                EnsureDirectory(path);

                if (assetType.Equals("Material", StringComparison.OrdinalIgnoreCase))
                {
                    var mat = new Material(Shader.Find("Standard"));
                    AssetDatabase.CreateAsset(mat, path);
                }
                else if (assetType.Equals("Folder", StringComparison.OrdinalIgnoreCase))
                {
                    SysIO.Directory.CreateDirectory(path);
                    AssetDatabase.Refresh();
                }
                else if (assetType.Equals("Prefab", StringComparison.OrdinalIgnoreCase))
                {
                    var go = new GameObject(SysIO.Path.GetFileNameWithoutExtension(path));
                    PrefabUtility.SaveAsPrefabAsset(go, path);
                    UnityEngine.Object.DestroyImmediate(go);
                }
                else
                {
                    return UnityBridgeSkillResponse.Error(400,
                        $"Unsupported assetType: {assetType}. Supported: Material, Prefab, Folder");
                }

                AssetDatabase.Refresh();
                return UnityBridgeSkillResponse.Success(
                    $"{{\"created\":true,\"path\":\"{Esc(path)}\",\"type\":\"{Esc(assetType)}\"}}");
            }
            catch (Exception ex)
            {
                return UnityBridgeSkillResponse.Error(500, $"Failed: {ex.Message}");
            }
        }

        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static void EnsureDirectory(string assetPath)
        {
            var dir = SysIO.Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrWhiteSpace(dir) && !SysIO.Directory.Exists(dir))
            {
                SysIO.Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }
        }

        private static Type ResolveType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            var matches = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Type.EmptyTypes; }
                })
                .Where(t => t != null && (t.FullName == typeName || t.Name == typeName))
                .ToArray();

            return matches.Length >= 1 ? matches[0] : null;
        }

        private static Dictionary<string, string> ParseSimpleBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                var obj = JsonUtility.FromJson<SimpleAssetBody>(body);
                if (obj == null) return null;
                var d = new Dictionary<string, string>();
                if (obj.sourcePath != null) d["sourcePath"] = obj.sourcePath;
                if (obj.targetPath != null) d["targetPath"] = obj.targetPath;
                if (obj.typeName != null) d["typeName"] = obj.typeName;
                if (obj.path != null) d["path"] = obj.path;
                if (obj.assetType != null) d["assetType"] = obj.assetType;
                return d;
            }
            catch
            {
                return null;
            }
        }

        [Serializable]
        private class SimpleAssetBody
        {
            public string sourcePath;
            public string targetPath;
            public string typeName;
            public string path;
            public string assetType;
        }
    }
}
