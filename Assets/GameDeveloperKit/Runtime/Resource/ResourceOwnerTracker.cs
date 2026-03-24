using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    public sealed class ResourceOwnerTracker : MonoBehaviour
    {
        private readonly Dictionary<string, AssetHandle> _bindings = new();
        private readonly List<AssetHandle> _instanceHandles = new();

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

        public void TrackInstance(AssetHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            _instanceHandles.Add(handle);
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
