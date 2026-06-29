using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace GameDeveloperKit
{
    /// <summary>
    /// 模块注册表，负责模块实例的存储、按需创建、依赖解析和失败回滚。
    /// </summary>
    public sealed class ModuleRegistry
    {
        private readonly Dictionary<Type, IGameModule> _modules = new Dictionary<Type, IGameModule>();
        private readonly List<Type> _moduleOrder = new List<Type>();
        private Dictionary<Type, IGameModule> _assignableCache;
        private bool _assignableCacheDirty;
        private Func<bool> _isShuttingDown;

        internal IReadOnlyList<Type> ModuleOrder => _moduleOrder;

        internal void SetShuttingDownCheck(Func<bool> check)
        {
            _isShuttingDown = check ?? throw new ArgumentNullException(nameof(check));
        }

        /// <summary>
        /// 获取或创建模块，并递归启动声明的模块依赖。
        /// </summary>
        public T GetModule<T>() where T : class, IGameModule, new()
        {
            return (T)ResolveModuleWithRollback(typeof(T), _isShuttingDown ?? (() => false));
        }

        /// <summary>
        /// 注册模块（触发依赖解析和启动）。
        /// </summary>
        public void Register<T>() where T : class, IGameModule, new()
        {
            var type = typeof(T);
            if (TryGetRegistered(type, out _))
            {
                throw new GameException($"Module '{type.Name}' has already been registered.");
            }

            GetModule<T>();
        }

        /// <summary>
        /// 卸载模块。
        /// </summary>
        public void Unregister<T>() where T : IGameModule
        {
            var type = typeof(T);
            if (!TryGetRegistered(type, out var module))
            {
                throw new GameException($"Module '{type.Name}' is not registered.");
            }

            var registeredType = module.GetType();
            _modules.Remove(registeredType);
            InvalidateAssignableCache();
            RemoveModuleOrder(registeredType);
            module.Shutdown();
        }

        /// <summary>
        /// 尝试获取已注册模块。
        /// </summary>
        public bool TryGetRegistered<T>(out T module) where T : class, IGameModule
        {
            if (TryGetRegistered(typeof(T), out var value))
            {
                module = (T)value;
                return true;
            }

            module = null;
            return false;
        }

        /// <summary>
        /// 尝试获取已注册模块（按类型）。先精确匹配，再查可赋值类型缓存。
        /// </summary>
        public bool TryGetRegistered(Type type, out IGameModule module)
        {
            if (_modules.TryGetValue(type, out module))
            {
                return true;
            }

            if (_assignableCacheDirty)
            {
                RebuildAssignableCache();
            }

            if (_assignableCache != null && _assignableCache.TryGetValue(type, out module))
            {
                return true;
            }

            module = null;
            return false;
        }

        private void InvalidateAssignableCache()
        {
            _assignableCacheDirty = true;
        }

        private void RebuildAssignableCache()
        {
            if (_modules.Count == 0)
            {
                _assignableCache = null;
                _assignableCacheDirty = false;
                return;
            }

            _assignableCache = new Dictionary<Type, IGameModule>();
            foreach (var kvp in _modules)
            {
                var concreteType = kvp.Key;
                var module = kvp.Value;
                _assignableCache[concreteType] = module;
                foreach (var iface in concreteType.GetInterfaces())
                {
                    if (typeof(IGameModule).IsAssignableFrom(iface))
                    {
                        _assignableCache[iface] = module;
                    }
                }

                var baseType = concreteType.BaseType;
                while (baseType != null && typeof(IGameModule).IsAssignableFrom(baseType))
                {
                    _assignableCache[baseType] = module;
                    baseType = baseType.BaseType;
                }
            }

            _assignableCacheDirty = false;
        }

        /// <summary>
        /// 按反序关闭所有已注册模块并清空注册表。
        /// </summary>
        internal Exception ShutdownModules()
        {
            var exceptions = new List<Exception>();
            for (var i = _moduleOrder.Count - 1; i >= 0; i--)
            {
                var type = _moduleOrder[i];
                if (!_modules.TryGetValue(type, out var module))
                {
                    continue;
                }

                try
                {
                    module.Shutdown();
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
            }

            _modules.Clear();
            _moduleOrder.Clear();
            InvalidateAssignableCache();

            if (exceptions.Count == 0)
            {
                return null;
            }

            return exceptions.Count == 1
                ? exceptions[0]
                : new AggregateException($"{exceptions.Count} modules threw exceptions during shutdown.", exceptions);
        }

        internal IGameModule ResolveModuleWithRollback(Type moduleType, Func<bool> isShuttingDown)
        {
            if (isShuttingDown())
            {
                throw new GameException($"Cannot resolve module '{moduleType.Name}' while framework is shutting down.");
            }

            var createdTypes = new List<Type>();
            try
            {
                return ResolveModule(moduleType, new List<Type>(), createdTypes);
            }
            catch (Exception exception)
            {
                var cleanupException = RollbackCreatedModules(createdTypes);
                if (cleanupException != null)
                {
                    throw new AggregateException($"Module '{moduleType.Name}' startup failed and rollback failed.", exception, cleanupException);
                }

                ExceptionDispatchInfo.Capture(exception).Throw();
                throw;
            }
        }

        internal void TrackModuleOrder(Type type, bool moveToEnd = false)
        {
            if (moveToEnd)
            {
                _moduleOrder.Remove(type);
            }

            if (!_moduleOrder.Contains(type))
            {
                _moduleOrder.Add(type);
            }
        }

        internal void RemoveModuleOrder(Type type)
        {
            _moduleOrder.Remove(type);
        }

        private IGameModule ResolveModule(Type moduleType, List<Type> resolvingTypes, List<Type> createdTypes)
        {
            ValidateModuleType(moduleType);
            if (TryGetRegistered(moduleType, out var existingModule))
            {
                return existingModule;
            }

            if (resolvingTypes.Contains(moduleType))
            {
                throw new GameException($"Circular module dependency detected: {FormatDependencyChain(resolvingTypes, moduleType)}.");
            }

            resolvingTypes.Add(moduleType);
            try
            {
                var dependencyTypes = GetModuleDependencyTypes(moduleType);
                for (var i = 0; i < dependencyTypes.Count; i++)
                {
                    ResolveModule(dependencyTypes[i], resolvingTypes, createdTypes);
                }

                if (TryGetRegistered(moduleType, out existingModule))
                {
                    return existingModule;
                }

                var module = (IGameModule)Activator.CreateInstance(moduleType);
                _modules.Add(moduleType, module);
                InvalidateAssignableCache();
                try
                {
                    module.Startup();
                    TrackModuleOrder(moduleType);
                    createdTypes.Add(moduleType);
                    return module;
                }
                catch (Exception exception)
                {
                    _modules.Remove(moduleType);
                    InvalidateAssignableCache();
                    RemoveModuleOrder(moduleType);
                    throw new GameException($"Failed to start module '{moduleType.Name}'.", exception);
                }
            }
            finally
            {
                resolvingTypes.RemoveAt(resolvingTypes.Count - 1);
            }
        }

        private static List<Type> GetModuleDependencyTypes(Type moduleType)
        {
            var attributes = (ModuleDependencyAttribute[])Attribute.GetCustomAttributes(
                moduleType,
                typeof(ModuleDependencyAttribute),
                false);
            var dependencyTypes = new List<Type>(attributes.Length);
            for (var i = 0; i < attributes.Length; i++)
            {
                var dependencyType = attributes[i].DependencyType;
                ValidateModuleType(dependencyType);
                if (!dependencyTypes.Contains(dependencyType))
                {
                    dependencyTypes.Add(dependencyType);
                }
            }

            return dependencyTypes;
        }

        private static void ValidateModuleType(Type moduleType)
        {
            if (moduleType == null)
            {
                throw new ArgumentNullException(nameof(moduleType));
            }

            if (!typeof(IGameModule).IsAssignableFrom(moduleType))
            {
                throw new GameException($"Module type '{moduleType.FullName}' must implement IGameModule.");
            }

            if (moduleType.IsInterface || moduleType.IsAbstract)
            {
                throw new GameException($"Module type '{moduleType.FullName}' must be a concrete type.");
            }

            if (moduleType.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new GameException($"Module type '{moduleType.FullName}' must have a public parameterless constructor.");
            }
        }

        private Exception RollbackCreatedModules(List<Type> createdTypes)
        {
            var exceptions = new List<Exception>();
            for (var i = createdTypes.Count - 1; i >= 0; i--)
            {
                var type = createdTypes[i];
                if (!_modules.TryGetValue(type, out var module))
                {
                    continue;
                }

                try
                {
                    module.Shutdown();
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
                finally
                {
                    _modules.Remove(type);
                    InvalidateAssignableCache();
                    RemoveModuleOrder(type);
                }
            }

            if (exceptions.Count == 0)
            {
                return null;
            }

            return exceptions.Count == 1
                ? exceptions[0]
                : new AggregateException($"{exceptions.Count} modules threw exceptions during rollback.", exceptions);
        }

        private static string FormatDependencyChain(List<Type> resolvingTypes, Type repeatedType)
        {
            var cycleIndex = resolvingTypes.IndexOf(repeatedType);
            if (cycleIndex < 0)
            {
                cycleIndex = 0;
            }

            var names = new List<string>();
            for (var i = cycleIndex; i < resolvingTypes.Count; i++)
            {
                names.Add(resolvingTypes[i].Name);
            }

            names.Add(repeatedType.Name);
            return string.Join(" -> ", names);
        }
    }
}
