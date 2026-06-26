using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityMaterialSkill : IUnityBridgeSkill
    {
        public string Name => "unity-material";

        public string Description =>
            "Read and write Material properties: shader, color, texture, float, vector, keywords. Create new materials. " +
            "Access exposed shader parameters on any material regardless of shader type.";

        public string Trigger =>
            "Use when the user wants to modify material colors, textures, shader properties, toggle keywords, " +
            "create new materials, or inspect what shader and properties a material has.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("GET", "/material?path=Assets/Materials/m.mat",
                "Returns all exposed shader properties and their values for a material asset."),
            new UnityBridgeSkillEndpoint("GET", "/material?target=Cube",
                "Returns material info for a GameObject's renderer (first material)."),
            new UnityBridgeSkillEndpoint("POST", "/material/set",
                "Set material properties. Supports colors, textures (by asset path), floats, vectors, keywords.",
                "{\"path\":\"Assets/Materials/m.mat\",\"properties\":{\"_Color\":{\"r\":1,\"g\":0,\"b\":0,\"a\":1},\"_Metallic\":0.8}}"),
            new UnityBridgeSkillEndpoint("POST", "/material/create",
                "Creates a new material with the given shader.",
                "{\"path\":\"Assets/Materials/New.mat\",\"shader\":\"Standard\"}"),
            new UnityBridgeSkillEndpoint("GET", "/material/shaders",
                "Lists all available shader names in the project.")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js material --path Assets/Materials/Red.mat`",
            "`node scripts/unity-bridge.js material-set --path Assets/Materials/Red.mat --props '{\"_Color\":{\"r\":1,\"g\":0,\"b\":0}}'`",
            "`node scripts/unity-bridge.js material-create --path Assets/Materials/New.mat --shader Standard`",
            "`node scripts/unity-bridge.js material-shaders`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Material properties are shader-specific. Use the GET endpoint to discover available names.",
            "Textures must be specified by asset path (e.g. 'Assets/Textures/MyTex.png').",
            "Color values can be sent as comma-separated 'r,g,b,a' or as a JSON object with r,g,b,a fields."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return (request.Method == "GET" && path == "material")
                || (request.Method == "POST" && path == "material/set")
                || (request.Method == "POST" && path == "material/create")
                || (request.Method == "GET" && path == "material/shaders");
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return path switch
            {
                "material" => GetMaterial(request),
                "material/set" => SetMaterial(request),
                "material/create" => CreateMaterial(request),
                "material/shaders" => ListShaders(),
                _ => UnityBridgeSkillResponse.Error(404, $"Unknown endpoint: /{path}")
            };
        }

        private static UnityBridgeSkillResponse GetMaterial(UnityBridgeSkillRequest request)
        {
            var query = ParseQuery(request.QueryString);
            var assetPath = query.GetValueOrDefault("path", "");
            var targetName = query.GetValueOrDefault("target", "");

            Material mat = null;
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (mat == null)
                    return UnityBridgeSkillResponse.Error(404, $"Material not found: {assetPath}");
            }
            else if (!string.IsNullOrWhiteSpace(targetName))
            {
                var go = FindGameObject(targetName);
                if (go == null)
                    return UnityBridgeSkillResponse.Error(404, $"GameObject not found: {targetName}");

                var renderer = go.GetComponent<Renderer>();
                if (renderer == null || renderer.sharedMaterial == null)
                    return UnityBridgeSkillResponse.Error(404, $"No material found on GameObject: {targetName}");

                mat = renderer.sharedMaterial;
            }
            else
            {
                return UnityBridgeSkillResponse.Error(400, "Provide 'path' for a material asset or 'target' for a GameObject");
            }

            return SerializeMaterial(mat);
        }

        private static UnityBridgeSkillResponse SerializeMaterial(Material mat)
        {
            var shader = mat.shader;
            var propCount = ShaderUtil.GetPropertyCount(shader);
            var props = new List<string>();

            for (int i = 0; i < propCount; i++)
            {
                var propName = ShaderUtil.GetPropertyName(shader, i);
                var propType = ShaderUtil.GetPropertyType(shader, i);
                var valueStr = SerializeMatProp(mat, propName, propType);
                props.Add($"\"{Esc(propName)}\":{valueStr}");
            }

            var keywords = mat.shaderKeywords?.Select(k => $"\"{Esc(k)}\"").ToArray() ?? Array.Empty<string>();

            var assetPath = AssetDatabase.GetAssetPath(mat);
            var scenePath = string.IsNullOrEmpty(assetPath) ? "(scene)" : assetPath;
            var propsJoined = string.Join(",", props);
            var kwJoined = string.Join(",", keywords);
            return UnityBridgeSkillResponse.Success(
                "{\"name\":\"" + Esc(mat.name) + "\",\"path\":\"" + Esc(scenePath) + "\",\"shader\":\"" + Esc(shader.name) + "\",\"properties\":{" + propsJoined + "},\"keywords\":[" + kwJoined + "]}");
        }

        private static string SerializeMatProp(Material mat, string propName, ShaderUtil.ShaderPropertyType propType)
        {
            try
            {
                return propType switch
                {
                    ShaderUtil.ShaderPropertyType.Color =>
                        $"{{\"r\":{mat.GetColor(propName).r:F3},\"g\":{mat.GetColor(propName).g:F3},\"b\":{mat.GetColor(propName).b:F3},\"a\":{mat.GetColor(propName).a:F3}}}",
                    ShaderUtil.ShaderPropertyType.Vector =>
                        $"{{\"x\":{mat.GetVector(propName).x:F3},\"y\":{mat.GetVector(propName).y:F3},\"z\":{mat.GetVector(propName).z:F3},\"w\":{mat.GetVector(propName).w:F3}}}",
                    ShaderUtil.ShaderPropertyType.Float or ShaderUtil.ShaderPropertyType.Range =>
                        mat.GetFloat(propName).ToString("F4"),
                    ShaderUtil.ShaderPropertyType.TexEnv =>
                        mat.GetTexture(propName) != null
                            ? $"\"{Esc(mat.GetTexture(propName).name)}\""
                            : "null",
                    _ => $"\"{propType}\""
                };
            }
            catch { return "\"(error)\""; }
        }

        private static UnityBridgeSkillResponse SetMaterial(UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var assetPath = body?.GetValueOrDefault("path", "");
            var targetName = body?.GetValueOrDefault("target", "");

            Material mat = null;
            if (!string.IsNullOrWhiteSpace(assetPath))
                mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            else if (!string.IsNullOrWhiteSpace(targetName))
            {
                var go = FindGameObject(targetName);
                if (go != null) { var r = go.GetComponent<Renderer>(); if (r != null) mat = r.sharedMaterial; }
            }

            if (mat == null)
                return UnityBridgeSkillResponse.Error(404, "Material not found");

            var propsJson = body?.GetValueOrDefault("properties", "");
            if (string.IsNullOrWhiteSpace(propsJson))
                return UnityBridgeSkillResponse.Error(400, "Missing 'properties' field");

            try
            {
                var flat = FlattenJson(propsJson);
                var changed = 0;
                Undo.RecordObject(mat, "Set Material Properties");

                foreach (var kv in flat)
                {
                    if (SetMatProp(mat, kv.Key, kv.Value))
                        changed++;
                }

                EditorUtility.SetDirty(mat);
                return UnityBridgeSkillResponse.Success(
                    $"{{\"set\":true,\"name\":\"{Esc(mat.name)}\",\"changed\":{changed}}}");
            }
            catch (Exception ex)
            {
                return UnityBridgeSkillResponse.Error(400, $"Failed: {ex.Message}");
            }
        }

        private static bool SetMatProp(Material mat, string propName, string value)
        {
            if (!mat.HasProperty(propName)) return false;

            try
            {
                var propType = GetMatPropType(mat, propName);
                switch (propType)
                {
                    case ShaderUtil.ShaderPropertyType.Color:
                        if (ParseColorFromValue(value, out var c)) { mat.SetColor(propName, c); return true; }
                        break;
                    case ShaderUtil.ShaderPropertyType.Vector:
                        if (ParseVec4(value, out var v4)) { mat.SetVector(propName, v4); return true; }
                        break;
                    case ShaderUtil.ShaderPropertyType.Float:
                    case ShaderUtil.ShaderPropertyType.Range:
                        if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var f))
                        { mat.SetFloat(propName, f); return true; }
                        break;
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        if (value.Equals("null", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(value))
                            mat.SetTexture(propName, null);
                        else
                        {
                            var tex = AssetDatabase.LoadAssetAtPath<Texture>(value);
                            if (tex != null) mat.SetTexture(propName, tex);
                            else return false;
                        }
                        return true;
                }
            }
            catch { }
            return false;
        }

        private static ShaderUtil.ShaderPropertyType GetMatPropType(Material mat, string propName)
        {
            var shader = mat.shader;
            var count = ShaderUtil.GetPropertyCount(shader);
            for (int i = 0; i < count; i++)
            {
                if (ShaderUtil.GetPropertyName(shader, i) == propName)
                    return ShaderUtil.GetPropertyType(shader, i);
            }
            return ShaderUtil.ShaderPropertyType.Float; // default fallback
        }

        private static UnityBridgeSkillResponse CreateMaterial(UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var path = body?.GetValueOrDefault("path", "");
            var shaderName = body?.GetValueOrDefault("shader", "Standard");

            if (string.IsNullOrWhiteSpace(path))
                return UnityBridgeSkillResponse.Error(400, "Missing 'path' field");

            var shader = Shader.Find(shaderName);
            if (shader == null)
                return UnityBridgeSkillResponse.Error(404, $"Shader not found: {shaderName}");

            if (!path.EndsWith(".mat")) path += ".mat";

            try
            {
                var mat = new Material(shader);
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                AssetDatabase.CreateAsset(mat, path);
                AssetDatabase.Refresh();
                return UnityBridgeSkillResponse.Success(
                    $"{{\"created\":true,\"path\":\"{Esc(path)}\",\"shader\":\"{Esc(shaderName)}\"}}");
            }
            catch (Exception ex)
            {
                return UnityBridgeSkillResponse.Error(500, $"Failed to create material: {ex.Message}");
            }
        }

        private static UnityBridgeSkillResponse ListShaders()
        {
            // Find all shader assets and also built-in shaders
            var guids = AssetDatabase.FindAssets("t:Shader");
            var shaders = guids.Take(100).Select(g =>
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var s = AssetDatabase.LoadAssetAtPath<Shader>(p);
                return s != null ? $"\"{Esc(s.name)}\"" : null;
            }).Where(s => s != null).Distinct().ToArray();

            // Also include common built-in shader names
            var builtins = new[] { "Standard", "Standard (Specular setup)", "Unlit/Color", "Unlit/Texture",
                "Unlit/Transparent", "Sprites/Default", "UI/Default", "Legacy Shaders/Diffuse",
                "Mobile/Diffuse", "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit" };

            var all = shaders.Concat(builtins.Select(s => $"\"{Esc(s)}\"")).Distinct().ToArray();
            return UnityBridgeSkillResponse.Success($"{{\"shaders\":[{string.Join(",", all)}],\"count\":{all.Length}}}");
        }

        private static bool ParseColorFromValue(string val, out Color c)
        {
            c = Color.white;
            if (string.IsNullOrWhiteSpace(val)) return false;
            if (val.TrimStart().StartsWith("{"))
            {
                var o = JsonUtility.FromJson<ColorWrap>($"{{\"c\":{val}}}");
                if (o?.c != null) { c = o.c; return true; }
                return false;
            }
            var parts = val.Split(',');
            if (parts.Length >= 3 && float.TryParse(parts[0], out var r) && float.TryParse(parts[1], out var g)
                && float.TryParse(parts[2], out var b))
            { c = new Color(r, g, b, parts.Length >= 4 && float.TryParse(parts[3], out var a) ? a : 1f); return true; }
            return false;
        }

        private static bool ParseVec4(string val, out Vector4 v)
        {
            v = Vector4.zero;
            if (string.IsNullOrWhiteSpace(val)) return false;
            if (val.TrimStart().StartsWith("{"))
            {
                var o = JsonUtility.FromJson<Vec4Wrap>($"{{\"v\":{val}}}");
                if (o?.v != null) { v = o.v; return true; }
                return false;
            }
            var parts = val.Split(',');
            if (parts.Length >= 4 && float.TryParse(parts[0], out var x) && float.TryParse(parts[1], out var y)
                && float.TryParse(parts[2], out var z) && float.TryParse(parts[3], out var w))
            { v = new Vector4(x, y, z, w); return true; }
            return false;
        }

        private static GameObject FindGameObject(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .FirstOrDefault(g => string.Equals(g.name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static Dictionary<string, string> FlattenJson(string json)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(json)) return result;
            var trimmed = json.Trim();
            if (!trimmed.StartsWith("{") || !trimmed.EndsWith("}")) return result;

            var inner = trimmed.Substring(1, trimmed.Length - 2);
            var depth = 0;
            var currentKey = "";
            var currentValue = "";
            var inKey = true;
            var inString = false;

            for (int i = 0; i < inner.Length; i++)
            {
                var ch = inner[i];
                if (ch == '"' && (i == 0 || inner[i - 1] != '\\')) { inString = !inString; if (!inKey) currentValue += ch; continue; }
                if (inString) { if (inKey) currentKey += ch; else currentValue += ch; continue; }
                if (ch == '{' || ch == '[') { depth++; currentValue += ch; continue; }
                if (ch == '}' || ch == ']') { depth--; currentValue += ch; continue; }
                if (depth == 0 && ch == ':' && inKey) { inKey = false; continue; }
                if (depth == 0 && ch == ',')
                {
                    if (!string.IsNullOrWhiteSpace(currentKey))
                    {
                        var k = currentKey.Trim().Trim('"');
                        result[k] = currentValue.Trim();
                    }
                    currentKey = "";
                    currentValue = "";
                    inKey = true;
                    continue;
                }
                if (inKey) currentKey += ch;
                else currentValue += ch;
            }

            if (!string.IsNullOrWhiteSpace(currentKey))
            {
                var k = currentKey.Trim().Trim('"');
                result[k] = currentValue.Trim();
            }
            return result;
        }

        private static Dictionary<string, string> ParseQuery(string q)
        {
            var r = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(q)) return r;
            q = q.TrimStart('?');
            foreach (var p in q.Split('&')) { var kv = p.Split('='); if (kv.Length == 2) r[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]); }
            return r;
        }

        private static Dictionary<string, string> ParseBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                var obj = JsonUtility.FromJson<MatBody>(body);
                if (obj == null) return null;
                var d = new Dictionary<string, string>();
                if (obj.path != null) d["path"] = obj.path;
                if (obj.target != null) d["target"] = obj.target;
                if (obj.shader != null) d["shader"] = obj.shader;
                if (obj.properties != null) d["properties"] = obj.properties;
                return d;
            }
            catch { return null; }
        }

        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        [Serializable] private class MatBody { public string path; public string target; public string shader; public string properties; }
        [Serializable] private class ColorWrap { public Color c; }
        [Serializable] private class Vec4Wrap { public Vector4 v; }
    }
}
