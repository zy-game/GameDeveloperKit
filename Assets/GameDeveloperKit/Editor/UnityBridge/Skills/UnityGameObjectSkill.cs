using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityGameObjectSkill : IUnityBridgeSkill
    {
        public string Name => "unity-gameobject";

        public string Description =>
            "Create, find, modify, and destroy GameObjects. Set transform, add components, change active state.";

        public string Trigger =>
            "Use when the user asks to create or manipulate GameObjects, set transforms, add components, or manage scene objects.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("GET", "/scene/objects", "Lists root GameObjects in the active scene."),
            new UnityBridgeSkillEndpoint("POST", "/gameobject/create", "Creates a GameObject.", "{\"name\":\"MyObject\"}"),
            new UnityBridgeSkillEndpoint("GET", "/gameobject/find?name=xxx", "Finds GameObjects by name (case-insensitive, partial match)."),
            new UnityBridgeSkillEndpoint("POST", "/gameobject/transform/set", "Sets transform of a GameObject.", "{\"name\":\"MyObject\",\"position\":[1,2,3],\"rotation\":[0,45,0],\"scale\":[2,2,2]}"),
            new UnityBridgeSkillEndpoint("POST", "/gameobject/component/add", "Adds a component to a GameObject.", "{\"name\":\"MyObject\",\"componentType\":\"BoxCollider\"}"),
            new UnityBridgeSkillEndpoint("POST", "/gameobject/active", "Sets the active state.", "{\"name\":\"MyObject\",\"active\":false}"),
            new UnityBridgeSkillEndpoint("POST", "/gameobject/destroy", "Destroys a GameObject by name.", "{\"name\":\"MyObject\"}")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js scene-objects`",
            "`node scripts/unity-bridge.js go-create --name MyObject`",
            "`node scripts/unity-bridge.js go-find --name MainCamera`",
            "`node scripts/unity-bridge.js go-transform --name MyObject --position 1,2,3`",
            "`node scripts/unity-bridge.js go-add-component --name MyObject --componentType BoxCollider`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Finding GameObjects matches any substring in the name.",
            "Component type names support short forms (e.g. 'BoxCollider' instead of 'UnityEngine.BoxCollider')."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');
            return (request.Method == "GET" && path == "scene/objects")
                || (request.Method == "POST" && path == "gameobject/create")
                || (request.Method == "GET" && path.StartsWith("gameobject/find"))
                || (request.Method == "POST" && path == "gameobject/transform/set")
                || (request.Method == "POST" && path == "gameobject/component/add")
                || (request.Method == "POST" && path == "gameobject/active")
                || (request.Method == "POST" && path == "gameobject/destroy");
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            var path = request.Path.Trim('/');

            return path switch
            {
                "scene/objects" => ListRootObjects(),
                "gameobject/create" => CreateGameObject(request),
                _ when path.StartsWith("gameobject/find") => FindGameObjects(request),
                "gameobject/transform/set" => SetTransform(request),
                "gameobject/component/add" => AddComponent(request),
                "gameobject/active" => SetActive(request),
                "gameobject/destroy" => DestroyGameObject(request),
                _ => UnityBridgeSkillResponse.Error(404, $"Unknown endpoint: /{path}")
            };
        }

        private static UnityBridgeSkillResponse ListRootObjects()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var parts = roots.Select(go => "{"
                + $"\"name\":\"{Esc(go.name)}\","
                + $"\"active\":{(go.activeSelf ? "true" : "false")},"
                + $"\"components\":[{string.Join(",", go.GetComponents<Component>().Select(c => $"\"{c.GetType().Name}\""))}]"
                + "}").ToArray();

            return UnityBridgeSkillResponse.Success($"{{\"objects\":[{string.Join(",", parts)}],\"count\":{parts.Length}}}");
        }

        private static UnityBridgeSkillResponse CreateGameObject(UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var name = body?.GetValueOrDefault("name", "New GameObject");
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create GameObject");
            return UnityBridgeSkillResponse.Success($"{{\"created\":true,\"name\":\"{Esc(go.name)}\"}}");
        }

        private static UnityBridgeSkillResponse FindGameObjects(UnityBridgeSkillRequest request)
        {
            var nameFilter = ParseQuery(request.QueryString).GetValueOrDefault("name", "");
            var all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            var matches = string.IsNullOrWhiteSpace(nameFilter)
                ? all
                : all.Where(g => g.name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();

            var parts = matches.Take(50).Select(go => "{"
                + $"\"name\":\"{Esc(go.name)}\","
                + $"\"active\":{(go.activeSelf ? "true" : "false")},"
                + $"\"position\":[{go.transform.position.x:F2},{go.transform.position.y:F2},{go.transform.position.z:F2}]"
                + "}").ToArray();

            return UnityBridgeSkillResponse.Success($"{{\"objects\":[{string.Join(",", parts)}],\"count\":{parts.Length}}}");
        }

        private static UnityBridgeSkillResponse SetTransform(UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var name = body?.GetValueOrDefault("name", "");
            var go = FindByName(name);
            if (go == null) return UnityBridgeSkillResponse.Error(404, $"GameObject not found: {name}");

            if (ParseVector3(body, "position", out var pos)) go.transform.position = pos;
            if (ParseVector3(body, "rotation", out var rot)) go.transform.eulerAngles = rot;
            if (ParseVector3(body, "scale", out var scl)) go.transform.localScale = scl;

            var t = go.transform;
            return UnityBridgeSkillResponse.Success(
                $"{{\"set\":true,\"position\":[{t.position.x:F2},{t.position.y:F2},{t.position.z:F2}],\"rotation\":[{t.eulerAngles.x:F2},{t.eulerAngles.y:F2},{t.eulerAngles.z:F2}],\"scale\":[{t.localScale.x:F2},{t.localScale.y:F2},{t.localScale.z:F2}]}}");
        }

        private static UnityBridgeSkillResponse AddComponent(UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var name = body?.GetValueOrDefault("name", "");
            var componentType = body?.GetValueOrDefault("componentType", "");
            var go = FindByName(name);
            if (go == null) return UnityBridgeSkillResponse.Error(404, $"GameObject not found: {name}");

            var type = ResolveComponentType(componentType);
            if (type == null) return UnityBridgeSkillResponse.Error(404, $"Component type not found: {componentType}");

            try
            {
                go.AddComponent(type);
                return UnityBridgeSkillResponse.Success($"{{\"added\":true,\"component\":\"{Esc(type.Name)}\"}}");
            }
            catch (Exception ex)
            {
                return UnityBridgeSkillResponse.Error(500, $"Failed to add component: {ex.Message}");
            }
        }

        private static UnityBridgeSkillResponse SetActive(UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var name = body?.GetValueOrDefault("name", "");
            var activeStr = body?.GetValueOrDefault("active", "true");
            var go = FindByName(name);
            if (go == null) return UnityBridgeSkillResponse.Error(404, $"GameObject not found: {name}");

            var active = !activeStr.Equals("false", StringComparison.OrdinalIgnoreCase) && activeStr != "0";
            go.SetActive(active);
            return UnityBridgeSkillResponse.Success($"{{\"set\":true,\"active\":{(active ? "true" : "false")}}}");
        }

        private static UnityBridgeSkillResponse DestroyGameObject(UnityBridgeSkillRequest request)
        {
            var body = ParseBody(request.Body);
            var name = body?.GetValueOrDefault("name", "");
            var go = FindByName(name);
            if (go == null) return UnityBridgeSkillResponse.Error(404, $"GameObject not found: {name}");

            Undo.DestroyObjectImmediate(go);
            return UnityBridgeSkillResponse.Success($"{{\"destroyed\":true,\"name\":\"{Esc(name)}\"}}");
        }

        private static GameObject FindByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .FirstOrDefault(g => string.Equals(g.name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static Type ResolveComponentType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Type.EmptyTypes; }
                })
                .FirstOrDefault(t => typeof(Component).IsAssignableFrom(t)
                    && (t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)
                        || t.FullName != null && t.FullName.Equals(typeName, StringComparison.OrdinalIgnoreCase)));
        }

        private static bool ParseVector3(Dictionary<string, string> body, string key, out Vector3 result)
        {
            result = Vector3.zero;
            if (body == null || !body.TryGetValue(key, out var val)) return false;
            var parts = val.Split(',');
            if (parts.Length != 3) return false;
            if (!float.TryParse(parts[0], out var x)) return false;
            if (!float.TryParse(parts[1], out var y)) return false;
            if (!float.TryParse(parts[2], out var z)) return false;
            result = new Vector3(x, y, z);
            return true;
        }

        private static Dictionary<string, string> ParseBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                var obj = JsonUtility.FromJson<GoBody>(body);
                if (obj == null) return null;
                var d = new Dictionary<string, string>();
                if (obj.name != null) d["name"] = obj.name;
                if (obj.componentType != null) d["componentType"] = obj.componentType;
                if (obj.active != null) d["active"] = obj.active;
                if (obj.position != null) d["position"] = obj.position;
                if (obj.rotation != null) d["rotation"] = obj.rotation;
                if (obj.scale != null) d["scale"] = obj.scale;
                return d;
            }
            catch { return null; }
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

        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        [Serializable]
        private class GoBody
        {
            public string name;
            public string componentType;
            public string active;
            public string position;
            public string rotation;
            public string scale;
        }
    }
}
