using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityEditorSettingsSkill : IUnityBridgeSkill
    {
        public string Name => "unity-editor-settings";

        public string Description =>
            "Read and write Editor settings: PlayerSettings (app name, version, icons, resolution), QualitySettings, " +
            "PhysicsSettings, TimeSettings, GraphicsSettings, Tags/Layers, EditorPrefs.";

        public string Trigger =>
            "Use when the user wants to configure project settings, PlayerSettings, quality levels, physics, tags/layers, " +
            "time scale, graphics, or EditorPrefs key-value pairs.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("GET", "/playersettings", "Returns key PlayerSettings values."),
            new UnityBridgeSkillEndpoint("POST", "/playersettings/set", "Sets PlayerSettings values.",
                "{\"productName\":\"MyGame\",\"bundleVersion\":\"1.0.1\",\"resolutionWidth\":1920,\"resolutionHeight\":1080}"),
            new UnityBridgeSkillEndpoint("GET", "/quality", "Lists quality levels and current settings."),
            new UnityBridgeSkillEndpoint("POST", "/quality/set", "Sets quality level by index or name.",
                "{\"level\":\"Ultra\"}"),
            new UnityBridgeSkillEndpoint("GET", "/tags", "Lists all tags."),
            new UnityBridgeSkillEndpoint("POST", "/tags/add", "Adds a new tag.", "{\"name\":\"Enemy\"}"),
            new UnityBridgeSkillEndpoint("GET", "/layers", "Lists all layers."),
            new UnityBridgeSkillEndpoint("GET", "/physics", "Returns Physics settings."),
            new UnityBridgeSkillEndpoint("GET", "/graphics", "Returns Graphics settings."),
            new UnityBridgeSkillEndpoint("POST", "/editorprefs/set", "Sets an EditorPrefs key-value pair.",
                "{\"key\":\"MyTool.LastPath\",\"value\":\"Assets/Scenes\"}"),
            new UnityBridgeSkillEndpoint("GET", "/editorprefs?key=MyTool.LastPath", "Gets an EditorPrefs value.")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js playersettings`",
            "`node scripts/unity-bridge.js playersettings-set --productName MyGame --bundleVersion 1.0`",
            "`node scripts/unity-bridge.js quality --level Ultra`",
            "`node scripts/unity-bridge.js tags-add --name Enemy`",
            "`node scripts/unity-bridge.js layers`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "PlayerSettings changes require platform switching repaint but apply immediately.",
            "Tags and Layers are project-wide. Adding a tag requires the TagManager asset to be modified.",
            "EditorPrefs persist across Unity sessions."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var p = request.Path.Trim('/');
            return (request.Method == "GET" && (p == "playersettings" || p == "quality" || p == "tags" || p == "layers" || p == "physics" || p == "graphics" || p == "editorprefs"))
                || (request.Method == "POST" && (p == "playersettings/set" || p == "quality/set" || p == "tags/add" || p == "editorprefs/set"));
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            return request.Path.Trim('/') switch
            {
                "playersettings" => GetPlayerSettings(),
                "playersettings/set" => SetPlayerSettings(request),
                "quality" => GetQuality(),
                "quality/set" => SetQuality(request),
                "tags" => GetTags(),
                "tags/add" => AddTag(request),
                "layers" => GetLayers(),
                "physics" => GetPhysics(),
                "graphics" => GetGraphics(),
                "editorprefs" => GetEditorPrefs(request),
                "editorprefs/set" => SetEditorPrefs(request),
                _ => UnityBridgeSkillResponse.Error(404, "Unknown")
            };
        }

        private static UnityBridgeSkillResponse GetPlayerSettings()
        {
            var info = "{"
                + $"\"productName\":\"{Esc(PlayerSettings.productName)}\","
                + $"\"companyName\":\"{Esc(PlayerSettings.companyName)}\","
                + $"\"bundleVersion\":\"{Esc(PlayerSettings.bundleVersion)}\","
                + $"\"defaultScreenWidth\":{PlayerSettings.defaultScreenWidth},"
                + $"\"defaultScreenHeight\":{PlayerSettings.defaultScreenHeight},"
                + $"\"fullscreenMode\":\"{PlayerSettings.fullScreenMode}\","
                + $"\"runInBackground\":{(PlayerSettings.runInBackground ? "true" : "false")},"
                + $"\"useHDRDisplay\":{(PlayerSettings.useHDRDisplay ? "true" : "false")},"
                + $"\"applicationIdentifier\":\"{Esc(PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Standalone))}\""
                + "}";
            return UnityBridgeSkillResponse.Success(info);
        }

        private static UnityBridgeSkillResponse SetPlayerSettings(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            if (b.TryGetValue("productName", out var pn)) PlayerSettings.productName = pn;
            if (b.TryGetValue("companyName", out var cn)) PlayerSettings.companyName = cn;
            if (b.TryGetValue("bundleVersion", out var bv)) PlayerSettings.bundleVersion = bv;
            if (b.TryGetValue("resolutionWidth", out var rw) && int.TryParse(rw, out var iw))
            { PlayerSettings.defaultScreenWidth = iw; PlayerSettings.defaultScreenHeight = int.TryParse(b.GetValueOrDefault("resolutionHeight", ""), out var ih) ? ih : PlayerSettings.defaultScreenHeight; }
            if (b.TryGetValue("resolutionHeight", out var rh) && int.TryParse(rh, out var ih2))
                PlayerSettings.defaultScreenHeight = ih2;
            if (b.TryGetValue("runInBackground", out var rib)) PlayerSettings.runInBackground = rib != "false" && rib != "0";
            if (b.TryGetValue("useHDRDisplay", out var hdr)) PlayerSettings.useHDRDisplay = hdr != "false" && hdr != "0";
            return UnityBridgeSkillResponse.Success($"{{\"set\":true}}");
        }

        private static UnityBridgeSkillResponse GetQuality()
        {
            var names = QualitySettings.names;
            var current = QualitySettings.GetQualityLevel();
            var parts = names.Select((n, i) =>
                $"{{\"index\":{i},\"name\":\"{Esc(n)}\",\"active\":{(i == current ? "true" : "false")}}}").ToArray();
            return UnityBridgeSkillResponse.Success(
                $"{{\"levels\":[{string.Join(",", parts)}],\"current\":{current},\"currentName\":\"{Esc(names.Length > current ? names[current] : "")}\"}}");
        }

        private static UnityBridgeSkillResponse SetQuality(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var level = b?.GetValueOrDefault("level", "");
            if (int.TryParse(level, out var idx))
                QualitySettings.SetQualityLevel(idx, true);
            else
                for (int i = 0; i < QualitySettings.names.Length; i++)
                    if (QualitySettings.names[i].Equals(level, StringComparison.OrdinalIgnoreCase))
                    { QualitySettings.SetQualityLevel(i, true); break; }
            return GetQuality();
        }

        private static UnityBridgeSkillResponse GetTags()
        {
            var tags = UnityEditorInternal.InternalEditorUtility.tags;
            var parts = tags.Select(t => $"\"{Esc(t)}\"").ToArray();
            return UnityBridgeSkillResponse.Success($"{{\"tags\":[{string.Join(",", parts)}],\"count\":{parts.Length}}}");
        }

        private static UnityBridgeSkillResponse AddTag(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var name = b?.GetValueOrDefault("name", "");
            if (string.IsNullOrWhiteSpace(name)) return UnityBridgeSkillResponse.Error(400, "Missing 'name'");
            if (UnityEditorInternal.InternalEditorUtility.tags.Contains(name))
                return UnityBridgeSkillResponse.Error(400, $"Tag already exists: {name}");

            var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tagsProp = so.FindProperty("tags");
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (string.IsNullOrEmpty(tagsProp.GetArrayElementAtIndex(i).stringValue))
                {
                    tagsProp.GetArrayElementAtIndex(i).stringValue = name;
                    so.ApplyModifiedProperties();
                    return UnityBridgeSkillResponse.Success($"{{\"added\":true,\"name\":\"{Esc(name)}\"}}");
                }
            }
            so.Dispose();
            return UnityBridgeSkillResponse.Error(500, "Tag list is full. Remove unused tags first.");
        }

        private static UnityBridgeSkillResponse GetLayers()
        {
            var layers = UnityEditorInternal.InternalEditorUtility.layers;
            var parts = layers.Select((l, i) => $"{{\"index\":{i},\"name\":\"{Esc(l)}\"}}").ToArray();
            return UnityBridgeSkillResponse.Success($"{{\"layers\":[{string.Join(",", parts)}],\"count\":{parts.Length}}}");
        }

        private static UnityBridgeSkillResponse GetPhysics()
        {
            return UnityBridgeSkillResponse.Success(
                $"{{\"gravity\":[{Physics.gravity.x:F2},{Physics.gravity.y:F2},{Physics.gravity.z:F2}],\"bounceThreshold\":{Physics.bounceThreshold:F3},\"defaultMaxDepenetrationVelocity\":{Physics.defaultMaxDepenetrationVelocity:F2},\"sleepThreshold\":{Physics.sleepThreshold:F3},\"defaultContactOffset\":{Physics.defaultContactOffset:F3}}}");
        }

        private static UnityBridgeSkillResponse GetGraphics()
        {
            return UnityBridgeSkillResponse.Success(
                $"{{\"activeTier\":\"{Graphics.activeTier}\",\"shaderLevel\":{SystemInfo.graphicsShaderLevel},\"renderPipeline\":\"{Esc(QualitySettings.renderPipeline?.name ?? "Built-in")}\"}}");
        }

        private static UnityBridgeSkillResponse GetEditorPrefs(UnityBridgeSkillRequest request)
        {
            var q = ParseQuery(request.QueryString);
            var key = q.GetValueOrDefault("key", "");
            if (string.IsNullOrWhiteSpace(key))
            {
                // List common known prefs keys? Not easily enumerable. Return a note.
                return UnityBridgeSkillResponse.Success("{\"note\":\"EditorPrefs keys are not enumerable. Provide a specific key.\"}");
            }
            var val = EditorPrefs.GetString(key, "(not set)");
            return UnityBridgeSkillResponse.Success($"{{\"key\":\"{Esc(key)}\",\"value\":\"{Esc(val)}\"}}");
        }

        private static UnityBridgeSkillResponse SetEditorPrefs(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var key = b?.GetValueOrDefault("key", "");
            var value = b?.GetValueOrDefault("value", "");
            if (string.IsNullOrWhiteSpace(key)) return UnityBridgeSkillResponse.Error(400, "Missing 'key'");
            EditorPrefs.SetString(key, value);
            return UnityBridgeSkillResponse.Success($"{{\"set\":true,\"key\":\"{Esc(key)}\"}}");
        }

        private static Dictionary<string, string> ParseQuery(string q) { var r = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); if (!string.IsNullOrWhiteSpace(q)) foreach (var p in q.TrimStart('?').Split('&')) { var kv = p.Split('='); if (kv.Length == 2) r[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]); } return r; }
        private static Dictionary<string, string> ParseBody(string body) { if (string.IsNullOrWhiteSpace(body)) return null; try { var o = JsonUtility.FromJson<ES>(body); var d = new Dictionary<string, string>(); if (o.productName != null) d["productName"] = o.productName; if (o.companyName != null) d["companyName"] = o.companyName; if (o.bundleVersion != null) d["bundleVersion"] = o.bundleVersion; if (o.resolutionWidth != null) d["resolutionWidth"] = o.resolutionWidth; if (o.resolutionHeight != null) d["resolutionHeight"] = o.resolutionHeight; if (o.runInBackground != null) d["runInBackground"] = o.runInBackground; if (o.useHDRDisplay != null) d["useHDRDisplay"] = o.useHDRDisplay; if (o.level != null) d["level"] = o.level; if (o.name != null) d["name"] = o.name; if (o.key != null) d["key"] = o.key; if (o.value != null) d["value"] = o.value; return d; } catch { return null; } }
        private static string Esc(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        [Serializable] private class ES { public string productName, companyName, bundleVersion, resolutionWidth, resolutionHeight, runInBackground, useHDRDisplay, level, name, key, value; }
    }
}
