using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.CLI
{
    [Serializable]
    public class PrefabListResult
    {
        public List<PrefabInfo> prefabs = new();
    }

    [Serializable]
    public class PrefabInfo
    {
        public string name;
        public string path;
        public string guid;
        public int childCount;
        public List<string> components = new();
    }

    [Serializable]
    public class PrefabOperationResult
    {
        public bool success;
        public string message;
        public string path;
        public string guid;
        public int instanceId;
    }

    public class PrefabHandler : ICLIHandler
    {
        public List<string> GetCommands()
        {
            return new List<string>
            {
                "unity_list_prefabs",
                "unity_create_prefab",
                "unity_get_prefab_info",
                "unity_update_prefab",
                "unity_delete_prefab",
                "unity_instantiate_prefab",
                "unity_apply_prefab_overrides",
                "unity_revert_prefab_overrides"
            };
        }

        public string Execute(string command, string parameters)
        {
            var args = string.IsNullOrEmpty(parameters) ? new JObject() : JObject.Parse(parameters);
            
            return command switch
            {
                "unity_list_prefabs" => ListPrefabs(args),
                "unity_create_prefab" => CreatePrefab(args),
                "unity_get_prefab_info" => GetPrefabInfo(args),
                "unity_update_prefab" => UpdatePrefab(args),
                "unity_delete_prefab" => DeletePrefab(args),
                "unity_instantiate_prefab" => InstantiatePrefab(args),
                "unity_apply_prefab_overrides" => ApplyPrefabOverrides(args),
                "unity_revert_prefab_overrides" => RevertPrefabOverrides(args),
                _ => JsonConvert.SerializeObject(new PrefabOperationResult { success = false, message = $"Unknown command: {command}" })
            };
        }

        private string ListPrefabs(JObject args)
        {
            var pathFilter = args["path"]?.ToString();
            var guids = AssetDatabase.FindAssets("t:Prefab");
            var result = new PrefabListResult();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (!string.IsNullOrEmpty(pathFilter) && !path.StartsWith(pathFilter))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var info = new PrefabInfo
                {
                    name = prefab.name,
                    path = path,
                    guid = guid,
                    childCount = prefab.transform.childCount
                };

                foreach (var comp in prefab.GetComponents<Component>())
                {
                    if (comp != null)
                        info.components.Add(comp.GetType().Name);
                }

                result.prefabs.Add(info);
            }

            return JsonConvert.SerializeObject(result);
        }

        private string CreatePrefab(JObject args)
        {
            var name = args["name"]?.ToString();
            var path = args["path"]?.ToString();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new PrefabOperationResult { success = false, message = "name and path are required" });
            }

            var fullPath = path.EndsWith("/") ? path + name + ".prefab" : path + "/" + name + ".prefab";

            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var go = new GameObject(name);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, fullPath);
            UnityEngine.Object.DestroyImmediate(go);

            var guid = AssetDatabase.AssetPathToGUID(fullPath);

            return JsonConvert.SerializeObject(new PrefabOperationResult { success = true, message = "Prefab created", path = fullPath, guid = guid });
        }

        private string GetPrefabInfo(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new PrefabOperationResult { success = false, message = "path is required" });
            }
            
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                return JsonConvert.SerializeObject(new PrefabOperationResult { success = false, message = $"Prefab not found: {path}" });
            }

            var guid = AssetDatabase.AssetPathToGUID(path);
            var info = new PrefabInfo
            {
                name = prefab.name,
                path = path,
                guid = guid,
                childCount = prefab.transform.childCount
            };

            foreach (var comp in prefab.GetComponents<Component>())
            {
                if (comp != null)
                    info.components.Add(comp.GetType().Name);
            }

            return JsonConvert.SerializeObject(info);
        }

        private string UpdatePrefab(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new PrefabOperationResult { success = false, message = "path is required" });
            }
            
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                return JsonConvert.SerializeObject(new PrefabOperationResult { success = false, message = $"Prefab not found: {path}" });
            }

            var newName = args["newName"]?.ToString();
            var tag = args["tag"]?.ToString();
            var layer = args["layer"]?.ToObject<int?>() ?? -1;

            var changes = new List<string>();

            using (var editScope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                var root = editScope.prefabContentsRoot;

                if (!string.IsNullOrEmpty(newName))
                {
                    root.name = newName;
                    changes.Add($"name={newName}");
                }

                if (!string.IsNullOrEmpty(tag))
                {
                    root.tag = tag;
                    changes.Add($"tag={tag}");
                }

                if (layer >= 0)
                {
                    root.layer = layer;
                    changes.Add($"layer={layer}");
                }
            }

            if (!string.IsNullOrEmpty(newName))
            {
                AssetDatabase.RenameAsset(path, newName);
            }

            return JsonConvert.SerializeObject(new PrefabOperationResult { success = true, message = $"Updated: {string.Join(", ", changes)}", path = path });
        }

        private string DeletePrefab(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new PrefabOperationResult { success = false, message = "path is required" });
            }

            if (!File.Exists(path))
            {
                return JsonConvert.SerializeObject(new PrefabOperationResult { success = false, message = $"Prefab not found: {path}" });
            }

            AssetDatabase.DeleteAsset(path);

            return JsonConvert.SerializeObject(new PrefabOperationResult { success = true, message = "Prefab deleted", path = path });
        }

        private string InstantiatePrefab(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new PrefabOperationResult { success = false, message = "path is required" });
            }
            
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                return JsonConvert.SerializeObject(new PrefabOperationResult { success = false, message = $"Prefab not found: {path}" });
            }

            Transform parent = null;
            var parentPath = args["parent"]?.ToString();
            if (!string.IsNullOrEmpty(parentPath))
            {
                var parentGo = GameObject.Find(parentPath);
                if (parentGo != null)
                    parent = parentGo.transform;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);

            var position = args["position"] as JObject;
            if (position != null)
            {
                instance.transform.position = new Vector3(
                    position["x"]?.ToObject<float>() ?? 0,
                    position["y"]?.ToObject<float>() ?? 0,
                    position["z"]?.ToObject<float>() ?? 0
                );
            }

            var rotation = args["rotation"] as JObject;
            if (rotation != null)
            {
                instance.transform.rotation = Quaternion.Euler(
                    rotation["x"]?.ToObject<float>() ?? 0,
                    rotation["y"]?.ToObject<float>() ?? 0,
                    rotation["z"]?.ToObject<float>() ?? 0
                );
            }

            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Prefab");

            return JsonConvert.SerializeObject(new PrefabOperationResult
            {
                success = true,
                message = "Prefab instantiated",
                path = GetGameObjectPath(instance),
                instanceId = instance.GetInstanceID()
            });
        }

        private string ApplyPrefabOverrides(JObject args)
        {
            var instancePath = args["instancePath"]?.ToString();
            
            if (string.IsNullOrEmpty(instancePath))
            {
                return JsonConvert.SerializeObject(new PrefabOperationResult { success = false, message = "instancePath is required" });
            }
            
            var instance = GameObject.Find(instancePath);

            if (instance == null)
            {
                return JsonConvert.SerializeObject(new PrefabOperationResult { success = false, message = $"Instance not found: {instancePath}" });
            }

            if (!PrefabUtility.IsPartOfPrefabInstance(instance))
            {
                return JsonConvert.SerializeObject(new PrefabOperationResult { success = false, message = "Object is not a prefab instance" });
            }

            var targetPrefab = args["targetPrefab"]?.ToString();
            var prefabPath = string.IsNullOrEmpty(targetPrefab) 
                ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance)
                : targetPrefab;

            PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.UserAction);

            return JsonConvert.SerializeObject(new PrefabOperationResult { success = true, message = "Overrides applied", path = prefabPath });
        }

        private string RevertPrefabOverrides(JObject args)
        {
            var instancePath = args["instancePath"]?.ToString();
            
            if (string.IsNullOrEmpty(instancePath))
            {
                return JsonConvert.SerializeObject(new PrefabOperationResult { success = false, message = "instancePath is required" });
            }
            
            var instance = GameObject.Find(instancePath);

            if (instance == null)
            {
                return JsonConvert.SerializeObject(new PrefabOperationResult { success = false, message = $"Instance not found: {instancePath}" });
            }

            if (!PrefabUtility.IsPartOfPrefabInstance(instance))
            {
                return JsonConvert.SerializeObject(new PrefabOperationResult { success = false, message = "Object is not a prefab instance" });
            }

            PrefabUtility.RevertPrefabInstance(instance, InteractionMode.UserAction);

            return JsonConvert.SerializeObject(new PrefabOperationResult { success = true, message = "Overrides reverted", path = instancePath });
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
    }
}
