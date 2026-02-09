using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.Config
{
    /// <summary>
    /// 值转换器注册中心
    /// </summary>
    public static class ValueTransportRegistry
    {
        private static Dictionary<string, IValueTransport> _transports;
        private static bool _initialized;
        
        /// <summary>
        /// 初始化并注册所有Transport
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            if (_initialized) return;
            
            _transports = new Dictionary<string, IValueTransport>(StringComparer.OrdinalIgnoreCase);
            
            // 注册基础类型
            RegisterBasicTransports();
            
            // 注册Unity类型
            RegisterUnityTransports();
            
            // 注册复杂类型
            RegisterComplexTransports();
            
            _initialized = true;
            Debug.Log($"ValueTransportRegistry initialized with {_transports.Count} transports");
        }
        
        private static void RegisterBasicTransports()
        {
            Register(new IntValueTransport());
            Register(new FloatValueTransport());
            Register(new BoolValueTransport());
            Register(new StringValueTransport());
        }
        
        private static void RegisterUnityTransports()
        {
            Register(new Vector2ValueTransport());
            Register(new Vector3ValueTransport());
            Register(new Vector4ValueTransport());
            Register(new RectValueTransport());
            Register(new ColorValueTransport());
        }
        
        private static void RegisterComplexTransports()
        {
            Register(new JsonObjectTransport());
        }
        
        /// <summary>
        /// 注册Transport
        /// </summary>
        public static void Register(IValueTransport transport)
        {
            if (transport == null)
            {
                Debug.LogError("Cannot register null transport");
                return;
            }
            
            if (_transports == null)
            {
                Initialize();
            }
            
            _transports[transport.TypeName] = transport;
        }
        
        /// <summary>
        /// 获取Transport
        /// </summary>
        public static IValueTransport GetTransport(string typeName)
        {
            if (_transports == null)
            {
                Initialize();
            }
            
            if (string.IsNullOrEmpty(typeName))
            {
                return _transports["string"];
            }
            
            // 处理数组类型
            if (typeName.EndsWith("[]"))
            {
                var elementType = typeName.Substring(0, typeName.Length - 2);
                var elementTransport = GetTransport(elementType);
                return new ArrayValueTransport(elementTransport, typeName);
            }
            
            // 查找已注册的Transport（精确匹配）
            if (_transports.TryGetValue(typeName, out var transport))
            {
                return transport;
            }
            
            // 遍历所有Transport，使用CanHandle检查是否支持
            foreach (var kvp in _transports)
            {
                if (kvp.Value.CanHandle(typeName))
                {
                    return kvp.Value;
                }
            }
            
            // 未知类型，尝试作为JSON对象处理
            return new JsonObjectTransport(typeName);
        }
        
        /// <summary>
        /// 是否支持该类型
        /// </summary>
        public static bool IsSupported(string typeName)
        {
            if (_transports == null)
            {
                Initialize();
            }
            
            if (string.IsNullOrEmpty(typeName)) return true;
            
            // 数组类型：检查元素类型
            if (typeName.EndsWith("[]"))
            {
                var elementType = typeName.Substring(0, typeName.Length - 2);
                return IsSupported(elementType);
            }
            
            return _transports.ContainsKey(typeName);
        }
        
        /// <summary>
        /// 获取所有注册的类型名称
        /// </summary>
        public static IEnumerable<string> GetAllTypeNames()
        {
            if (_transports == null)
            {
                Initialize();
            }
            
            return _transports.Keys;
        }
    }
}
