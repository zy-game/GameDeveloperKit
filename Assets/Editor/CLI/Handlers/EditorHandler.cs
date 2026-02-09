using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace GameDeveloperKit.Editor.CLI
{
    [Serializable]
    public class EditorStateInfo
    {
        public bool isPlaying;
        public bool isPaused;
        public bool isCompiling;
        public string activeScenePath;
        public string activeSceneName;
        public List<string> selectedObjects = new();
        public string focusedWindow;
    }

    [Serializable]
    public class EditorOperationResult
    {
        public bool success;
        public string message;
    }

    public class EditorHandler : ICLIHandler
    {
        public List<string> GetCommands()
        {
            return new List<string>
            {
                "unity_get_editor_state",
                "unity_set_play_mode",
                "unity_set_pause",
                "unity_select_objects",
                "unity_step_frame",
                "unity_focus_gameobject",
                "unity_compile_scripts",
                "unity_refresh_assets_force"
            };
        }

        public string Execute(string command, string parameters)
        {
            var args = string.IsNullOrEmpty(parameters) ? new JObject() : JObject.Parse(parameters);
            
            return command switch
            {
                "unity_get_editor_state" => GetEditorState(),
                "unity_set_play_mode" => SetPlayMode(args),
                "unity_set_pause" => SetPause(args),
                "unity_select_objects" => SelectObjects(args),
                "unity_step_frame" => StepFrame(),
                "unity_focus_gameobject" => FocusGameObject(args),
                "unity_compile_scripts" => CompileScripts(),
                "unity_refresh_assets_force" => RefreshAssetsForce(),
                _ => JsonConvert.SerializeObject(new EditorOperationResult { success = false, message = $"Unknown command: {command}" })
            };
        }

        private string GetEditorState()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            var state = new EditorStateInfo
            {
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                isCompiling = EditorApplication.isCompiling,
                activeScenePath = activeScene.path,
                activeSceneName = activeScene.name,
                focusedWindow = EditorWindow.focusedWindow?.GetType().Name ?? "None"
            };

            foreach (var obj in Selection.gameObjects)
            {
                state.selectedObjects.Add(GetGameObjectPath(obj));
            }

            foreach (var obj in Selection.objects)
            {
                if (obj is GameObject) continue;
                var path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path))
                    state.selectedObjects.Add(path);
            }

            return JsonConvert.SerializeObject(state);
        }

        private string SetPlayMode(JObject args)
        {
            var playing = args["playing"]?.ToObject<bool>() ?? false;

            if (playing && !EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
                return JsonConvert.SerializeObject(new EditorOperationResult { success = true, message = "Entering play mode" });
            }
            else if (!playing && EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return JsonConvert.SerializeObject(new EditorOperationResult { success = true, message = "Exiting play mode" });
            }

            return JsonConvert.SerializeObject(new EditorOperationResult
            {
                success = true,
                message = $"Play mode already {(EditorApplication.isPlaying ? "active" : "inactive")}"
            });
        }

        private string SetPause(JObject args)
        {
            var paused = args["paused"]?.ToObject<bool>() ?? false;

            if (!EditorApplication.isPlaying)
            {
                return JsonConvert.SerializeObject(new EditorOperationResult { success = false, message = "Cannot pause when not in play mode" });
            }

            EditorApplication.isPaused = paused;
            return JsonConvert.SerializeObject(new EditorOperationResult { success = true, message = paused ? "Paused" : "Resumed" });
        }

        private string SelectObjects(JObject args)
        {
            var paths = args["paths"]?.ToObject<string[]>();

            if (paths == null || paths.Length == 0)
            {
                Selection.objects = Array.Empty<UnityEngine.Object>();
                return JsonConvert.SerializeObject(new EditorOperationResult { success = true, message = "Selection cleared" });
            }

            var objects = new List<UnityEngine.Object>();

            foreach (var path in paths)
            {
                var go = GameObject.Find(path);
                if (go != null)
                {
                    objects.Add(go);
                    continue;
                }

                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null)
                {
                    objects.Add(asset);
                }
            }

            if (objects.Count == 0)
            {
                return JsonConvert.SerializeObject(new EditorOperationResult { success = false, message = "No objects found for the given paths" });
            }

            Selection.objects = objects.ToArray();
            return JsonConvert.SerializeObject(new EditorOperationResult { success = true, message = $"Selected {objects.Count} object(s)" });
        }

        private string StepFrame()
        {
            if (!EditorApplication.isPlaying)
            {
                return JsonConvert.SerializeObject(new EditorOperationResult { success = false, message = "Cannot step when not in play mode" });
            }

            if (!EditorApplication.isPaused)
            {
                return JsonConvert.SerializeObject(new EditorOperationResult { success = false, message = "Cannot step when not paused" });
            }

            EditorApplication.Step();
            return JsonConvert.SerializeObject(new EditorOperationResult { success = true, message = "Stepped one frame" });
        }

        private string FocusGameObject(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new EditorOperationResult { success = false, message = "path is required" });
            }

            var go = GameObject.Find(path);
            if (go == null)
            {
                return JsonConvert.SerializeObject(new EditorOperationResult { success = false, message = $"GameObject not found: {path}" });
            }

            Selection.activeGameObject = go;
            SceneView.lastActiveSceneView?.FrameSelected();

            return JsonConvert.SerializeObject(new EditorOperationResult { success = true, message = $"Focused on {path}" });
        }

        private string GetGameObjectPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private string CompileScripts()
        {
            if (EditorApplication.isCompiling)
            {
                return JsonConvert.SerializeObject(new EditorOperationResult
                {
                    success = true,
                    message = "Compilation already in progress"
                });
            }

            CompilationPipeline.RequestScriptCompilation();

            return JsonConvert.SerializeObject(new EditorOperationResult
            {
                success = true,
                message = "Script compilation requested"
            });
        }

        private string RefreshAssetsForce()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            
            return JsonConvert.SerializeObject(new EditorOperationResult
            {
                success = true,
                message = EditorApplication.isCompiling 
                    ? "AssetDatabase refreshed, compilation triggered"
                    : "AssetDatabase refreshed"
            });
        }
    }
}
