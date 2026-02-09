using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.CLI
{
    [Serializable]
    public class GameObjectInfo
    {
        public string name;
        public string path;
        public int instanceId;
        public List<string> components = new();
        public int childCount;
        public string tag;
        public int layer;
        public bool activeSelf;
    }

    [Serializable]
    public class GameObjectListResult
    {
        public List<GameObjectInfo> gameObjects = new();
    }

    [Serializable]
    public class GameObjectOperationResult
    {
        public bool success;
        public string message;
        public string path;
        public int instanceId;
    }

    [Serializable]
    public class ComponentInfo
    {
        public string typeName;
        public string fullTypeName;
        public List<ComponentPropertyInfo> properties = new();
    }

    [Serializable]
    public class ComponentPropertyInfo
    {
        public string name;
        public string type;
        public string value;
        public bool isReadOnly;
    }

    public class GameObjectHandler : ICLIHandler
    {
        public List<string> GetCommands()
        {
            return new List<string>
            {
                "unity_list_gameobjects",
                "unity_create_gameobject",
                "unity_get_gameobject",
                "unity_update_gameobject",
                "unity_delete_gameobject",
                "unity_add_component",
                "unity_set_transform",
                "unity_get_component",
                "unity_set_component"
            };
        }

        public string Execute(string command, string parameters)
        {
            var args = string.IsNullOrEmpty(parameters) ? new JObject() : JObject.Parse(parameters);
            
            return command switch
            {
                "unity_list_gameobjects" => ListGameObjects(args),
                "unity_create_gameobject" => CreateGameObject(args),
                "unity_get_gameobject" => GetGameObject(args),
                "unity_update_gameobject" => UpdateGameObject(args),
                "unity_delete_gameobject" => DeleteGameObject(args),
                "unity_add_component" => AddComponent(args),
                "unity_set_transform" => SetTransform(args),
                "unity_get_component" => GetComponent(args),
                "unity_set_component" => SetComponent(args),
                _ => JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = $"Unknown command: {command}" })
            };
        }

        private string ListGameObjects(JObject args)
        {
            var pathFilter = args["path"]?.ToString();
            var result = new GameObjectListResult();

            if (!string.IsNullOrEmpty(pathFilter))
            {
                var parent = GameObject.Find(pathFilter);
                if (parent != null)
                {
                    foreach (Transform child in parent.transform)
                    {
                        result.gameObjects.Add(CreateGameObjectInfo(child.gameObject));
                    }
                }
            }
            else
            {
                var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var go in rootObjects)
                {
                    result.gameObjects.Add(CreateGameObjectInfo(go));
                }
            }

            return JsonConvert.SerializeObject(result);
        }

        private string CreateGameObject(JObject args)
        {
            var name = args["name"]?.ToString();
            var parent = args["parent"]?.ToString();
            var primitiveType = args["primitiveType"]?.ToString();

            if (string.IsNullOrEmpty(name))
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = "name is required" });
            }

            GameObject go;
            if (!string.IsNullOrEmpty(primitiveType) && Enum.TryParse<PrimitiveType>(primitiveType, true, out var pt))
            {
                go = GameObject.CreatePrimitive(pt);
                go.name = name;
            }
            else
            {
                go = new GameObject(name);
            }

            if (!string.IsNullOrEmpty(parent))
            {
                var parentGo = GameObject.Find(parent);
                if (parentGo != null)
                {
                    go.transform.SetParent(parentGo.transform, false);
                }
            }

            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            EditorUtility.SetDirty(go);

            return JsonConvert.SerializeObject(new GameObjectOperationResult
            {
                success = true,
                message = "GameObject created",
                path = GetGameObjectPath(go),
                instanceId = go.GetInstanceID()
            });
        }

        private string GetGameObject(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = "path is required" });
            }
            
            var go = GameObject.Find(path);

            if (go == null)
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = $"GameObject not found: {path}" });
            }

            return JsonConvert.SerializeObject(CreateGameObjectInfo(go));
        }

        private string UpdateGameObject(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = "path is required" });
            }
            
            var go = GameObject.Find(path);

            if (go == null)
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = $"GameObject not found: {path}" });
            }

            Undo.RecordObject(go, "Update GameObject");

            var newName = args["newName"]?.ToString();
            var tag = args["tag"]?.ToString();
            var layer = args["layer"]?.ToObject<int?>() ?? -1;
            var active = args["active"]?.ToObject<bool?>();

            if (!string.IsNullOrEmpty(newName))
                go.name = newName;

            if (!string.IsNullOrEmpty(tag))
                go.tag = tag;

            if (layer >= 0)
                go.layer = layer;

            if (active.HasValue)
                go.SetActive(active.Value);

            EditorUtility.SetDirty(go);

            return JsonConvert.SerializeObject(new GameObjectOperationResult
            {
                success = true,
                message = "GameObject updated",
                path = GetGameObjectPath(go)
            });
        }

        private string DeleteGameObject(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = "path is required" });
            }
            
            var go = GameObject.Find(path);

            if (go == null)
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = $"GameObject not found: {path}" });
            }

            Undo.DestroyObjectImmediate(go);

            return JsonConvert.SerializeObject(new GameObjectOperationResult { success = true, message = "GameObject deleted", path = path });
        }

        private string AddComponent(JObject args)
        {
            var path = args["path"]?.ToString();
            var componentType = args["componentType"]?.ToString();
            
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(componentType))
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = "path and componentType are required" });
            }
            
            var go = GameObject.Find(path);

            if (go == null)
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = $"GameObject not found: {path}" });
            }

            var type = FindComponentType(componentType);
            if (type == null)
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = $"Component type not found: {componentType}" });
            }

            Undo.AddComponent(go, type);

            return JsonConvert.SerializeObject(new GameObjectOperationResult { success = true, message = $"Component {componentType} added", path = path });
        }

        private string SetTransform(JObject args)
        {
            var path = args["path"]?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = "path is required" });
            }
            
            var go = GameObject.Find(path);

            if (go == null)
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = $"GameObject not found: {path}" });
            }

            Undo.RecordObject(go.transform, "Set Transform");

            var changes = new List<string>();
            
            var position = args["position"] as JObject;
            if (position != null)
            {
                var newPos = new Vector3(
                    position["x"]?.ToObject<float>() ?? 0,
                    position["y"]?.ToObject<float>() ?? 0,
                    position["z"]?.ToObject<float>() ?? 0
                );
                go.transform.position = newPos;
                changes.Add($"position={newPos}");
            }

            var rotation = args["rotation"] as JObject;
            if (rotation != null)
            {
                var newRot = new Vector3(
                    rotation["x"]?.ToObject<float>() ?? 0,
                    rotation["y"]?.ToObject<float>() ?? 0,
                    rotation["z"]?.ToObject<float>() ?? 0
                );
                go.transform.rotation = Quaternion.Euler(newRot);
                changes.Add($"rotation={newRot}");
            }

            var scale = args["scale"] as JObject;
            if (scale != null)
            {
                var newScale = new Vector3(
                    scale["x"]?.ToObject<float>() ?? 1,
                    scale["y"]?.ToObject<float>() ?? 1,
                    scale["z"]?.ToObject<float>() ?? 1
                );
                go.transform.localScale = newScale;
                changes.Add($"scale={newScale}");
            }

            EditorUtility.SetDirty(go);

            return JsonConvert.SerializeObject(new GameObjectOperationResult
            {
                success = true,
                message = changes.Count > 0 ? $"Transform updated: {string.Join(", ", changes)}" : "No changes applied",
                path = GetGameObjectPath(go)
            });
        }

        private string GetComponent(JObject args)
        {
            var path = args["path"]?.ToString();
            var componentType = args["componentType"]?.ToString();
            
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(componentType))
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = "path and componentType are required" });
            }
            
            var go = GameObject.Find(path);

            if (go == null)
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = $"GameObject not found: {path}" });
            }

            var type = FindComponentType(componentType);
            if (type == null)
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = $"Component type not found: {componentType}" });
            }

            var component = go.GetComponent(type);
            if (component == null)
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = $"Component not found on GameObject: {componentType}" });
            }

            var info = new ComponentInfo
            {
                typeName = type.Name,
                fullTypeName = type.FullName
            };

            var serializedObject = new SerializedObject(component);
            var iterator = serializedObject.GetIterator();
            
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (iterator.name == "m_Script") continue;
                    
                    var propInfo = new ComponentPropertyInfo
                    {
                        name = iterator.name,
                        type = iterator.propertyType.ToString(),
                        isReadOnly = !iterator.editable,
                        value = GetPropertyValueString(iterator)
                    };

                    info.properties.Add(propInfo);
                } while (iterator.NextVisible(false));
            }

            return JsonConvert.SerializeObject(info);
        }

        private string SetComponent(JObject args)
        {
            var path = args["path"]?.ToString();
            var componentType = args["componentType"]?.ToString();
            var properties = args["properties"] as JObject;
            
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(componentType))
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = "path and componentType are required" });
            }
            
            var go = GameObject.Find(path);

            if (go == null)
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = $"GameObject not found: {path}" });
            }

            var type = FindComponentType(componentType);
            if (type == null)
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = $"Component type not found: {componentType}" });
            }

            var component = go.GetComponent(type);
            if (component == null)
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = $"Component not found on GameObject: {componentType}" });
            }

            if (properties == null || !properties.HasValues)
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = "properties is required" });
            }

            try
            {
                var serializedObject = new SerializedObject(component);
                Undo.RecordObject(component, "Set Component Properties");

                var changedProps = new List<string>();
                foreach (var kvp in properties)
                {
                    var prop = serializedObject.FindProperty(kvp.Key);
                    if (prop == null) continue;

                    SetPropertyValue(prop, kvp.Value);
                    changedProps.Add(kvp.Key);
                }

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(component);

                return JsonConvert.SerializeObject(new GameObjectOperationResult
                {
                    success = true,
                    message = $"Updated properties: {string.Join(", ", changedProps)}",
                    path = path
                });
            }
            catch (Exception e)
            {
                return JsonConvert.SerializeObject(new GameObjectOperationResult { success = false, message = $"Failed to set properties: {e.Message}" });
            }
        }

        private GameObjectInfo CreateGameObjectInfo(GameObject go)
        {
            var info = new GameObjectInfo
            {
                name = go.name,
                path = GetGameObjectPath(go),
                instanceId = go.GetInstanceID(),
                childCount = go.transform.childCount,
                tag = go.tag,
                layer = go.layer,
                activeSelf = go.activeSelf
            };

            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp != null)
                    info.components.Add(comp.GetType().Name);
            }

            return info;
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

        private Type FindComponentType(string typeName)
        {
            var builtinTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                ["BoxCollider"] = typeof(BoxCollider),
                ["SphereCollider"] = typeof(SphereCollider),
                ["CapsuleCollider"] = typeof(CapsuleCollider),
                ["MeshCollider"] = typeof(MeshCollider),
                ["Rigidbody"] = typeof(Rigidbody),
                ["AudioSource"] = typeof(AudioSource),
                ["Light"] = typeof(Light),
                ["Camera"] = typeof(Camera),
                ["Canvas"] = typeof(Canvas),
                ["Image"] = typeof(UnityEngine.UI.Image),
                ["Text"] = typeof(UnityEngine.UI.Text),
                ["Button"] = typeof(UnityEngine.UI.Button),
            };

            if (builtinTypes.TryGetValue(typeName, out var builtinType))
                return builtinType;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null && typeof(Component).IsAssignableFrom(type))
                    return type;
            }

            var unityType = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (unityType != null && typeof(Component).IsAssignableFrom(unityType))
                return unityType;

            return null;
        }

        private string GetPropertyValueString(SerializedProperty prop)
        {
            return prop.propertyType switch
            {
                SerializedPropertyType.Integer => prop.intValue.ToString(),
                SerializedPropertyType.Boolean => prop.boolValue.ToString(),
                SerializedPropertyType.Float => prop.floatValue.ToString(),
                SerializedPropertyType.String => prop.stringValue,
                SerializedPropertyType.Color => JsonConvert.SerializeObject(new { r = prop.colorValue.r, g = prop.colorValue.g, b = prop.colorValue.b, a = prop.colorValue.a }),
                SerializedPropertyType.Vector2 => JsonConvert.SerializeObject(new { x = prop.vector2Value.x, y = prop.vector2Value.y }),
                SerializedPropertyType.Vector3 => JsonConvert.SerializeObject(new { x = prop.vector3Value.x, y = prop.vector3Value.y, z = prop.vector3Value.z }),
                SerializedPropertyType.Enum => prop.enumNames[prop.enumValueIndex],
                SerializedPropertyType.ObjectReference => prop.objectReferenceValue != null ? AssetDatabase.GetAssetPath(prop.objectReferenceValue) : "null",
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
            }
        }
    }
}
