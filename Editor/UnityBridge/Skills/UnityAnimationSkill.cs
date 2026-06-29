using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityAnimationSkill : IUnityBridgeSkill
    {
        public string Name => "unity-animation";

        public string Description =>
            "Create and edit AnimationClips: add keyframes, set curves, configure clip settings, discover animatable properties.";

        public string Trigger =>
            "Use when the user wants to create animation clips, add keyframes, set animation curves, discover what properties can be animated on a GameObject, or configure clip wrap/loop settings.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("GET", "/animation/bindings?target=Cube", "Lists all animatable properties on a GameObject."),
            new UnityBridgeSkillEndpoint("POST", "/animation/clip/create", "Creates a new AnimationClip asset.",
                "{\"path\":\"Assets/MyClip.anim\",\"frameRate\":30}"),
            new UnityBridgeSkillEndpoint("POST", "/animation/keyframe/add", "Adds keyframes to a clip for a specific property.",
                "{\"path\":\"Assets/MyClip.anim\",\"target\":\"Cube\",\"component\":\"Transform\",\"property\":\"m_LocalPosition.x\",\"keyframes\":[[0,0],[0.5,1],[1,0]]}"),
            new UnityBridgeSkillEndpoint("POST", "/animation/clip/settings", "Configures clip wrap mode, loop time, etc.",
                "{\"path\":\"Assets/MyClip.anim\",\"loopTime\":true,\"wrapMode\":\"Loop\"}")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js anim-bindings --target Cube`",
            "`node scripts/unity-bridge.js anim-clip-create --path Assets/Spin.anim --frameRate 30`",
            "`node scripts/unity-bridge.js anim-keyframe --path Assets/Spin.anim --target Cube --component Transform --property m_LocalPosition.y --keyframes 0,0 0.5,2 1,0`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Property names must use serialized names (e.g. 'm_LocalPosition.x'). Use the bindings endpoint to discover them.",
            "Keyframes format: [[time,value],[time,value],...].",
            "The target path can be empty string for the root GameObject."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var p = request.Path.Trim('/');
            return (request.Method == "GET" && p == "animation/bindings")
                || (request.Method == "POST" && (p == "animation/clip/create" || p == "animation/keyframe/add" || p == "animation/clip/settings"));
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            var p = request.Path.Trim('/');
            return p switch
            {
                "animation/bindings" => GetBindings(request),
                "animation/clip/create" => CreateClip(request),
                "animation/keyframe/add" => AddKeyframes(request),
                "animation/clip/settings" => SetClipSettings(request),
                _ => UnityBridgeSkillResponse.Error(404, $"Unknown endpoint: /{p}")
            };
        }

        private static UnityBridgeSkillResponse GetBindings(UnityBridgeSkillRequest request)
        {
            var q = ParseQuery(request.QueryString);
            var target = q.GetValueOrDefault("target", "");
            var go = FindGo(target);
            if (go == null) return UnityBridgeSkillResponse.Error(404, $"GameObject not found: {target}");

            var bindings = AnimationUtility.GetAnimatableBindings(go, go);
            var parts = bindings.Select(b => $"{{\"path\":\"{Esc(b.path)}\",\"type\":\"{Esc(b.type.Name)}\",\"property\":\"{Esc(b.propertyName)}\"}}").ToArray();

            return UnityBridgeSkillResponse.Success($"{{\"target\":\"{Esc(target)}\",\"bindings\":[{string.Join(",", parts)}],\"count\":{parts.Length}}}");
        }

        private static UnityBridgeSkillResponse CreateClip(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var path = b?.GetValueOrDefault("path", "Assets/NewClip.anim");
            var fps = 30f;
            if (b != null && b.TryGetValue("frameRate", out var fr) && float.TryParse(fr, out var f)) fps = f;

            if (!path.EndsWith(".anim")) path += ".anim";
            EnsureDir(path);

            var clip = new AnimationClip { name = System.IO.Path.GetFileNameWithoutExtension(path), frameRate = fps };
            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.Refresh();
            return UnityBridgeSkillResponse.Success($"{{\"created\":true,\"path\":\"{Esc(path)}\",\"frameRate\":{fps:F1}}}");
        }

        private static UnityBridgeSkillResponse AddKeyframes(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var path = b?.GetValueOrDefault("path", "");
            var target = b?.GetValueOrDefault("target", "");
            var component = b?.GetValueOrDefault("component", "Transform");
            var property = b?.GetValueOrDefault("property", "");
            var kfsJson = b?.GetValueOrDefault("keyframes", "");

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) return UnityBridgeSkillResponse.Error(404, $"Clip not found: {path}");

            var go = FindGo(target);
            Type compType = go != null ? go.GetComponents<Component>().FirstOrDefault(c => c.GetType().Name == component)?.GetType() : typeof(Transform);

            var kfs = ParseKeyframes(kfsJson);
            if (kfs.Count == 0) return UnityBridgeSkillResponse.Error(400, "Invalid keyframes format");

            var curve = new AnimationCurve(kfs.Select(k => new Keyframe(k.time, k.value)).ToArray());
            var binding = EditorCurveBinding.FloatCurve("", compType ?? typeof(Transform), property);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            return UnityBridgeSkillResponse.Success($"{{\"added\":true,\"path\":\"{Esc(path)}\",\"property\":\"{Esc(property)}\",\"keyframeCount\":{kfs.Count}}}");
        }

        private static UnityBridgeSkillResponse SetClipSettings(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var path = b?.GetValueOrDefault("path", "");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) return UnityBridgeSkillResponse.Error(404, $"Clip not found: {path}");

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (b.TryGetValue("loopTime", out var lt)) settings.loopTime = !lt.Equals("false", StringComparison.OrdinalIgnoreCase) && lt != "0";
            if (b.TryGetValue("loopBlend", out var lb)) settings.loopBlend = !lb.Equals("false", StringComparison.OrdinalIgnoreCase) && lb != "0";
            if (b.TryGetValue("cycleOffset", out var co) && float.TryParse(co, out var cof)) settings.cycleOffset = cof;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            return UnityBridgeSkillResponse.Success($"{{\"configured\":true,\"path\":\"{Esc(path)}\",\"loopTime\":{(settings.loopTime ? "true" : "false")}}}");
        }

        private static List<(float time, float value)> ParseKeyframes(string json)
        {
            var result = new List<(float, float)>();
            if (string.IsNullOrWhiteSpace(json)) return result;
            try
            {
                var wrapper = JsonUtility.FromJson<KeyframeListWrap>($"{{\"kfs\":{json}}}");
                if (wrapper?.kfs != null) { result.AddRange(wrapper.kfs.Select(k => (k[0], k[1]))); return result; }
            }
            catch { }
            // Also try flat: "t,v t,v ..."
            foreach (var pair in json.Split(' ', '\n'))
            {
                var parts = pair.Trim('[', ']', ',').Split(',');
                if (parts.Length == 2 && float.TryParse(parts[0], out var t) && float.TryParse(parts[1], out var v))
                    result.Add((t, v));
            }
            return result;
        }

        private static void EnsureDir(string p) { var d = System.IO.Path.GetDirectoryName(p); if (!string.IsNullOrWhiteSpace(d) && !System.IO.Directory.Exists(d)) System.IO.Directory.CreateDirectory(d); }

        private static GameObject FindGo(string n) => string.IsNullOrWhiteSpace(n) ? null : Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).FirstOrDefault(g => string.Equals(g.name, n, StringComparison.OrdinalIgnoreCase));

        private static Dictionary<string, string> ParseQuery(string q) { var r = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); if (!string.IsNullOrWhiteSpace(q)) foreach (var p in q.TrimStart('?').Split('&')) { var kv = p.Split('='); if (kv.Length == 2) r[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]); } return r; }

        private static Dictionary<string, string> ParseBody(string body) { if (string.IsNullOrWhiteSpace(body)) return null; try { var o = JsonUtility.FromJson<AB>(body); var d = new Dictionary<string, string>(); if (o.path != null) d["path"] = o.path; if (o.target != null) d["target"] = o.target; if (o.component != null) d["component"] = o.component; if (o.property != null) d["property"] = o.property; if (o.keyframes != null) d["keyframes"] = o.keyframes; if (o.frameRate != null) d["frameRate"] = o.frameRate; if (o.loopTime != null) d["loopTime"] = o.loopTime; if (o.loopBlend != null) d["loopBlend"] = o.loopBlend; if (o.cycleOffset != null) d["cycleOffset"] = o.cycleOffset; return d; } catch { return null; } }

        private static string Esc(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        [Serializable] private class AB { public string path, target, component, property, keyframes, frameRate, loopTime, loopBlend, cycleOffset; }
        [Serializable] private class KeyframeListWrap { public float[][] kfs; }
    }
}
