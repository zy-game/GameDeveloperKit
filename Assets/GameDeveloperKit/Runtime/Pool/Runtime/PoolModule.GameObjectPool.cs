using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    public sealed partial class PoolModule
    {
        /// <summary>
        /// 管理 GameObject 对象池的内部类。
        /// </summary>
        /// <remarks>
        /// 此类负责管理基于预制体的 GameObject 对象池，包括对象的创建、获取、释放和销毁。
        /// 使用栈结构存储未激活的实例，以实现高效的复用。
        /// </remarks>
        private sealed class GameObjectPool
        {
            private readonly Stack<GameObject> _inactiveInstances = new();
            private readonly Transform _poolRoot;

            /// <summary>
            /// 初始化 GameObjectPool 的新实例。
            /// </summary>
            /// <param name="prefab">用于实例化的预制体。</param>
            /// <param name="root">池根节点 Transform，用于组织所有池中的对象。</param>
            public GameObjectPool(GameObject prefab, Transform root)
            {
                Prefab = prefab;
                _poolRoot = root;
            }

            /// <summary>
            /// 获取此池使用的预制体。
            /// </summary>
            public GameObject Prefab { get; }

            /// <summary>
            /// 获取当前池中未激活的实例数量。
            /// </summary>
            public int InactiveCount => _inactiveInstances.Count;

            /// <summary>
            /// 获取此池创建的总实例数量（包括已销毁的实例）。
            /// </summary>
            public int TotalCount { get; private set; }

            /// <summary>
            /// 获取或设置池中允许保留的最大未激活实例数量。
            /// </summary>
            /// <remarks>
            /// 当释放对象时，如果未激活实例数量超过此值，多余的实例将被销毁而不是放回池中。
            /// 默认值为 32。
            /// </remarks>
            public int MaxInactiveCount { get; set; } = 32;

            /// <summary>
            /// 预热对象池，提前创建指定数量的实例。
            /// </summary>
            /// <param name="count">要预热到的实例数量。</param>
            /// <remarks>
            /// 此方法会创建足够的实例，使池中未激活的实例数量达到指定的 count。
            /// 如果当前未激活数量已达到或超过 count，则不会创建新实例。
            /// </remarks>
            public void Warmup(int count)
            {
                for (var i = InactiveCount; i < count; i++)
                {
                    _inactiveInstances.Push(CreateInstance());
                }
            }

            /// <summary>
            /// 从对象池中生成一个实例。
            /// </summary>
            /// <param name="parent">生成实例的父 Transform。</param>
            /// <param name="position">生成实例的位置。</param>
            /// <param name="rotation">生成实例的旋转。</param>
            /// <returns>从池中获取并激活的 GameObject 实例。</returns>
            /// <remarks>
            /// 此方法首先尝试从池中获取未激活的实例。如果池为空，则创建新实例。
            /// 生成的实例会被激活，设置到指定的位置和旋转，并通知所有实现了 IGameObjectPoolable 的组件。
            /// </remarks>
            public GameObject Spawn(Transform parent, Vector3 position, Quaternion rotation)
            {
                var instance = _inactiveInstances.Count > 0 ? _inactiveInstances.Pop() : CreateInstance();
                instance.transform.SetParent(parent, false);
                instance.transform.SetPositionAndRotation(position, rotation);
                instance.SetActive(true);
                NotifySpawned(instance);
                return instance;
            }

            /// <summary>
            /// 将实例释放回对象池。
            /// </summary>
            /// <param name="instance">要释放回池中的 GameObject 实例。</param>
            /// <remarks>
            /// 如果池中未激活实例数量超过 MaxInactiveCount，则直接销毁实例。
            /// 否则，将实例设为未激活状态并放回池中。
            /// 释放前会通知所有实现了 IGameObjectPoolable 的组件。
            /// </remarks>
            public void Despawn(GameObject instance)
            {
                NotifyDespawned(instance);
                if (_inactiveInstances.Count >= MaxInactiveCount)
                {
                    UnityEngine.Object.Destroy(instance);
                    TotalCount = Math.Max(0, TotalCount - 1);
                    return;
                }

                instance.transform.SetParent(_poolRoot, false);
                instance.SetActive(false);
                _inactiveInstances.Push(instance);
            }

            /// <summary>
            /// 销毁池中所有未激活的实例。
            /// </summary>
            /// <remarks>
            /// 此方法会清除池中所有实例并销毁它们。操作完成后，池将变空。
            /// 应谨慎使用此方法，因为它会释放所有可复用的实例。
            /// </remarks>
            public void DestroyAll()
            {
                while (_inactiveInstances.Count > 0)
                {
                    var instance = _inactiveInstances.Pop();
                    if (instance != null)
                    {
                        UnityEngine.Object.Destroy(instance);
                    }
                }
            }

            /// <summary>
            /// 创建一个新的池实例。
            /// </summary>
            /// <returns>新创建并初始化的 GameObject 实例。</returns>
            /// <remarks>
            /// 此方法从预制体实例化新对象，将其设置为未激活状态，
            /// 添加 PooledInstanceMarker 组件以标记其为池实例，并增加总实例计数。
            /// </remarks>
            private GameObject CreateInstance()
            {
                var instance = UnityEngine.Object.Instantiate(Prefab, _poolRoot);
                instance.name = $"{Prefab.name}(Pooled)";
                instance.SetActive(false);
                TotalCount++;

                var marker = instance.GetComponent<PooledInstanceMarker>();
                if (marker == null)
                {
                    marker = instance.AddComponent<PooledInstanceMarker>();
                }

                marker.Prefab = Prefab;
                return instance;
            }

            /// <summary>
            /// 通知实例及其子对象中的所有实现了 IGameObjectPoolable 的组件对象已被生成。
            /// </summary>
            /// <param name="instance">刚从池中生成的 GameObject 实例。</param>
            private static void NotifySpawned(GameObject instance)
            {
                var poolables = instance.GetComponentsInChildren<MonoBehaviour>(true);
                for (var i = 0; i < poolables.Length; i++)
                {
                    if (poolables[i] is IGameObjectPoolable poolable)
                    {
                        poolable.OnSpawnedFromPool();
                    }
                }
            }

            /// <summary>
            /// 通知实例及其子对象中的所有实现了 IGameObjectPoolable 的组件对象将被释放回池。
            /// </summary>
            /// <param name="instance">即将被释放回池的 GameObject 实例。</param>
            private static void NotifyDespawned(GameObject instance)
            {
                var poolables = instance.GetComponentsInChildren<MonoBehaviour>(true);
                for (var i = 0; i < poolables.Length; i++)
                {
                    if (poolables[i] is IGameObjectPoolable poolable)
                    {
                        poolable.OnDespawnedToPool();
                    }
                }
            }
        }
    }
}
