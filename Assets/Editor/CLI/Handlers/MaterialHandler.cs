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
    public class MaterialListResult
    {
        public List<MaterialInfo> materials = new();
    }

    [Serializable]
    public class MaterialInfo
    {
        public string name;
        public string path;
        public string guid;
        public string shaderName;
        public int renderQueue;
    }

    [Serializable]
    public class MaterialOperationResult
    {
        public bool success;
        public string message;
        public string path;
        public string guid;
    }

    public class MaterialHandler : ICLIHandler
    {
        public List<string> GetCommands()
        {
            return new List<string>
            {
                "unity_list_materials",
                "unity_create_material",
                "unity_get_material",
                "unity_update_material",
                "unity_delete_material"
            };
        }

        public string Execute(string command, string parameters)
        {
            var args = string.IsNullOrEmpty(parameters) ? new JObject() : JObject.Parse(parameters);
            
            return command switch
            {
                "unity_list_materials" => ListMaterials(args),
                "unity_create_material" => CreateMaterial(args),
                "unity_get_material" => GetMaterial(args),
                "unity_update_material" => UpdateMaterial(args),
                "unity_delete_material" => DeleteMaterial(args),
                _ => JsonConvert.SerializeObject(new MaterialOperationResult { success = false, message = $"Unknown command: {command}" })
            };
        }

        private string ListMaterials(JObject args)
        {
            var pathFilter = args["path"]?.ToString();
            var shaderFilter = args["shader"]?.ToString();
            var guids = AssetDatabase.FindAssets("t:Material");
            var result = new MaterialListResult();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (!string.IsNullOrEmpty(pathFilter) && !path.StartsWith(pathFilter))
                    continue;

                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;

                if (!string.IsNullOrEmpty(shaderFilter))
                {
                    if (material.shader == null || !material.shader.name.Contains(shaderFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                result.materials.Add(new MaterialInfo
                {
                    name = material.name,
                    path = path,
                    guid = guid,
                    shaderName = material.shader?.name ?? "None",
                    renderQueue = material.renderQueue
                });
            }

            return JsonConvert.SerializeObject(result);
        }

        private string CreateMaterial(JObject args)
        {
            var name = args["name"]?.ToString();
            var path = args["path"]?.ToString();
            var shaderName = args["shader"]?.ToString();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new MaterialOperationResult { success = false, message = "name and path are required" });
            }

            var shader = string.IsNullOrEmpty(shaderName) ? Shader.Find("Standard") : Shader.Find(shaderName);

            if (shader == null)
            {
                return JsonConvert.SerializeObject(new MaterialOperationResult { success = false, message = $"Shader not found: {shaderName}" });
            }

            var material = new Material(shader);
            var fullPath = path.EndsWith("/") ? path + name + ".mat" : path + "/" + name + ".mat";

            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            AssetDatabase.CreateAsset(material, fullPath);
            AssetDatabase.SaveAssets();

            var guid = AssetDatabase.AssetPathToGUID(fullPath);

            return JsonConvert.SerializeObject(new MaterialOperationResult { success = true, message = "Material created", path = fullPath, guid = guid });
        }

        private string GetMaterial(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new MaterialOperationResult { success = false, message = "path is required" });
            }
            
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                return JsonConvert.SerializeObject(new MaterialOperationResult { success = false, message = $"Material not found: {path}" });
            }

            var guid = AssetDatabase.AssetPathToGUID(path);
            var info = new
            {
                name = material.name,
                path = path,
                guid = guid,
                shaderName = material.shader?.name ?? "None",
                renderQueue = material.renderQueue,
                keywords = material.shaderKeywords
            };

            return JsonConvert.SerializeObject(info);
        }

        private string UpdateMaterial(JObject args)
        {
            var path = args["path"]?.ToString();
            var properties = args["properties"] as JObject;
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new MaterialOperationResult { success = false, message = "path is required" });
            }
            
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                return JsonConvert.SerializeObject(new MaterialOperationResult { success = false, message = $"Material not found: {path}" });
            }

            if (properties == null || !properties.HasValues)
            {
                return JsonConvert.SerializeObject(new MaterialOperationResult { success = false, message = "properties is required" });
            }

            try
            {
                Undo.RecordObject(material, "Update Material");
                var changes = new List<string>();

                foreach (var kvp in properties)
                {
                    if (!material.HasProperty(kvp.Key)) continue;

                    if (kvp.Value is JObject colorObj && colorObj.ContainsKey("r"))
                    {
                        var color = new Color(
                            colorObj["r"]?.ToObject<float>() ?? 0,
                            colorObj["g"]?.ToObject<float>() ?? 0,
                            colorObj["b"]?.ToObject<float>() ?? 0,
                            colorObj["a"]?.ToObject<float>() ?? 1
                        );
                        material.SetColor(kvp.Key, color);
                        changes.Add(kvp.Key);
                    }
                    else if (kvp.Value.Type == JTokenType.Float || kvp.Value.Type == JTokenType.Integer)
                    {
                        material.SetFloat(kvp.Key, kvp.Value.ToObject<float>());
                        changes.Add(kvp.Key);
                    }
                }

                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();

                return JsonConvert.SerializeObject(new MaterialOperationResult { success = true, message = $"Updated: {string.Join(", ", changes)}", path = path });
            }
            catch (Exception e)
            {
                return JsonConvert.SerializeObject(new MaterialOperationResult { success = false, message = $"Failed to update material: {e.Message}" });
            }
        }

        private string DeleteMaterial(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new MaterialOperationResult { success = false, message = "path is required" });
            }

            if (!File.Exists(path))
            {
                return JsonConvert.SerializeObject(new MaterialOperationResult { success = false, message = $"Material not found: {path}" });
            }

            AssetDatabase.DeleteAsset(path);

            return JsonConvert.SerializeObject(new MaterialOperationResult { success = true, message = "Material deleted", path = path });
        }
    }
}
