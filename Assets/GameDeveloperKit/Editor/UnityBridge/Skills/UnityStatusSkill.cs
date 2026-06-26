using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityStatusSkill : IUnityBridgeSkill
    {
        public string Name => "unity-status";

        public string Description =>
            "Detect Unity Editor state: compilation, play mode, pause, active scene, version, bridge status.";

        public string Trigger =>
            "Use when the user asks whether Unity is compiling, running, paused, available, or wants current editor state.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("GET", "/status", "Unity Editor and bridge status."),
            new UnityBridgeSkillEndpoint("GET", "/ping", "Health check for Unity Bridge.")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js status`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "If Unity is compiling, the status endpoint will report isCompiling=true."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return path == "status" || path == "ping";
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');

            if (path == "ping")
            {
                return UnityBridgeSkillResponse.Success(
                    $"{{\"pong\":true,\"timestamp\":\"{DateTime.Now:O}\"}}");
            }

            var json = "{"
                + $"\"isCompiling\":{(EditorApplication.isCompiling ? "true" : "false")},"
                + $"\"isPlaying\":{(EditorApplication.isPlaying ? "true" : "false")},"
                + $"\"isPaused\":{(EditorApplication.isPaused ? "true" : "false")},"
                + $"\"isUpdating\":{(EditorApplication.isUpdating ? "true" : "false")},"
                + $"\"scene\":\"{Esc(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)}\","
                + $"\"sceneCount\":{UnityEngine.SceneManagement.SceneManager.sceneCount},"
                + $"\"unityVersion\":\"{Esc(Application.unityVersion)}\","
                + $"\"productName\":\"{Esc(Application.productName)}\","
                + $"\"bridgeRunning\":{(UnityBridgeTaskQueue.IsRunning ? "true" : "false")}"
                + "}";
            return UnityBridgeSkillResponse.Success(json);
        }

        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
