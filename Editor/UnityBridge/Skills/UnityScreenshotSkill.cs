using System;
using System.Collections.Generic;
using System.IO;
using SysIO = System.IO;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityScreenshotSkill : IUnityBridgeSkill
    {
        private static string s_LatestScreenshotPath;

        public string Name => "unity-screenshot";

        public string Description =>
            "Capture scene view or game view screenshots. Save to a file path and optionally return as base64.";

        public string Trigger =>
            "Use when the user wants to see what Unity is showing, capture the scene or game view, take a screenshot, or get a visual of the editor.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("POST", "/screenshot/scene", "Capture the Scene View.", "{\"path\":\"Assets/screenshot.png\",\"width\":1920,\"height\":1080}"),
            new UnityBridgeSkillEndpoint("POST", "/screenshot/game", "Capture the Game View (Play mode only).", "{\"path\":\"Assets/game_screen.png\",\"width\":1920,\"height\":1080}"),
            new UnityBridgeSkillEndpoint("GET", "/screenshot/latest", "Returns the path and dimensions of the last screenshot taken.")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js screenshot-scene --path Assets/scene.png --width 1280`",
            "`node scripts/unity-bridge.js screenshot-game --path Assets/game.png`",
            "`node scripts/unity-bridge.js screenshot-latest`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Scene view capture works in Edit mode. Game view capture requires Play mode.",
            "Images are saved as PNG. Use project-relative paths like 'Assets/...'.",
            "If path is omitted, a temp path is used."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return (request.Method == "POST" && path == "screenshot/scene")
                || (request.Method == "POST" && path == "screenshot/game")
                || (request.Method == "GET" && path == "screenshot/latest");
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return path switch
            {
                "screenshot/scene" => CaptureSceneView(request),
                "screenshot/game" => CaptureGameView(request),
                "screenshot/latest" => GetLatest(),
                _ => UnityBridgeSkillResponse.Error(404, $"Unknown endpoint: /{path}")
            };
        }

        private static UnityBridgeSkillResponse CaptureSceneView(UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var filePath = body?.GetValueOrDefault("path", $"Assets/unity_bridge_scene_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            var width = ParseInt(body, "width", 1920);
            var height = ParseInt(body, "height", 1080);

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return UnityBridgeSkillResponse.Error(400, "No active Scene View. Open a Scene View window first.");

            try
            {
                var camera = sceneView.camera;
                if (camera == null)
                    return UnityBridgeSkillResponse.Error(500, "Scene View camera not available.");

                var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                var previousTarget = camera.targetTexture;
                camera.targetTexture = rt;
                camera.Render();

                var previousActive = RenderTexture.active;
                RenderTexture.active = rt;

                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;

                var bytes = tex.EncodeToPNG();
                SysIO.File.WriteAllBytes(filePath, bytes);
                AssetDatabase.Refresh();

                UnityEngine.Object.DestroyImmediate(tex);
                UnityEngine.Object.DestroyImmediate(rt);

                s_LatestScreenshotPath = filePath;
                return UnityBridgeSkillResponse.Success(
                    $"{{\"captured\":true,\"path\":\"{Esc(filePath)}\",\"width\":{width},\"height\":{height},\"sizeBytes\":{bytes.Length}}}");
            }
            catch (Exception ex)
            {
                return UnityBridgeSkillResponse.Error(500, $"Screenshot failed: {ex.Message}");
            }
        }

        private static UnityBridgeSkillResponse CaptureGameView(UnityBridgeSkillRequest request)
        {
            if (!EditorApplication.isPlaying)
                return UnityBridgeSkillResponse.Error(400, "Game view capture requires Play mode. Enter Play mode first.");

            var body = ParseBody(request.Body);
            var filePath = body?.GetValueOrDefault("path", $"Assets/unity_bridge_game_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            var scaleStr = body?.GetValueOrDefault("supersize", "1");

            if (!float.TryParse(scaleStr, out var scale) || scale < 1f) scale = 1f;
            if (scale > 4f) scale = 4f;

            try
            {
                var previousPath = filePath;
                ScreenCapture.CaptureScreenshot(previousPath, (int)scale);

                // ScreenCapture.CaptureScreenshot is asynchronous; wait briefly for the file
                var start = DateTime.Now;
                while (!SysIO.File.Exists(previousPath) && (DateTime.Now - start).TotalSeconds < 3)
                {
                    System.Threading.Thread.Sleep(100);
                }

                s_LatestScreenshotPath = filePath;
                AssetDatabase.Refresh();
                return UnityBridgeSkillResponse.Success(
                    $"{{\"captured\":true,\"path\":\"{Esc(filePath)}\",\"supersize\":{scale}}}");
            }
            catch (Exception ex)
            {
                return UnityBridgeSkillResponse.Error(500, $"Game screenshot failed: {ex.Message}");
            }
        }

        private static UnityBridgeSkillResponse GetLatest()
        {
            if (string.IsNullOrWhiteSpace(s_LatestScreenshotPath))
                return UnityBridgeSkillResponse.Error(404, "No screenshot has been taken yet.");

            var info = new SysIO.FileInfo(s_LatestScreenshotPath);
            var exists = info.Exists;
            var size = exists ? info.Length : 0L;
            return UnityBridgeSkillResponse.Success(
                $"{{\"path\":\"{Esc(s_LatestScreenshotPath)}\",\"exists\":{(exists ? "true" : "false")},\"sizeBytes\":{size}}}");
        }

        private static int ParseInt(Dictionary<string, string> body, string key, int fallback)
        {
            if (body != null && body.TryGetValue(key, out var val) && int.TryParse(val, out var result))
                return Math.Clamp(result, 64, 7680);
            return fallback;
        }

        private static Dictionary<string, string> ParseBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                var obj = JsonUtility.FromJson<ScreenshotBody>(body);
                if (obj == null) return null;
                var d = new Dictionary<string, string>();
                if (obj.path != null) d["path"] = obj.path;
                if (obj.width != null) d["width"] = obj.width;
                if (obj.height != null) d["height"] = obj.height;
                if (obj.supersize != null) d["supersize"] = obj.supersize;
                return d;
            }
            catch { return null; }
        }

        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        [Serializable]
        private class ScreenshotBody { public string path; public string width; public string height; public string supersize; }
    }
}
