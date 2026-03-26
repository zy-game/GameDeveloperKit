using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源所有者跟踪器，用于自动管理GameObject上的资源句柄生命周期。
    /// </summary>
    public sealed class ResourceOwnerTracker : MonoBehaviour
    {
        private readonly Dictionary<string, AssetHandle> _bindings = new();
        private readonly List<AssetHandle> _instanceHandles = new();

        /// <summary>
        /// 跟踪绑定到特定槽位的资源句柄。
        /// </summary>
        /// <param name="slotKey">槽位键。</param>
        /// <param name="handle">资源句柄。</param>
        public void TrackBinding(string slotKey, AssetHandle handle)
        {
            if (string.IsNullOrWhiteSpace(slotKey) || handle == null)
            {
                return;
            }

            if (_bindings.TryGetValue(slotKey, out var previousHandle))
            {
                if (ReferenceEquals(previousHandle, handle))
                {
                    return;
                }

                previousHandle.Release();
            }

            _bindings[slotKey] = handle;
        }

        /// <summary>
        /// 跟踪实例化的资源句柄。
        /// </summary>
        /// <param name="handle">资源句柄。</param>
        public void TrackInstance(AssetHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            _instanceHandles.Add(handle);
        }

        /// <summary>
        /// 清除指定槽位的绑定并释放资源句柄。
        /// </summary>
        /// <param name="slotKey">槽位键。</param>
        public void ClearBinding(string slotKey)
        {
            if (string.IsNullOrWhiteSpace(slotKey))
            {
                return;
            }

            if (!_bindings.Remove(slotKey, out var handle))
            {
                return;
            }

            handle.Release();
        }

        private void OnDestroy()
        {
            ReleaseBindings();
            ReleaseInstances();
        }

        private void ReleaseBindings()
        {
            foreach (var pair in _bindings)
            {
                pair.Value.Release();
            }

            _bindings.Clear();
        }

        private void ReleaseInstances()
        {
            for (var i = 0; i < _instanceHandles.Count; i++)
            {
                _instanceHandles[i].Release();
            }

            _instanceHandles.Clear();
        }
    }
}
