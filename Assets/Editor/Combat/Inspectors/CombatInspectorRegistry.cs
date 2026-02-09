using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace GameDeveloperKit.Editor.Combat
{
    /// <summary>
    /// 战斗系统Inspector注册表
    /// 负责扫描所有程序集，建立数据类型与Inspector类型的映射关系
    /// </summary>
    public static class CombatInspectorRegistry
    {
        private static Dictionary<Type, Type> _inspectorMap;
        private static bool _initialized;

        /// <summary>
        /// 获取指定数据类型对应的Inspector类型
        /// 支持继承链查找：如果没有精确匹配，会查找父类的Inspector
        /// </summary>
        public static Type GetInspectorType(Type dataType)
        {
            EnsureInitialized();

            if (dataType == null) return null;

            // 精确匹配
            if (_inspectorMap.TryGetValue(dataType, out var inspectorType))
            {
                return inspectorType;
            }

            // 继承链查找
            var baseType = dataType.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                if (_inspectorMap.TryGetValue(baseType, out inspectorType))
                {
                    return inspectorType;
                }
                baseType = baseType.BaseType;
            }

            return null;
        }

        /// <summary>
        /// 创建指定数据类型对应的Inspector实例
        /// </summary>
        public static CombatInspectorBase CreateInspector(Type dataType)
        {
            var inspectorType = GetInspectorType(dataType);
            if (inspectorType == null) return null;

            return Activator.CreateInstance(inspectorType) as CombatInspectorBase;
        }

        /// <summary>
        /// 强制重新扫描所有Inspector
        /// 当有新的程序集加载时调用
        /// </summary>
        public static void Refresh()
        {
            _initialized = false;
            EnsureInitialized();
        }

        /// <summary>
        /// 获取所有已注册的Inspector信息（用于调试）
        /// </summary>
        public static IReadOnlyDictionary<Type, Type> GetAllRegisteredInspectors()
        {
            EnsureInitialized();
            return _inspectorMap;
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;

            _inspectorMap = new Dictionary<Type, Type>();
            var inspectorEntries = new List<(Type dataType, Type inspectorType, int priority)>();

            // 扫描所有程序集
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // 跳过系统程序集
                if (assembly.FullName.StartsWith("System") ||
                    assembly.FullName.StartsWith("mscorlib") ||
                    assembly.FullName.StartsWith("netstandard"))
                {
                    continue;
                }

                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.IsAbstract || type.IsInterface) continue;
                        if (!typeof(CombatInspectorBase).IsAssignableFrom(type)) continue;

                        var attr = type.GetCustomAttribute<CombatInspectorAttribute>();
                        if (attr == null) continue;

                        inspectorEntries.Add((attr.TargetType, type, attr.Priority));
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // 忽略无法加载的程序集
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[CombatInspectorRegistry] Failed to scan assembly {assembly.FullName}: {e.Message}");
                }
            }

            // 按优先级排序，高优先级覆盖低优先级
            foreach (var entry in inspectorEntries.OrderBy(e => e.priority))
            {
                _inspectorMap[entry.dataType] = entry.inspectorType;
            }

            _initialized = true;

            Debug.Log($"[CombatInspectorRegistry] Registered {_inspectorMap.Count} inspectors");
        }
    }
}
