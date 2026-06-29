using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace GameDeveloperKit.UnityBridge
{
    internal sealed class UnityReflectionSkill : IUnityBridgeSkill
    {
        public string Name => "unity-reflection";

        public string Description =>
            "Call static C# methods or get/set static properties in loaded assemblies.";

        public string Trigger =>
            "Use only when there is no safer endpoint or menu command for the requested automation.";

        public IEnumerable<UnityBridgeSkillEndpoint> Endpoints => new[]
        {
            new UnityBridgeSkillEndpoint("POST", "/eval",
                "Call a static method or get/set a static property.",
                "{\"call\":{\"type\":\"MyTool\",\"method\":\"Run\",\"args\":[]}}")
        };

        public IEnumerable<string> Examples => new[]
        {
            "`node scripts/unity-bridge.js eval --type MyNamespace.MyTool --method Run`"
        };

        public IEnumerable<string> Notes => new[]
        {
            "Prefer dedicated endpoints over reflection.",
            "Only static members are accessible; arguments are simple strings."
        };

        public bool CanExecute(UnityBridgeSkillRequest request)
        {
            return request.Method == "POST" && request.Path.Trim('/') == "eval";
        }

        public UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request)
        {
            var body = request.Body;

            var callReq = Deserialize<EvalCallRequest>(body);
            if (callReq != null && !string.IsNullOrWhiteSpace(callReq.type) && !string.IsNullOrWhiteSpace(callReq.method))
            {
                return HandleCall(callReq);
            }

            var getReq = Deserialize<EvalPropertyRequest>(body);
            if (getReq != null && !string.IsNullOrWhiteSpace(getReq.type) && !string.IsNullOrWhiteSpace(getReq.property))
            {
                return HandleGet(getReq);
            }

            return UnityBridgeSkillResponse.Error(400,
                "Use {call:{type,method,args}}, {get:{type,property}}, or {set:{type,property,value}}");
        }

        private UnityBridgeSkillResponse HandleCall(EvalCallRequest req)
        {
            var type = ResolveType(req.type);
            if (type == null)
                return UnityBridgeSkillResponse.Error(404, $"Type not found: {req.type}");

            var args = req.args ?? Array.Empty<string>();
            var method = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == req.method && m.GetParameters().Length == args.Length);
            if (method == null)
                return UnityBridgeSkillResponse.Error(404, $"Method not found: {req.type}.{req.method}({args.Length} args)");

            try
            {
                var converted = ConvertArgs(method.GetParameters(), args);
                var result = method.Invoke(null, converted);
                return UnityBridgeSkillResponse.Success($"{{\"result\":{Serialize(result)}}}");
            }
            catch (TargetInvocationException ex)
            {
                return UnityBridgeSkillResponse.Error(500,
                    $"Exception in {req.type}.{req.method}: {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                return UnityBridgeSkillResponse.Error(500, $"Failed: {ex.Message}");
            }
        }

        private UnityBridgeSkillResponse HandleGet(EvalPropertyRequest req)
        {
            var type = ResolveType(req.type);
            if (type == null)
                return UnityBridgeSkillResponse.Error(404, $"Type not found: {req.type}");

            var prop = type.GetProperty(req.property, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (prop == null)
                return UnityBridgeSkillResponse.Error(404, $"Property not found: {req.type}.{req.property}");

            try
            {
                var value = prop.GetValue(null);
                return UnityBridgeSkillResponse.Success($"{{\"result\":{Serialize(value)}}}");
            }
            catch (Exception ex)
            {
                return UnityBridgeSkillResponse.Error(500, $"Failed: {ex.Message}");
            }
        }

        private static Type ResolveType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Type.EmptyTypes; }
                })
                .FirstOrDefault(t => t != null && (t.FullName == typeName || t.Name == typeName));
        }

        private static object[] ConvertArgs(ParameterInfo[] parameters, string[] args)
        {
            var result = new object[parameters.Length];
            for (int i = 0; i < parameters.Length && i < args.Length; i++)
            {
                result[i] = ConvertValue(args[i], parameters[i].ParameterType);
            }

            return result;
        }

        private static object ConvertValue(string value, Type targetType)
        {
            if (value == null || value == "null")
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            if (targetType == typeof(string)) return value;
            if (targetType == typeof(int)) return int.TryParse(value, out var i) ? i : 0;
            if (targetType == typeof(float)) return float.TryParse(value, out var f) ? f : 0f;
            if (targetType == typeof(double)) return double.TryParse(value, out var d) ? d : 0.0;
            if (targetType == typeof(bool)) return bool.TryParse(value, out var b) ? b : value == "1";
            if (targetType == typeof(long)) return long.TryParse(value, out var l) ? l : 0L;
            if (targetType.IsEnum)
                return Enum.TryParse(targetType, value, true, out var e) ? e : Activator.CreateInstance(targetType);

            try { return Convert.ChangeType(value, targetType); }
            catch { return null; }
        }

        private static string Serialize(object value)
        {
            if (value == null) return "null";
            if (value is string s) return $"\"{Esc(s)}\"";
            if (value is bool b) return b ? "true" : "false";
            if (value is int or float or double or long or short or byte) return value.ToString();
            if (value is Enum) return $"\"{value}\"";

            try { return JsonUtility.ToJson(value); }
            catch { return $"\"{Esc(value.ToString())}\""; }
        }

        private static T Deserialize<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonUtility.FromJson<T>(json); }
            catch { return null; }
        }

        private static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        [Serializable]
        private class EvalCallRequest
        {
            public string type;
            public string method;
            public string[] args;
        }

        [Serializable]
        private class EvalPropertyRequest
        {
            public string type;
            public string property;
            public string value;
        }
    }
}
