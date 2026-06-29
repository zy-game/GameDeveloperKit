using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnitySerializedPropertySkill : IUnityBridgeSkill
    {
        public string Name => "unity-property";

        public string Description =>
            "Read and write any serialized field on any component via SerializedObject/SerializedProperty. " +
            "This is the universal Inspector key — use it for Transform, Material, Camera, Light, and any custom component.";

        public string Trigger =>
            "Use when the user wants to read or modify component properties, change Inspector values, " +
            "adjust Transform/RectTransform, set material exposed params, tweak light/camera settings, " +
            "or modify any serialized field on any GameObject component.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("GET", "/property?target=MainCamera&component=Camera",
                "Returns all serialized property names and types on a component."),
            new UnityBridgeSkillEndpoint("GET", "/property/list?target=Cube",
                "Lists all components on a GameObject."),
            new UnityBridgeSkillEndpoint("POST", "/property/get",
                "Reads specific property values from a component.",
                "{\"target\":\"MainCamera\",\"component\":\"Camera\",\"properties\":[\"fieldOfView\",\"nearClipPlane\"]}"),
            new UnityBridgeSkillEndpoint("POST", "/property/set",
                "Writes property values to a component. Supports int, float, bool, string, Vector3, Color, Object ref.",
                "{\"target\":\"MainCamera\",\"component\":\"Camera\",\"properties\":{\"fieldOfView\":90,\"nearClipPlane\":0.05}}")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js property-list --target \"Main Camera\"`",
            "`node scripts/unity-bridge.js property-get --target \"Main Camera\" --component Camera --properties fieldOfView`",
            "`node scripts/unity-bridge.js property-set --target Cube --component Transform --props '{\"m_LocalPosition\":{\"x\":1,\"y\":2,\"z\":3}}'`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Property names must match serialized names (e.g. 'm_LocalPosition' not 'position'). Use the list endpoint to discover names.",
            "Supports int, float, bool, string, Vector2, Vector3, Vector4, Color, Quaternion, enum (by index), and Object references (by asset path).",
            "Changes are applied via SerializedObject.ApplyModifiedProperties, which handles Undo and Prefab overrides automatically."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return (request.Method == "GET" && (path == "property" || path == "property/list"))
                || (request.Method == "POST" && (path == "property/get" || path == "property/set"));
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return path switch
            {
                "property" => ListProperties(request),
                "property/list" => ListComponents(request),
                "property/get" => GetProperties(request),
                "property/set" => SetProperties(request),
                _ => UnityBridgeSkillResponse.Error(404, $"Unknown endpoint: /{path}")
            };
        }

        private static UnityBridgeSkillResponse ListComponents(UnityBridgeSkillRequest request)
        {
            var query = ParseQuery(request.QueryString);
            var targetName = query.GetValueOrDefault("target", "");
            var go = FindGameObject(targetName);
            if (go == null)
                return UnityBridgeSkillResponse.Error(404, $"GameObject not found: {targetName}");

            var components = go.GetComponents<Component>();
            var parts = components.Select(c => "{"
                + $"\"type\":\"{Esc(c.GetType().Name)}\","
                + $"\"fullType\":\"{Esc(c.GetType().FullName)}\""
                + "}").ToArray();

            return UnityBridgeSkillResponse.Success(
                $"{{\"target\":\"{Esc(go.name)}\",\"components\":[{string.Join(",", parts)}],\"count\":{parts.Length}}}");
        }

        private static UnityBridgeSkillResponse ListProperties(UnityBridgeSkillRequest request)
        {
            var query = ParseQuery(request.QueryString);
            var targetName = query.GetValueOrDefault("target", "");
            var componentType = query.GetValueOrDefault("component", "");

            var so = GetSerializedObject(targetName, componentType, out var error);
            if (so == null)
                return error;

            var props = new List<string>();
            var iter = so.GetIterator();
            if (iter.NextVisible(true))
            {
                do
                {
                    props.Add("{"
                        + $"\"name\":\"{Esc(iter.name)}\","
                        + $"\"displayName\":\"{Esc(iter.displayName)}\","
                        + $"\"type\":\"{iter.propertyType}\","
                        + $"\"path\":\"{Esc(iter.propertyPath)}\""
                        + "}");
                } while (iter.NextVisible(false));
            }

            so.Dispose();
            return UnityBridgeSkillResponse.Success(
                $"{{\"target\":\"{Esc(targetName)}\",\"component\":\"{Esc(componentType)}\",\"properties\":[{string.Join(",", props)}],\"count\":{props.Count}}}");
        }

        private static UnityBridgeSkillResponse GetProperties(UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var targetName = body?.GetValueOrDefault("target", "");
            var componentType = body?.GetValueOrDefault("component", "");
            var propNames = ParseStringArray(body?.GetValueOrDefault("properties", ""));

            var so = GetSerializedObject(targetName, componentType, out var error);
            if (so == null)
                return error;

            var results = new List<string>();
            foreach (var propName in propNames)
            {
                var prop = string.IsNullOrWhiteSpace(propName) ? null : so.FindProperty(propName);
                if (prop == null)
                {
                    results.Add($"{{\"name\":\"{Esc(propName)}\",\"error\":\"not found\"}}");
                    continue;
                }

                results.Add(SerializePropertyValue(prop));
            }

            so.Dispose();
            return UnityBridgeSkillResponse.Success(
                $"{{\"target\":\"{Esc(targetName)}\",\"component\":\"{Esc(componentType)}\",\"values\":[{string.Join(",", results)}]}}");
        }

        private static UnityBridgeSkillResponse SetProperties(UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var targetName = body?.GetValueOrDefault("target", "");
            var componentType = body?.GetValueOrDefault("component", "");

            var so = GetSerializedObject(targetName, componentType, out var error);
            if (so == null)
                return error;

            var propsJson = body?.GetValueOrDefault("properties", "");
            if (string.IsNullOrWhiteSpace(propsJson))
            {
                so.Dispose();
                return UnityBridgeSkillResponse.Error(400, "Missing 'properties' field");
            }

            try
            {
                // Parse the properties as a flat JSON object
                var wrapper = JsonUtility.FromJson<PropertiesWrapper>($"{{\"props\":{propsJson}}}");
                var flat = FlattenJson(propsJson);
                var changed = 0;

                foreach (var kv in flat)
                {
                    var prop = so.FindProperty(kv.Key);
                    if (prop == null) continue;
                    if (SetPropertyValue(prop, kv.Value))
                        changed++;
                }

                so.ApplyModifiedProperties();
                so.Dispose();

                return UnityBridgeSkillResponse.Success(
                    $"{{\"set\":true,\"target\":\"{Esc(targetName)}\",\"component\":\"{Esc(componentType)}\",\"changed\":{changed}}}");
            }
            catch (Exception ex)
            {
                so.Dispose();
                return UnityBridgeSkillResponse.Error(400, $"Failed to parse properties: {ex.Message}");
            }
        }

        private static SerializedObject GetSerializedObject(string targetName, string componentType, out UnityBridgeSkillResponse error)
        {
            error = null;
            var go = FindGameObject(targetName);
            if (go == null)
            {
                error = UnityBridgeSkillResponse.Error(404, $"GameObject not found: {targetName}");
                return null;
            }

            Component comp = go.transform;
            if (!string.IsNullOrWhiteSpace(componentType) && !componentType.Equals("GameObject", StringComparison.OrdinalIgnoreCase)
                && !componentType.Equals("Transform", StringComparison.OrdinalIgnoreCase))
            {
                comp = ResolveComponent(go, componentType);
                if (comp == null)
                {
                    error = UnityBridgeSkillResponse.Error(404, $"Component '{componentType}' not found on '{targetName}'");
                    return null;
                }
            }

            return new SerializedObject(comp);
        }

        private static Component ResolveComponent(GameObject go, string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)
                || typeName.Equals("GameObject", StringComparison.OrdinalIgnoreCase))
                return go.transform;

            if (typeName.Equals("Transform", StringComparison.OrdinalIgnoreCase))
                return go.transform;

            return go.GetComponents<Component>()
                .FirstOrDefault(c => c.GetType().Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)
                    || (c.GetType().FullName != null && c.GetType().FullName.Equals(typeName, StringComparison.OrdinalIgnoreCase)));
        }

        private static string SerializePropertyValue(SerializedProperty prop)
        {
            var valueStr = GetValueString(prop);
            return $"{{\"name\":\"{Esc(prop.name)}\",\"type\":\"{prop.propertyType}\",\"value\":{valueStr}}}";
        }

        private static string GetValueString(SerializedProperty prop)
        {
            try
            {
                return prop.propertyType switch
                {
                    SerializedPropertyType.Integer => prop.intValue.ToString(),
                    SerializedPropertyType.Float => prop.floatValue.ToString("F4"),
                    SerializedPropertyType.Boolean => prop.boolValue ? "true" : "false",
                    SerializedPropertyType.String => $"\"{Esc(prop.stringValue)}\"",
                    SerializedPropertyType.Vector2 => $"{{\"x\":{prop.vector2Value.x:F3},\"y\":{prop.vector2Value.y:F3}}}",
                    SerializedPropertyType.Vector3 => $"{{\"x\":{prop.vector3Value.x:F3},\"y\":{prop.vector3Value.y:F3},\"z\":{prop.vector3Value.z:F3}}}",
                    SerializedPropertyType.Vector4 => $"{{\"x\":{prop.vector4Value.x:F3},\"y\":{prop.vector4Value.y:F3},\"z\":{prop.vector4Value.z:F3},\"w\":{prop.vector4Value.w:F3}}}",
                    SerializedPropertyType.Quaternion => $"{{\"x\":{prop.quaternionValue.x:F3},\"y\":{prop.quaternionValue.y:F3},\"z\":{prop.quaternionValue.z:F3},\"w\":{prop.quaternionValue.w:F3}}}",
                    SerializedPropertyType.Color => $"{{\"r\":{prop.colorValue.r:F3},\"g\":{prop.colorValue.g:F3},\"b\":{prop.colorValue.b:F3},\"a\":{prop.colorValue.a:F3}}}",
                    SerializedPropertyType.Enum => $"\"{Esc(prop.enumNames.Length > prop.enumValueIndex && prop.enumValueIndex >= 0 ? prop.enumNames[prop.enumValueIndex] : "Unknown")}\"",
                    SerializedPropertyType.ObjectReference => prop.objectReferenceValue != null
                        ? $"\"{Esc(prop.objectReferenceValue.name)}\""
                        : "null",
                    SerializedPropertyType.ArraySize => prop.intValue.ToString(),
                    _ => $"\"{Esc(prop.propertyType.ToString())}\""
                };
            }
            catch
            {
                return "\"(error reading property)\"";
            }
        }

        private static bool SetPropertyValue(SerializedProperty prop, string value)
        {
            try
            {
                switch (prop.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        if (int.TryParse(value, out var iv)) { prop.intValue = iv; return true; }
                        break;
                    case SerializedPropertyType.Float:
                        if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var fv)) { prop.floatValue = fv; return true; }
                        break;
                    case SerializedPropertyType.Boolean:
                        prop.boolValue = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
                        return true;
                    case SerializedPropertyType.String:
                        prop.stringValue = value;
                        return true;
                    case SerializedPropertyType.Vector2:
                        if (ParseVec2(value, out var v2)) { prop.vector2Value = v2; return true; }
                        break;
                    case SerializedPropertyType.Vector3:
                        if (ParseVec3(value, out var v3)) { prop.vector3Value = v3; return true; }
                        break;
                    case SerializedPropertyType.Color:
                        if (ParseColor(value, out var c)) { prop.colorValue = c; return true; }
                        break;
                    case SerializedPropertyType.Enum:
                        var idx = Array.IndexOf(prop.enumNames, value);
                        if (idx >= 0) { prop.enumValueIndex = idx; return true; }
                        if (int.TryParse(value, out var ev) && ev >= 0 && ev < prop.enumNames.Length)
                        { prop.enumValueIndex = ev; return true; }
                        break;
                    case SerializedPropertyType.ObjectReference:
                        if (value == "null" || value == "")
                            prop.objectReferenceValue = null;
                        else
                            prop.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>(value)
                                ?? FindGameObject(value);
                        return true;
                }
            }
            catch { }
            return false;
        }

        private static bool ParseVec2(string val, out Vector2 v)
        {
            v = Vector2.zero;
            if (string.IsNullOrWhiteSpace(val)) return false;
            // Accept "x,y" or {"x":...,"y":...}
            if (val.TrimStart().StartsWith("{"))
            {
                var o = JsonUtility.FromJson<Vector2Wrapper>($"{{\"v\":{val}}}");
                if (o?.v != null) { v = o.v; return true; }
            }
            var parts = val.Split(',');
            if (parts.Length == 2 && float.TryParse(parts[0], out var x) && float.TryParse(parts[1], out var y))
            { v = new Vector2(x, y); return true; }
            return false;
        }

        private static bool ParseVec3(string val, out Vector3 v)
        {
            v = Vector3.zero;
            if (string.IsNullOrWhiteSpace(val)) return false;
            if (val.TrimStart().StartsWith("{"))
            {
                var o = JsonUtility.FromJson<Vector3Wrapper>($"{{\"v\":{val}}}");
                if (o?.v != null) { v = o.v; return true; }
            }
            var parts = val.Split(',');
            if (parts.Length == 3 && float.TryParse(parts[0], out var x) && float.TryParse(parts[1], out var y)
                && float.TryParse(parts[2], out var z))
            { v = new Vector3(x, y, z); return true; }
            return false;
        }

        private static bool ParseColor(string val, out Color c)
        {
            c = Color.white;
            if (string.IsNullOrWhiteSpace(val)) return false;
            if (val.TrimStart().StartsWith("{"))
            {
                var o = JsonUtility.FromJson<ColorWrapper>($"{{\"c\":{val}}}");
                if (o?.c != null) { c = o.c; return true; }
                return false;
            }
            var parts = val.Split(',');
            if (parts.Length >= 3 && float.TryParse(parts[0], out var r) && float.TryParse(parts[1], out var g)
                && float.TryParse(parts[2], out var b))
            { c = new Color(r, g, b, parts.Length >= 4 && float.TryParse(parts[3], out var a) ? a : 1f); return true; }
            return false;
        }

        private static Dictionary<string, string> FlattenJson(string json)
        {
            // Parse a simple flat JSON object like {"a": 1, "b": "hello", "c": {"x":1,"y":2}}
            var result = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(json)) return result;

            var trimmed = json.Trim();
            if (!trimmed.StartsWith("{") || !trimmed.EndsWith("}")) return result;

            // Simple parser for flat keys and nested objects
            var inner = trimmed.Substring(1, trimmed.Length - 2);
            var depth = 0;
            var currentKey = "";
            var currentValue = "";
            var inKey = true;
            var inString = false;

            for (int i = 0; i < inner.Length; i++)
            {
                var ch = inner[i];
                if (ch == '"' && (i == 0 || inner[i - 1] != '\\'))
                {
                    inString = !inString;
                    if (!inKey) currentValue += ch;
                    continue;
                }

                if (inString)
                {
                    if (inKey) currentKey += ch;
                    else currentValue += ch;
                    continue;
                }

                if (ch == '{' || ch == '[') { depth++; currentValue += ch; continue; }
                if (ch == '}' || ch == ']') { depth--; currentValue += ch; continue; }

                if (depth == 0 && ch == ':' && inKey)
                {
                    inKey = false;
                    continue;
                }

                if (depth == 0 && ch == ',')
                {
                    if (!string.IsNullOrWhiteSpace(currentKey))
                    {
                        var k = currentKey.Trim().Trim('"');
                        var v = currentValue.Trim();
                        result[k] = v;
                    }
                    currentKey = "";
                    currentValue = "";
                    inKey = true;
                    continue;
                }

                if (inKey) currentKey += ch;
                else currentValue += ch;
            }

            // Last entry
            if (!string.IsNullOrWhiteSpace(currentKey))
            {
                var k = currentKey.Trim().Trim('"');
                var v = currentValue.Trim();
                result[k] = v;
            }

            return result;
        }

        private static GameObject FindGameObject(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .FirstOrDefault(g => string.Equals(g.name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static List<string> ParseStringArray(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return new List<string>();
            if (val.TrimStart().StartsWith("["))
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<ArrayWrapper>($"{{\"items\":{val}}}");
                    return wrapper?.items?.ToList() ?? new List<string>();
                }
                catch { }
            }
            return val.Split(',').Select(s => s.Trim().Trim('"').Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(query)) return result;
            query = query.TrimStart('?');
            foreach (var pair in query.Split('&'))
            {
                var parts = pair.Split('=');
                if (parts.Length == 2)
                    result[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
            }
            return result;
        }

        private static Dictionary<string, string> ParseBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                var obj = JsonUtility.FromJson<PropBody>(body);
                if (obj == null) return null;
                var d = new Dictionary<string, string>();
                if (obj.target != null) d["target"] = obj.target;
                if (obj.component != null) d["component"] = obj.component;
                if (obj.properties != null) d["properties"] = obj.properties;
                return d;
            }
            catch { return null; }
        }

        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

        [Serializable] private class PropBody { public string target; public string component; public string properties; }
        [Serializable] private class ArrayWrapper { public string[] items; }
        [Serializable] private class PropertiesWrapper { public string props; }
        [Serializable] private class Vector2Wrapper { public Vector2 v; }
        [Serializable] private class Vector3Wrapper { public Vector3 v; }
        [Serializable] private class ColorWrapper { public Color c; }
    }
}
