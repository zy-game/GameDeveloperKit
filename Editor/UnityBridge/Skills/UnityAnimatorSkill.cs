using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityAnimatorSkill : IUnityBridgeSkill
    {
        public string Name => "unity-animator";

        public string Description =>
            "Create and edit AnimatorControllers: add layers, states, transitions, parameters (float, int, bool, trigger), blend trees.";

        public string Trigger =>
            "Use when the user wants to create AnimatorControllers, add animation states, set up transitions, configure parameters, or build state machines.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("POST", "/animator/create", "Creates a new AnimatorController asset.",
                "{\"path\":\"Assets/MyController.controller\"}"),
            new UnityBridgeSkillEndpoint("POST", "/animator/parameter/add", "Adds a parameter to the controller.",
                "{\"path\":\"Assets/MyController.controller\",\"name\":\"Speed\",\"type\":\"Float\"}"),
            new UnityBridgeSkillEndpoint("POST", "/animator/state/add", "Adds a state with an animation clip to the base layer.",
                "{\"path\":\"Assets/MyController.controller\",\"stateName\":\"Idle\",\"clip\":\"Assets/Idle.anim\"}"),
            new UnityBridgeSkillEndpoint("POST", "/animator/transition/add", "Adds a transition between two states.",
                "{\"path\":\"Assets/MyController.controller\",\"from\":\"Idle\",\"to\":\"Walk\",\"condition\":\"Speed,Greater,0.1\"}"),
            new UnityBridgeSkillEndpoint("GET", "/animator?path=Assets/MyController.controller", "Lists states, parameters, and layers in the controller."),
            new UnityBridgeSkillEndpoint("POST", "/animator/assign", "Assigns an AnimatorController to a GameObject.",
                "{\"target\":\"Player\",\"controller\":\"Assets/MyController.controller\"}")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js animator-create --path Assets/Player.controller`",
            "`node scripts/unity-bridge.js animator-param --path Assets/Player.controller --name Speed --type Float`",
            "`node scripts/unity-bridge.js animator-state --path Assets/Player.controller --name Walk --clip Assets/Walk.anim`",
            "`node scripts/unity-bridge.js animator-transition --path Assets/Player.controller --from Idle --to Walk --condition Speed,Greater,0.1`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Parameter types: Float, Int, Bool, Trigger.",
            "Transition condition format: paramName,operator,value. Operators: Greater, Less, Equals, NotEqual.",
            "Use the assign endpoint to attach a controller to a GameObject with an Animator component."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            var p = request.Path.Trim('/');
            return (request.Method == "GET" && p == "animator")
                || (request.Method == "POST" && (p == "animator/create" || p == "animator/parameter/add" || p == "animator/state/add" || p == "animator/transition/add" || p == "animator/assign"));
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            var p = request.Path.Trim('/');
            return p switch
            {
                "animator" => GetAnimator(request),
                "animator/create" => CreateController(request),
                "animator/parameter/add" => AddParameter(request),
                "animator/state/add" => AddState(request),
                "animator/transition/add" => AddTransition(request),
                "animator/assign" => Assign(request),
                _ => UnityBridgeSkillResponse.Error(404, $"Unknown: /{p}")
            };
        }

        private static UnityBridgeSkillResponse CreateController(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var path = b?.GetValueOrDefault("path", "Assets/NewController.controller");
            if (!path.EndsWith(".controller")) path += ".controller";
            EnsureDir(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AssetDatabase.Refresh();
            return UnityBridgeSkillResponse.Success($"{{\"created\":true,\"path\":\"{Esc(path)}\"}}");
        }

        private static UnityBridgeSkillResponse AddParameter(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var path = b?.GetValueOrDefault("path", "");
            var name = b?.GetValueOrDefault("name", "NewParam");
            var type = b?.GetValueOrDefault("type", "Float");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) return UnityBridgeSkillResponse.Error(404, $"Controller not found: {path}");

            switch (type.ToLowerInvariant())
            {
                case "float": controller.AddParameter(name, AnimatorControllerParameterType.Float); break;
                case "int": controller.AddParameter(name, AnimatorControllerParameterType.Int); break;
                case "bool": controller.AddParameter(name, AnimatorControllerParameterType.Bool); break;
                case "trigger": controller.AddParameter(name, AnimatorControllerParameterType.Trigger); break;
                default: return UnityBridgeSkillResponse.Error(400, $"Unknown type: {type}. Use Float/Int/Bool/Trigger");
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return UnityBridgeSkillResponse.Success($"{{\"added\":true,\"name\":\"{Esc(name)}\",\"type\":\"{Esc(type)}\"}}");
        }

        private static UnityBridgeSkillResponse AddState(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var path = b?.GetValueOrDefault("path", "");
            var stateName = b?.GetValueOrDefault("stateName", "NewState");
            var clipPath = b?.GetValueOrDefault("clip", "");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) return UnityBridgeSkillResponse.Error(404, $"Controller not found: {path}");

            var sm = controller.layers[0].stateMachine;
            var clip = string.IsNullOrWhiteSpace(clipPath) ? null : AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            var state = sm.AddState(stateName);
            if (clip != null) state.motion = clip;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return UnityBridgeSkillResponse.Success($"{{\"added\":true,\"state\":\"{Esc(stateName)}\",\"clip\":\"{Esc(clipPath)}\"}}");
        }

        private static UnityBridgeSkillResponse AddTransition(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var path = b?.GetValueOrDefault("path", "");
            var from = b?.GetValueOrDefault("from", "");
            var to = b?.GetValueOrDefault("to", "");
            var condition = b?.GetValueOrDefault("condition", "");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) return UnityBridgeSkillResponse.Error(404, $"Controller not found: {path}");

            var sm = controller.layers[0].stateMachine;
            var fromState = sm.states.FirstOrDefault(s => s.state.name == from).state;
            var toState = sm.states.FirstOrDefault(s => s.state.name == to).state;
            if (fromState == null) return UnityBridgeSkillResponse.Error(404, $"Source state not found: {from}");
            if (toState == null) return UnityBridgeSkillResponse.Error(404, $"Target state not found: {to}");

            var transition = fromState.AddTransition(toState);
            if (!string.IsNullOrWhiteSpace(condition))
            {
                var parts = condition.Split(',');
                if (parts.Length == 3)
                {
                    var mode = parts[1].Trim().ToLowerInvariant() switch
                    {
                        "greater" => AnimatorConditionMode.Greater,
                        "less" => AnimatorConditionMode.Less,
                        "equals" => AnimatorConditionMode.Equals,
                        "notequal" => AnimatorConditionMode.NotEqual,
                        _ => AnimatorConditionMode.Greater
                    };
                    if (float.TryParse(parts[2], out var cv))
                        transition.AddCondition(mode, cv, parts[0].Trim());
                }
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return UnityBridgeSkillResponse.Success($"{{\"transitionCreated\":true,\"from\":\"{Esc(from)}\",\"to\":\"{Esc(to)}\"}}");
        }

        private static UnityBridgeSkillResponse GetAnimator(UnityBridgeSkillRequest request)
        {
            var q = ParseQuery(request.QueryString);
            var path = q.GetValueOrDefault("path", "");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) return UnityBridgeSkillResponse.Error(404, $"Controller not found: {path}");

            var parts = new List<string>();
            foreach (var l in controller.layers)
            {
                var states = l.stateMachine.states.Select(s => $"{{\"name\":\"{Esc(s.state.name)}\",\"motion\":\"{Esc(s.state.motion?.name ?? "")}\"}}").ToArray();
                parts.Add($"{{\"layer\":\"{Esc(l.name)}\",\"states\":[{string.Join(",", states)}]}}");
            }
            var pars = controller.parameters.Select(p => $"{{\"name\":\"{Esc(p.name)}\",\"type\":\"{p.type}\"}}").ToArray();

            return UnityBridgeSkillResponse.Success($"{{\"path\":\"{Esc(path)}\",\"layers\":[{string.Join(",", parts)}],\"parameters\":[{string.Join(",", pars)}]}}");
        }

        private static UnityBridgeSkillResponse Assign(UnityBridgeSkillRequest request)
        {
            var b = ParseBody(request.Body);
            var target = b?.GetValueOrDefault("target", "");
            var controllerPath = b?.GetValueOrDefault("controller", "");
            var go = FindGo(target);
            if (go == null) return UnityBridgeSkillResponse.Error(404, $"GameObject not found: {target}");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) return UnityBridgeSkillResponse.Error(404, $"Controller not found: {controllerPath}");

            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            return UnityBridgeSkillResponse.Success($"{{\"assigned\":true,\"target\":\"{Esc(target)}\",\"controller\":\"{Esc(controllerPath)}\"}}");
        }

        private static void EnsureDir(string p) { var d = System.IO.Path.GetDirectoryName(p); if (!string.IsNullOrWhiteSpace(d) && !System.IO.Directory.Exists(d)) System.IO.Directory.CreateDirectory(d); }
        private static GameObject FindGo(string n) => string.IsNullOrWhiteSpace(n) ? null : Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).FirstOrDefault(g => string.Equals(g.name, n, StringComparison.OrdinalIgnoreCase));
        private static Dictionary<string, string> ParseQuery(string q) { var r = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); if (!string.IsNullOrWhiteSpace(q)) foreach (var p in q.TrimStart('?').Split('&')) { var kv = p.Split('='); if (kv.Length == 2) r[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]); } return r; }
        private static Dictionary<string, string> ParseBody(string body) { if (string.IsNullOrWhiteSpace(body)) return null; try { var o = JsonUtility.FromJson<AnimB>(body); var d = new Dictionary<string, string>(); if (o.path != null) d["path"] = o.path; if (o.name != null) d["name"] = o.name; if (o.type != null) d["type"] = o.type; if (o.stateName != null) d["stateName"] = o.stateName; if (o.clip != null) d["clip"] = o.clip; if (o.from != null) d["from"] = o.from; if (o.to != null) d["to"] = o.to; if (o.condition != null) d["condition"] = o.condition; if (o.target != null) d["target"] = o.target; if (o.controller != null) d["controller"] = o.controller; return d; } catch { return null; } }
        private static string Esc(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        [Serializable] private class AnimB { public string path, name, type, stateName, clip, from, to, condition, target, controller; }
    }
}
