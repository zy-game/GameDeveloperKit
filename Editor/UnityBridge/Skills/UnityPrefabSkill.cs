using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityPrefabSkill : IUnityBridgeSkill
    {
        public string Name => "unity-prefab";

        public string Description =>
            "Advanced prefab operations: open prefab in isolation mode, save, check overrides, unpack, create variants, reconnect broken instances.";

        public string Trigger =>
            "Use when the user wants to edit prefabs in isolation, save prefab changes, check for overrides, unpack prefab instances, or manage prefab variants.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("GET", "/prefab/info?target=Cube", "Returns prefab info for a GameObject (prefab path, overrides)."),
            new UnityBridgeSkillEndpoint("POST", "/prefab/open", "Opens a prefab asset in isolation (Prefab Stage).",
                "{\"path\":\"Assets/Prefabs/MyPrefab.prefab\"}"),
            new UnityBridgeSkillEndpoint("POST", "/prefab/save", "Saves the currently open prefab stage and exits isolation."),
            new UnityBridgeSkillEndpoint("POST", "/prefab/apply", "Applies GameObject overrides to its prefab asset.",
                "{\"target\":\"Cube\"}"),
            new UnityBridgeSkillEndpoint("POST", "/prefab/unpack", "Unpacks a prefab instance into a normal GameObject.",
                "{\"target\":\"Cube\",\"mode\":\"Completely\"}"),
            new UnityBridgeSkillEndpoint("POST", "/prefab/connect", "Connects a GameObject to an existing prefab asset.",
                "{\"target\":\"NewCube\",\"prefabPath\":\"Assets/Prefabs/Template.prefab\"}")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js prefab-info --target Cube`",
            "`node scripts/unity-bridge.js prefab-open --path Assets/Prefabs/Player.prefab`",
            "`node scripts/unity-bridge.js prefab-apply --target Cube`",
            "`node scripts/unity-bridge.js prefab-unpack --target Cube --mode Completely`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "PrefabStage operations affect the currently open prefab in isolation mode. Only one stage can be open at a time.",
            "Unpack modes: Completely (remove prefab link), OutermostRoot (keep nested prefabs)."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var p = request.Path.Trim('/');
            return (request.Method == "GET" && p == "prefab/info")
                || (request.Method == "POST" && (p == "prefab/open" || p == "prefab/save" || p == "prefab/apply"
                    || p == "prefab/unpack" || p == "prefab/connect"));
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            return request.Path.Trim('/') switch
            {
                "prefab/info" => GetPrefabInfo(request),
                "prefab/open" => OpenPrefab(request),
                "prefab/save" => SavePrefab(),
                "prefab/apply" => ApplyOverrides(request),
                "prefab/unpack" => Unpack(request),
                "prefab/connect" => Connect(request),
                _ => UnityBridgeSkillResponse.Error(404, "Unknown")
            };
        }

        private static UnityBridgeSkillResponse GetPrefabInfo(UnityBridgeSkillRequest request)
        {
            var q = ParseQuery(request.QueryString);
            var target = q.GetValueOrDefault("target", "");
            var go = FindGo(target);
            if (go == null) return UnityBridgeSkillResponse.Error(404, $"GameObject not found: {target}");

            var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            var prefabType = PrefabUtility.GetPrefabInstanceStatus(go);
            var isPrefab = PrefabUtility.IsPartOfPrefabInstance(go) || PrefabUtility.IsPartOfPrefabAsset(go);
            var overrides = PrefabUtility.GetObjectOverrides(go).Select(o =>
                $"{{\"instanceObject\":\"{Esc(o.instanceObject?.name ?? "")}\"}}").ToArray();

            var pfPath = string.IsNullOrEmpty(prefabPath) ? "(none)" : prefabPath;
            var overridesJoined = string.Join(",", overrides);
            return UnityBridgeSkillResponse.Success(
                "{\"target\":\"" + Esc(target) + "\",\"prefabPath\":\"" + Esc(pfPath) + "\",\"prefabType\":\"" + prefabType + "\",\"isPartOfPrefab\":" + (isPrefab ? "true" : "false") + ",\"overrides\":[" + overridesJoined + "]}");
        }

        private static UnityBridgeSkillResponse OpenPrefab(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var path = b?.GetValueOrDefault("path", "");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return UnityBridgeSkillResponse.Error(404, $"Prefab not found: {path}");

            var stage = PrefabStageUtility.OpenPrefab(path);
            if (stage != null)
                return UnityBridgeSkillResponse.Success($"{{\"opened\":true,\"path\":\"{Esc(path)}\"}}");
            return UnityBridgeSkillResponse.Error(500, "Failed to open prefab stage");
        }

        private static UnityBridgeSkillResponse SavePrefab()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null) return UnityBridgeSkillResponse.Error(400, "No prefab stage is currently open");
            PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath);
            StageUtility.GoBackToPreviousStage();
            return UnityBridgeSkillResponse.Success($"{{\"saved\":true,\"path\":\"{Esc(stage.assetPath)}\"}}");
        }

        private static UnityBridgeSkillResponse ApplyOverrides(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var target = b?.GetValueOrDefault("target", "");
            var go = FindGo(target);
            if (go == null) return UnityBridgeSkillResponse.Error(404, $"GameObject not found: {target}");
            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.AutomatedAction);
            return UnityBridgeSkillResponse.Success($"{{\"applied\":true,\"target\":\"{Esc(target)}\"}}");
        }

        private static UnityBridgeSkillResponse Unpack(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var target = b?.GetValueOrDefault("target", "");
            var modeStr = b?.GetValueOrDefault("mode", "Completely");
            var go = FindGo(target);
            if (go == null) return UnityBridgeSkillResponse.Error(404, $"GameObject not found: {target}");

            var mode = modeStr.Equals("OutermostRoot", StringComparison.OrdinalIgnoreCase)
                ? PrefabUnpackMode.OutermostRoot : PrefabUnpackMode.Completely;
            PrefabUtility.UnpackPrefabInstance(go, mode, InteractionMode.AutomatedAction);
            return UnityBridgeSkillResponse.Success($"{{\"unpacked\":true,\"target\":\"{Esc(target)}\",\"mode\":\"{Esc(modeStr)}\"}}");
        }

        private static UnityBridgeSkillResponse Connect(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var target = b?.GetValueOrDefault("target", "");
            var prefabPath = b?.GetValueOrDefault("prefabPath", "");
            var go = FindGo(target);
            if (go == null) return UnityBridgeSkillResponse.Error(404, $"GameObject not found: {target}");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return UnityBridgeSkillResponse.Error(404, $"Prefab asset not found: {prefabPath}");

            PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.AutomatedAction);
            return UnityBridgeSkillResponse.Success($"{{\"connected\":true,\"target\":\"{Esc(target)}\",\"prefab\":\"{Esc(prefabPath)}\"}}");
        }

        private static GameObject FindGo(string n) => string.IsNullOrWhiteSpace(n) ? null : Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).FirstOrDefault(g => string.Equals(g.name, n, StringComparison.OrdinalIgnoreCase));
        private static Dictionary<string, string> ParseQuery(string q) { var r = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); if (!string.IsNullOrWhiteSpace(q)) foreach (var p in q.TrimStart('?').Split('&')) { var kv = p.Split('='); if (kv.Length == 2) r[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]); } return r; }
        private static Dictionary<string, string> ParseBody(string body) { if (string.IsNullOrWhiteSpace(body)) return null; try { var o = JsonUtility.FromJson<PrB>(body); var d = new Dictionary<string, string>(); if (o.path != null) d["path"] = o.path; if (o.target != null) d["target"] = o.target; if (o.prefabPath != null) d["prefabPath"] = o.prefabPath; if (o.mode != null) d["mode"] = o.mode; return d; } catch { return null; } }
        private static string Esc(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        [Serializable] private class PrB { public string path, target, prefabPath, mode; }
    }
}
