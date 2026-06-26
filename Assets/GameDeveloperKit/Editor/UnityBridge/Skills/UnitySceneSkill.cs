using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnitySceneSkill : IUnityBridgeSkill
    {
        public string Name => "unity-scene";

        public string Description =>
            "Manage Unity scenes: create, load, save, and query scene info.";

        public string Trigger =>
            "Use when the user asks about scenes, wants to create/load/save a scene, or needs scene information.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("GET", "/scene", "Returns current scene info: name, path, dirty, buildIndex, root count."),
            new UnityBridgeSkillEndpoint("POST", "/scene/create", "Creates a new empty scene.", "{\"path\":\"Assets/Scenes/NewScene.unity\"}"),
            new UnityBridgeSkillEndpoint("POST", "/scene/load", "Loads a scene by path or name.", "{\"path\":\"Assets/Scenes/Main.unity\"}"),
            new UnityBridgeSkillEndpoint("POST", "/scene/save", "Saves the current active scene."),
            new UnityBridgeSkillEndpoint("GET", "/scene/list", "Lists all loaded scenes.")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js scene`",
            "`node scripts/unity-bridge.js scene-create --path Assets/Scenes/New.unity`",
            "`node scripts/unity-bridge.js scene-load --path Assets/Scenes/Main.unity`",
            "`node scripts/unity-bridge.js scene-save`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Creating a new scene will prompt to save the current scene if dirty.",
            "Load by name searches loaded scenes first, then the Build Settings scene list."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return (request.Method == "GET" && path == "scene")
                || (request.Method == "GET" && path == "scene/list")
                || (request.Method == "POST" && path == "scene/create")
                || (request.Method == "POST" && path == "scene/load")
                || (request.Method == "POST" && path == "scene/save");
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');

            return path switch
            {
                "scene" => GetSceneInfo(),
                "scene/list" => ListScenes(),
                "scene/create" => CreateScene(request),
                "scene/load" => LoadScene(request),
                "scene/save" => SaveScene(),
                _ => UnityBridgeSkillResponse.Error(404, $"Unknown scene endpoint: /{path}")
            };
        }

        private static UnityBridgeSkillResponse GetSceneInfo()
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var json = "{"
                + $"\"name\":\"{Esc(scene.name)}\","
                + $"\"path\":\"{Esc(scene.path)}\","
                + $"\"isDirty\":{(scene.isDirty ? "true" : "false")},"
                + $"\"isLoaded\":{(scene.isLoaded ? "true" : "false")},"
                + $"\"buildIndex\":{scene.buildIndex},"
                + $"\"rootCount\":{roots.Length}"
                + "}";
            return UnityBridgeSkillResponse.Success(json);
        }

        private static UnityBridgeSkillResponse ListScenes()
        {
            var parts = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                parts.Add("{"
                    + $"\"name\":\"{Esc(s.name)}\","
                    + $"\"path\":\"{Esc(s.path)}\","
                    + $"\"isLoaded\":{(s.isLoaded ? "true" : "false")},"
                    + $"\"buildIndex\":{s.buildIndex}"
                    + "}");
            }

            return UnityBridgeSkillResponse.Success($"{{\"scenes\":[{string.Join(",", parts)}],\"count\":{parts.Count}}}");
        }

        private static UnityBridgeSkillResponse CreateScene(UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var scenePath = body?.GetValueOrDefault("path", "");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!string.IsNullOrWhiteSpace(scenePath))
            {
                if (!scenePath.EndsWith(".unity")) scenePath += ".unity";
                EnsureDir(scenePath);
                EditorSceneManager.SaveScene(scene, scenePath);
            }

            return UnityBridgeSkillResponse.Success($"{{\"created\":true,\"name\":\"{Esc(scene.name)}\",\"path\":\"{Esc(scenePath)}\"}}");
        }

        private static UnityBridgeSkillResponse LoadScene(UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var scenePath = body?.GetValueOrDefault("path", "");
            var modeStr = body?.GetValueOrDefault("mode", "Single");

            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return UnityBridgeSkillResponse.Error(400, "Missing 'path' field");
            }

            if (!scenePath.EndsWith(".unity") && !scenePath.Contains("/"))
            {
                scenePath += ".unity";
            }

            var mode = modeStr.Equals("Additive", System.StringComparison.OrdinalIgnoreCase)
                ? OpenSceneMode.Additive : OpenSceneMode.Single;

            try
            {
                if (scenePath.Contains("/"))
                {
                    EditorSceneManager.OpenScene(scenePath, mode);
                }
                else
                {
                    EditorSceneManager.OpenScene(
                        EditorBuildSettings.scenes[0].path,
                        mode);
                }

                var scene = SceneManager.GetActiveScene();
                return UnityBridgeSkillResponse.Success(
                    $"{{\"loaded\":true,\"name\":\"{Esc(scene.name)}\",\"path\":\"{Esc(scene.path)}\"}}");
            }
            catch (System.Exception ex)
            {
                return UnityBridgeSkillResponse.Error(500, $"Failed to load scene: {ex.Message}");
            }
        }

        private static UnityBridgeSkillResponse SaveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                return UnityBridgeSkillResponse.Error(400, "Scene has no path. Use /scene/create with a path first.");
            }

            var ok = EditorSceneManager.SaveScene(scene);
            return UnityBridgeSkillResponse.Success($"{{\"saved\":{(ok ? "true" : "false")},\"path\":\"{Esc(scene.path)}\"}}");
        }

        private static Dictionary<string, string> ParseBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                var obj = JsonUtility.FromJson<SceneBody>(body);
                if (obj == null) return null;
                var d = new Dictionary<string, string>();
                if (obj.path != null) d["path"] = obj.path;
                if (obj.mode != null) d["mode"] = obj.mode;
                return d;
            }
            catch { return null; }
        }

        private static void EnsureDir(string path)
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }
        }

        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        [System.Serializable]
        private class SceneBody { public string path; public string mode; }
    }
}
