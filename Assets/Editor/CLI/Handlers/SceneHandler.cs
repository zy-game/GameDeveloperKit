using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Editor.CLI
{
    [Serializable]
    public class SceneListResult
    {
        public List<SceneInfo> scenes = new();
    }

    [Serializable]
    public class SceneInfo
    {
        public string name;
        public string path;
        public string guid;
        public bool isLoaded;
    }

    [Serializable]
    public class SceneOperationResult
    {
        public bool success;
        public string message;
        public string path;
        public string guid;
    }

    public class SceneHandler : ICLIHandler
    {
        public List<string> GetCommands()
        {
            return new List<string>
            {
                "unity_list_scenes",
                "unity_create_scene",
                "unity_open_scene",
                "unity_save_scene",
                "unity_delete_scene",
                "unity_get_scene_info"
            };
        }

        public string Execute(string command, string parameters)
        {
            var args = string.IsNullOrEmpty(parameters) ? new JObject() : JObject.Parse(parameters);
            
            return command switch
            {
                "unity_list_scenes" => ListScenes(args),
                "unity_create_scene" => CreateScene(args),
                "unity_open_scene" => OpenScene(args),
                "unity_save_scene" => SaveScene(),
                "unity_delete_scene" => DeleteScene(args),
                "unity_get_scene_info" => GetSceneInfo(args),
                _ => JsonConvert.SerializeObject(new SceneOperationResult { success = false, message = $"Unknown command: {command}" })
            };
        }

        private string ListScenes(JObject args)
        {
            var pathFilter = args["path"]?.ToString();
            var guids = AssetDatabase.FindAssets("t:Scene");
            var result = new SceneListResult();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (!string.IsNullOrEmpty(pathFilter) && !path.StartsWith(pathFilter))
                    continue;

                var isLoaded = false;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    if (SceneManager.GetSceneAt(i).path == path)
                    {
                        isLoaded = true;
                        break;
                    }
                }

                result.scenes.Add(new SceneInfo
                {
                    name = Path.GetFileNameWithoutExtension(path),
                    path = path,
                    guid = guid,
                    isLoaded = isLoaded
                });
            }

            return JsonConvert.SerializeObject(result);
        }

        private string CreateScene(JObject args)
        {
            var name = args["name"]?.ToString();
            var path = args["path"]?.ToString();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new SceneOperationResult { success = false, message = "name and path are required" });
            }

            var fullPath = path.EndsWith("/") ? path + name + ".unity" : path + "/" + name + ".unity";

            if (File.Exists(fullPath))
            {
                return JsonConvert.SerializeObject(new SceneOperationResult { success = false, message = $"Scene already exists: {fullPath}" });
            }

            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(newScene, fullPath);
            AssetDatabase.Refresh();

            var guid = AssetDatabase.AssetPathToGUID(fullPath);

            return JsonConvert.SerializeObject(new SceneOperationResult { success = true, message = "Scene created", path = fullPath, guid = guid });
        }

        private string OpenScene(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new SceneOperationResult { success = false, message = "path is required" });
            }

            if (!File.Exists(path))
            {
                return JsonConvert.SerializeObject(new SceneOperationResult { success = false, message = $"Scene not found: {path}" });
            }

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            EditorSceneManager.OpenScene(path);

            return JsonConvert.SerializeObject(new SceneOperationResult { success = true, message = "Scene opened", path = path });
        }

        private string SaveScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(activeScene);

            return JsonConvert.SerializeObject(new SceneOperationResult { success = true, message = "Scene saved", path = activeScene.path });
        }

        private string DeleteScene(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new SceneOperationResult { success = false, message = "path is required" });
            }

            if (!File.Exists(path))
            {
                return JsonConvert.SerializeObject(new SceneOperationResult { success = false, message = $"Scene not found: {path}" });
            }

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.path == path)
            {
                return JsonConvert.SerializeObject(new SceneOperationResult { success = false, message = "Cannot delete the currently active scene" });
            }

            AssetDatabase.DeleteAsset(path);

            return JsonConvert.SerializeObject(new SceneOperationResult { success = true, message = "Scene deleted", path = path });
        }

        private string GetSceneInfo(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new SceneOperationResult { success = false, message = "path is required" });
            }
            
            var guid = AssetDatabase.AssetPathToGUID(path);

            if (string.IsNullOrEmpty(guid))
            {
                return JsonConvert.SerializeObject(new SceneOperationResult { success = false, message = $"Scene not found: {path}" });
            }

            var isLoaded = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).path == path)
                {
                    isLoaded = true;
                    break;
                }
            }

            return JsonConvert.SerializeObject(new SceneInfo
            {
                name = Path.GetFileNameWithoutExtension(path),
                path = path,
                guid = guid,
                isLoaded = isLoaded
            });
        }
    }
}
