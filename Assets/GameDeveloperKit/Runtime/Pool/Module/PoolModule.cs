using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 对象池模块，提供游戏对象和引用对象的对象池管理功能
    /// </summary>
    public sealed partial class PoolModule : IGameFrameworkModule
    {
        private readonly Dictionary<GameObject, GameObjectPool> _pools = new();
        private readonly Dictionary<Type, ReferencePoolState> _referencePools = new();
        private readonly Transform _root;
        private bool _diagnosticsRegistered;

        /// <summary>
        /// 初始化 PoolModule 的新实例。
        /// </summary>
        public PoolModule()
        {
            var rootObject = new GameObject("[GameDeveloperKit.Pool]");
            UnityEngine.Object.DontDestroyOnLoad(rootObject);
            _root = rootObject.transform;
            ReferencePool = new ReferencePoolAccessor(this);
        }

        /// <summary>
        /// 获取引用对象池访问器
        /// </summary>
        public ReferencePoolAccessor ReferencePool { get; }

        /// <summary>
        /// 获取游戏对象池数量
        /// </summary>
        public int PoolCount => _pools.Count;

        /// <summary>
        /// 获取引用对象池数量
        /// </summary>
        public int ReferencePoolCount => _referencePools.Count;

        /// <summary>
        /// 预热游戏对象池
        /// </summary>
        /// <param name="prefab">预制体</param>
        /// <param name="count">预热数量</param>
        /// <exception cref="ArgumentNullException">预制体为空</exception>
        /// <exception cref="ArgumentOutOfRangeException">数量小于0</exception>
        public void Warmup(GameObject prefab, int count)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            GetOrCreatePool(prefab).Warmup(count);
            EnsureDiagnosticsSnapshotProviders();
        }

        /// <summary>
        /// 生成游戏对象
        /// </summary>
        /// <param name="prefab">预制体</param>
        /// <param name="parent">父节点</param>
        /// <returns>游戏对象</returns>
        /// <exception cref="ArgumentNullException">预制体为空</exception>
        public GameObject Spawn(GameObject prefab, Transform parent = null)
        {
            return Spawn(prefab, Vector3.zero, Quaternion.identity, parent);
        }

        /// <summary>
        /// 生成游戏对象
        /// </summary>
        /// <param name="prefab">预制体</param>
        /// <param name="position">位置</param>
        /// <param name="rotation">旋转</param>
        /// <param name="parent">父节点</param>
        /// <returns>游戏对象</returns>
        /// <exception cref="ArgumentNullException">预制体为空</exception>
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            return GetOrCreatePool(prefab).Spawn(parent, position, rotation);
        }

        /// <summary>
        /// 回收游戏对象
        /// </summary>
        /// <param name="instance">游戏对象实例</param>
        /// <returns>如果回收成功返回true，否则返回false</returns>
        public bool Despawn(GameObject instance)
        {
            if (instance == null)
            {
                return false;
            }

            var marker = instance.GetComponent<PooledInstanceMarker>();
            if (marker == null || marker.Prefab == null || !_pools.TryGetValue(marker.Prefab, out var pool))
            {
                return false;
            }

            pool.Despawn(instance);
            EnsureDiagnosticsSnapshotProviders();
            return true;
        }

        /// <summary>
        /// 设置游戏对象池容量
        /// </summary>
        /// <param name="prefab">预制体</param>
        /// <param name="maxInactiveCount">最大非活动数量</param>
        /// <exception cref="ArgumentNullException">预制体为空</exception>
        /// <exception cref="ArgumentOutOfRangeException">最大数量小于0</exception>
        public void SetCapacity(GameObject prefab, int maxInactiveCount)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (maxInactiveCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInactiveCount));
            }

            GetOrCreatePool(prefab).MaxInactiveCount = maxInactiveCount;
        }

        /// <summary>
        /// 获取引用对象
        /// </summary>
        /// <typeparam name="T">引用对象类型</typeparam>
        /// <returns>引用对象实例</returns>
        public T Acquire<T>()
            where T : class, new()
        {
            var type = typeof(T);
            if (_referencePools.TryGetValue(type, out var pool) && pool.Items.Count > 0)
            {
                return (T)pool.Items.Pop();
            }

            return new T();
        }

        /// <summary>
        /// 释放引用对象
        /// </summary>
        /// <typeparam name="T">引用对象类型</typeparam>
        /// <param name="instance">引用对象实例</param>
        public void Release<T>(T instance)
            where T : class
        {
            if (instance == null)
            {
                return;
            }

            if (instance is IReferencePoolable poolable)
            {
                poolable.ResetForPool();
            }

            var state = GetOrCreateReferencePool(typeof(T));
            if (state.Items.Count >= state.MaxCount)
            {
                return;
            }

            state.Items.Push(instance);
            EnsureDiagnosticsSnapshotProviders();
        }

        /// <summary>
        /// 释放对象池模块占用的所有资源
        /// </summary>
        public void Dispose()
        {
            foreach (var pool in _pools.Values)
            {
                pool.DestroyAll();
            }

            RemoveDiagnosticsSnapshotProviders();
            _pools.Clear();
            _referencePools.Clear();

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
            }
        }

        private void WarmupReferencePool<T>(int count)
            where T : class, new()
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var pool = GetOrCreateReferencePool(typeof(T));

            while (pool.Items.Count < count && pool.Items.Count < pool.MaxCount)
            {
                pool.Items.Push(new T());
            }
        }

        private void ClearReferencePool<T>()
            where T : class
        {
            _referencePools.Remove(typeof(T));
        }

        /// <summary>
        /// 设置引用对象池容量
        /// </summary>
        /// <typeparam name="T">引用对象类型</typeparam>
        /// <param name="maxCount">最大数量</param>
        /// <exception cref="ArgumentOutOfRangeException">最大数量小于0</exception>
        public void SetReferencePoolCapacity<T>(int maxCount)
            where T : class
        {
            if (maxCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount));
            }

            GetOrCreateReferencePool(typeof(T)).MaxCount = maxCount;
        }

        /// <summary>
        /// 获取游戏对象池的非活动对象数量
        /// </summary>
        /// <param name="prefab">预制体</param>
        /// <returns>非活动对象数量</returns>
        public int GetInactiveCount(GameObject prefab)
        {
            return prefab != null && _pools.TryGetValue(prefab, out var pool) ? pool.InactiveCount : 0;
        }

        /// <summary>
        /// 获取引用对象池的非活动对象数量
        /// </summary>
        /// <typeparam name="T">引用对象类型</typeparam>
        /// <returns>非活动对象数量</returns>
        public int GetReferenceInactiveCount<T>()
            where T : class
        {
            return _referencePools.TryGetValue(typeof(T), out var pool) ? pool.Items.Count : 0;
        }

        private GameObjectPool GetOrCreatePool(GameObject prefab)
        {
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new GameObjectPool(prefab, _root);
                _pools.Add(prefab, pool);
            }

            return pool;
        }

        private ReferencePoolState GetOrCreateReferencePool(Type type)
        {
            if (!_referencePools.TryGetValue(type, out var pool))
            {
                pool = new ReferencePoolState();
                _referencePools.Add(type, pool);
            }

            return pool;
        }

        private void EnsureDiagnosticsSnapshotProviders()
        {
            if (_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RegisterSnapshotProvider("Pool.GameObjectPoolCount", () => PoolCount.ToString());
            diagnostics.RegisterSnapshotProvider("Pool.ReferencePoolCount", () => ReferencePoolCount.ToString());
            diagnostics.RegisterSnapshotProvider("Pool.TotalInactiveGameObjects", GetTotalInactiveGameObjectsSnapshot);
            diagnostics.RegisterSnapshotProvider("Pool.TotalInactiveReferences", GetTotalInactiveReferencesSnapshot);
            _diagnosticsRegistered = true;
        }

        private void RemoveDiagnosticsSnapshotProviders()
        {
            if (!_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RemoveSnapshotProvider("Pool.GameObjectPoolCount");
            diagnostics.RemoveSnapshotProvider("Pool.ReferencePoolCount");
            diagnostics.RemoveSnapshotProvider("Pool.TotalInactiveGameObjects");
            diagnostics.RemoveSnapshotProvider("Pool.TotalInactiveReferences");
            _diagnosticsRegistered = false;
        }

        private string GetTotalInactiveGameObjectsSnapshot()
        {
            var total = 0;
            foreach (var pool in _pools.Values)
            {
                total += pool.InactiveCount;
            }

            return total.ToString();
        }

        private string GetTotalInactiveReferencesSnapshot()
        {
            var total = 0;
            foreach (var pool in _referencePools.Values)
            {
                total += pool.Items.Count;
            }

            return total.ToString();
        }
    }
}
