using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityBuildSkill : IUnityBridgeSkill
    {
        public string Name => "unity-build";

        public string Description =>
            "Build player, switch platform, list build scenes, get build report. Supports Windows, macOS, Linux, Android, iOS, WebGL.";

        public string Trigger =>
            "Use when the user wants to build the player, switch target platform, configure build scenes, or check build results.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("GET", "/build/info", "Returns current build target, platform, and enabled scenes."),
            new UnityBridgeSkillEndpoint("POST", "/build/switch", "Switches the active build target.",
                "{\"target\":\"Android\"}"),
            new UnityBridgeSkillEndpoint("POST", "/build/player", "Builds the player for the current or specified target.",
                "{\"target\":\"StandaloneWindows64\",\"path\":\"Build/MyGame.exe\",\"development\":false}"),
            new UnityBridgeSkillEndpoint("GET", "/build/scenes", "Lists all scenes in EditorBuildSettings.")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js build-info`",
            "`node scripts/unity-bridge.js build-switch --target Android`",
            "`node scripts/unity-bridge.js build-player --target StandaloneWindows64 --path Build/MyGame.exe`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Switching platform may take a long time. The bridge will time out on long operations.",
            "Build target names: StandaloneWindows64, StandaloneOSX, StandaloneLinux64, Android, iOS, WebGL.",
            "Use 'development:true' for development builds with script debugging."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var p = request.Path.Trim('/');
            return (request.Method == "GET" && (p == "build/info" || p == "build/scenes"))
                || (request.Method == "POST" && (p == "build/switch" || p == "build/player"));
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            return request.Path.Trim('/') switch
            {
                "build/info" => GetInfo(),
                "build/scenes" => GetScenes(),
                "build/switch" => Switch(request),
                "build/player" => Build(request),
                _ => UnityBridgeSkillResponse.Error(404, "Unknown")
            };
        }

        private static UnityBridgeSkillResponse GetInfo()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var group = BuildPipeline.GetBuildTargetGroup(target);
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => $"\"{Esc(s.path)}\"").ToArray();
            return UnityBridgeSkillResponse.Success(
                $"{{\"platform\":\"{Esc(target.ToString())}\",\"group\":\"{Esc(group.ToString())}\",\"development\":{(EditorUserBuildSettings.development ? "true" : "false")},\"scenes\":[{string.Join(",", scenes)}]}}");
        }

        private static UnityBridgeSkillResponse GetScenes()
        {
            var parts = EditorBuildSettings.scenes.Select(s =>
                $"{{\"path\":\"{Esc(s.path)}\",\"enabled\":{(s.enabled ? "true" : "false")}}}").ToArray();
            return UnityBridgeSkillResponse.Success($"{{\"scenes\":[{string.Join(",", parts)}],\"count\":{parts.Length}}}");
        }

        private static UnityBridgeSkillResponse Switch(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var targetStr = b?.GetValueOrDefault("target", "");
            if (!Enum.TryParse<BuildTarget>(targetStr, true, out var target))
                return UnityBridgeSkillResponse.Error(400, $"Unknown build target: {targetStr}");

            var group = BuildPipeline.GetBuildTargetGroup(target);
            if (EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
                return UnityBridgeSkillResponse.Success($"{{\"switched\":true,\"target\":\"{Esc(target.ToString())}\"}}");
            return UnityBridgeSkillResponse.Error(500, "Failed to switch platform");
        }

        private static UnityBridgeSkillResponse Build(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var targetStr = b?.GetValueOrDefault("target", "");
            var outputPath = b?.GetValueOrDefault("path", "Build/Player");
            var devStr = b?.GetValueOrDefault("development", "false");

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (!string.IsNullOrWhiteSpace(targetStr) && !Enum.TryParse<BuildTarget>(targetStr, true, out target))
                return UnityBridgeSkillResponse.Error(400, $"Unknown target: {targetStr}");

            var dev = !devStr.Equals("false", StringComparison.OrdinalIgnoreCase) && devStr != "0";

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) return UnityBridgeSkillResponse.Error(400, "No scenes in build settings");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = dev ? BuildOptions.Development : BuildOptions.None
            };

            try
            {
                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;
                if (summary.result == BuildResult.Succeeded)
                    return UnityBridgeSkillResponse.Success(
                        $"{{\"built\":true,\"target\":\"{Esc(target.ToString())}\",\"path\":\"{Esc(outputPath)}\",\"sizeBytes\":{summary.totalSize},\"errors\":{summary.totalErrors},\"warnings\":{summary.totalWarnings}}}");
                return UnityBridgeSkillResponse.Error(500, $"Build failed: {report.SummarizeErrors()}");
            }
            catch (Exception ex)
            {
                return UnityBridgeSkillResponse.Error(500, $"Build exception: {ex.Message}");
            }
        }

        private static Dictionary<string, string> ParseBody(string body) { if (string.IsNullOrWhiteSpace(body)) return null; try { var o = JsonUtility.FromJson<Bld>(body); var d = new Dictionary<string, string>(); if (o.target != null) d["target"] = o.target; if (o.path != null) d["path"] = o.path; if (o.development != null) d["development"] = o.development; return d; } catch { return null; } }
        private static string Esc(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        [Serializable] private class Bld { public string target, path, development; }
    }
}
