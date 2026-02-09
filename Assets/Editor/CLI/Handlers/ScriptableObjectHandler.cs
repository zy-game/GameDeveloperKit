using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.CLI
{
    [Serializable]
    public class SOListResult
    {
        public List<SOInfo> assets = new();
    }

    [Serializable]
    public class SOInfo
    {
        public string name;
        public string path;
        public string guid;
        public string typeName;
        public string jsonData;
    }

    [Serializable]
    public class SOOperationResult
    {
        public bool success;
        public string message;
        public string path;
        public string guid;
    }

    public class ScriptableObjectHandler : ICLIHandler
    {
        public List<string> GetCommands()
        {
            return new List<string>
            {
                "unity_list_scriptable_objects",
                "unity_create_scriptable_object",
                "unity_get_scriptable_object",
                "unity_update_scriptable_object",
                "unity_delete_scriptable_object"
            };
        }

        public string Execute(string command, string parameters)
        {
            var args = string.IsNullOrEmpty(parameters) ? new JObject() : JObject.Parse(parameters);
            
            return command switch
            {
                "unity_list_scriptable_objects" => ListScriptableObjects(args),
                "unity_create_scriptable_object" => CreateScriptableObject(args),
                "unity_get_scriptable_object" => GetScriptableObject(args),
                "unity_update_scriptable_object" => UpdateScriptableObject(args),
                "unity_delete_scriptable_object" => DeleteScriptableObject(args),
                _ => JsonConvert.SerializeObject(new SOOperationResult { success = false, message = $"Unknown command: {command}" })
            };
        }

        private string ListScriptableObjects(JObject args)
        {
            var typeFilter = args["type"]?.ToString();
            var pathFilter = args["path"]?.ToString();
            
            var guids = AssetDatabase.FindAssets("t:ScriptableObject");
            var result = new SOListResult();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (!string.IsNullOrEmpty(pathFilter) && !path.StartsWith(pathFilter))
                    continue;

                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null) continue;

                var typeName = asset.GetType().Name;
                if (!string.IsNullOrEmpty(typeFilter))
                {
                    if (!typeName.Contains(typeFilter, StringComparison.OrdinalIgnoreCase) &&
                        !asset.GetType().FullName.Contains(typeFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                result.assets.Add(new SOInfo
                {
                    name = asset.name,
                    path = path,
                    guid = guid,
                    typeName = asset.GetType().FullName
                });
            }

            return JsonConvert.SerializeObject(result);
        }

        private string CreateScriptableObject(JObject args)
        {
            var typeName = args["type"]?.ToString();
            var name = args["name"]?.ToString();
            var path = args["path"]?.ToString();

            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new SOOperationResult
                {
                    success = false,
                    message = "type, name and path are required"
                });
            }

            var type = FindType(typeName);
            if (type == null)
            {
                return JsonConvert.SerializeObject(new SOOperationResult
                {
                    success = false,
                    message = $"Type not found: {typeName}"
                });
            }

            if (!typeof(ScriptableObject).IsAssignableFrom(type))
            {
                return JsonConvert.SerializeObject(new SOOperationResult
                {
                    success = false,
                    message = $"Type is not a ScriptableObject: {typeName}"
                });
            }

            var asset = ScriptableObject.CreateInstance(type);
            asset.name = name;

            var fullPath = path.EndsWith("/") ? path + name + ".asset" : path + "/" + name + ".asset";

            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            AssetDatabase.CreateAsset(asset, fullPath);
            AssetDatabase.SaveAssets();

            var guid = AssetDatabase.AssetPathToGUID(fullPath);

            return JsonConvert.SerializeObject(new SOOperationResult
            {
                success = true,
                message = "ScriptableObject created",
                path = fullPath,
                guid = guid
            });
        }

        private string GetScriptableObject(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new SOOperationResult { success = false, message = "path is required" });
            }
            
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

            if (asset == null)
            {
                return JsonConvert.SerializeObject(new SOOperationResult
                {
                    success = false,
                    message = $"ScriptableObject not found: {path}"
                });
            }

            var guid = AssetDatabase.AssetPathToGUID(path);
            var info = new SOInfo
            {
                name = asset.name,
                path = path,
                guid = guid,
                typeName = asset.GetType().FullName,
                jsonData = SerializeToJson(asset)
            };

            return JsonConvert.SerializeObject(info);
        }

        private string UpdateScriptableObject(JObject args)
        {
            var path = args["path"]?.ToString();
            var fields = args["fields"] as JObject;
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new SOOperationResult { success = false, message = "path is required" });
            }
            
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

            if (asset == null)
            {
                return JsonConvert.SerializeObject(new SOOperationResult
                {
                    success = false,
                    message = $"ScriptableObject not found: {path}"
                });
            }

            if (fields == null || !fields.HasValues)
            {
                return JsonConvert.SerializeObject(new SOOperationResult
                {
                    success = false,
                    message = "fields is required"
                });
            }

            try
            {
                Undo.RecordObject(asset, "Update ScriptableObject");
                var changes = new List<string>();

                var serializedObject = new SerializedObject(asset);

                foreach (var kvp in fields)
                {
                    var prop = serializedObject.FindProperty(kvp.Key);
                    if (prop == null) continue;

                    SetPropertyValue(prop, kvp.Value);
                    changes.Add(kvp.Key);
                }

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();

                return JsonConvert.SerializeObject(new SOOperationResult
                {
                    success = true,
                    message = $"Updated: {string.Join(", ", changes)}",
                    path = path
                });
            }
            catch (Exception e)
            {
                return JsonConvert.SerializeObject(new SOOperationResult
                {
                    success = false,
                    message = $"Failed to update: {e.Message}"
                });
            }
        }

        private string DeleteScriptableObject(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new SOOperationResult { success = false, message = "path is required" });
            }

            if (!File.Exists(path))
            {
                return JsonConvert.SerializeObject(new SOOperationResult
                {
                    success = false,
                    message = $"ScriptableObject not found: {path}"
                });
            }

            AssetDatabase.DeleteAsset(path);

            return JsonConvert.SerializeObject(new SOOperationResult
            {
                success = true,
                message = "ScriptableObject deleted",
                path = path
            });
        }

        private Type FindType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                if (type != null) return type;
            }

            return null;
        }

        private string SerializeToJson(ScriptableObject asset)
        {
            var serializedObject = new SerializedObject(asset);
            var dict = new Dictionary<string, object>();

            var iterator = serializedObject.GetIterator();
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (iterator.name == "m_Script") continue;
                    dict[iterator.name] = GetPropertyValue(iterator);
                } while (iterator.NextVisible(false));
            }

            return JsonConvert.SerializeObject(dict, Formatting.Indented);
        }

        private object GetPropertyValue(SerializedProperty prop)
        {
            return prop.propertyType switch
            {
                SerializedPropertyType.Integer => prop.intValue,
                SerializedPropertyType.Boolean => prop.boolValue,
                SerializedPropertyType.Float => prop.floatValue,
                SerializedPropertyType.String => prop.stringValue,
                SerializedPropertyType.Color => new { r = prop.colorValue.r, g = prop.colorValue.g, b = prop.colorValue.b, a = prop.colorValue.a },
                SerializedPropertyType.Vector2 => new { x = prop.vector2Value.x, y = prop.vector2Value.y },
                SerializedPropertyType.Vector3 => new { x = prop.vector3Value.x, y = prop.vector3Value.y, z = prop.vector3Value.z },
                SerializedPropertyType.Vector4 => new { x = prop.vector4Value.x, y = prop.vector4Value.y, z = prop.vector4Value.z, w = prop.vector4Value.w },
                SerializedPropertyType.Enum => prop.enumNames[prop.enumValueIndex],
                SerializedPropertyType.ObjectReference => prop.objectReferenceValue != null ? AssetDatabase.GetAssetPath(prop.objectReferenceValue) : null,
                SerializedPropertyType.ArraySize => prop.intValue,
                _ => "(complex)"
            };
        }

        private void SetPropertyValue(SerializedProperty prop, JToken value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = value.ToObject<int>();
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = value.ToObject<bool>();
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = value.ToObject<float>();
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = value.ToString();
                    break;
                case SerializedPropertyType.Enum:
                    if (value.Type == JTokenType.String)
                    {
                        var index = Array.IndexOf(prop.enumNames, value.ToString());
                        if (index >= 0) prop.enumValueIndex = index;
                    }
                    else
                    {
                        prop.enumValueIndex = value.ToObject<int>();
                    }
                    break;
                case SerializedPropertyType.Vector3:
                    if (value is JObject v3obj)
                    {
                        prop.vector3Value = new Vector3(
                            v3obj["x"]?.ToObject<float>() ?? 0,
                            v3obj["y"]?.ToObject<float>() ?? 0,
                            v3obj["z"]?.ToObject<float>() ?? 0
                        );
                    }
                    break;
                case SerializedPropertyType.Color:
                    if (value is JObject colorObj)
                    {
                        prop.colorValue = new Color(
                            colorObj["r"]?.ToObject<float>() ?? 0,
                            colorObj["g"]?.ToObject<float>() ?? 0,
                            colorObj["b"]?.ToObject<float>() ?? 0,
                            colorObj["a"]?.ToObject<float>() ?? 1
                        );
                    }
                    break;
                case SerializedPropertyType.ObjectReference:
                    var assetPath = value.ToString();
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                        if (asset != null)
                            prop.objectReferenceValue = asset;
                    }
                    break;
            }
        }
    }
}
