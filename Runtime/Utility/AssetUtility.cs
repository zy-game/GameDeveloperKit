using System;
using System.Collections.Generic;
using GameDeveloperKit.Resource;
using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit
{
    /// <summary>
    /// 资源句柄工具类，提供资源句柄到UI组件的便捷绑定方法。
    /// </summary>
    public static class AssetUtility
    {
        /// <summary>
        /// 资源句柄生命周期绑定组件，用于在GameObject销毁时卸载已绑定资源。
        /// </summary>
        class ReferenceHandle : MonoBehaviour
        {
            private List<AssetHandle> _handles;

            /// <summary>
            /// Unity OnDestroy 回调。
            /// </summary>
            private void OnDestroy()
            {
                if (_handles == null || _handles.Count == 0)
                {
                    return;
                }

                foreach (var handle in _handles)
                {
                    App.Resource.UnloadAsset(handle);
                }
            }

            /// <summary>
            /// 持有资源句柄并释放旧句柄。
            /// </summary>
            /// <param name="handle">资源句柄。</param>
            public void HoldHandle(AssetHandle handle)
            {
                if (_handles == null)
                {
                    _handles = new List<AssetHandle>();
                }

                _handles.Add(handle);
            }

            /// <summary>
            /// 将资源句柄绑定到GameObject生命周期。
            /// </summary>
            /// <param name="handle">资源句柄。</param>
            /// <param name="gameObject">目标GameObject。</param>
            public static void Binding(AssetHandle handle, GameObject gameObject)
            {
                if (handle == null)
                {
                    return;
                }

                if (gameObject == null)
                {
                    return;
                }

                if (gameObject.TryGetComponent(out ReferenceHandle referenceHandle) is false)
                {
                    referenceHandle = gameObject.AddComponent<ReferenceHandle>();
                }

                referenceHandle.HoldHandle(handle);
            }
        }

        /// <summary>
        /// 设置精灵图
        /// </summary>
        /// <param name="handle">资源句柄</param>
        /// <param name="image">精灵图组件</param>
        /// <exception cref="ArgumentNullException">空资源异常</exception>
        public static void SetSprite(this AssetHandle handle, Image image)
        {
            if (image == null)
            {
                throw new System.ArgumentNullException(nameof(image));
            }

            image.sprite = handle.GetAsset<Sprite>();
            ReferenceHandle.Binding(handle, image.gameObject);
        }

        /// <summary>
        /// 设置图片
        /// </summary>
        /// <param name="handle">资源句柄</param>
        /// <param name="image">图片组件</param>
        /// <exception cref="ArgumentNullException">空资源异常</exception>
        public static void SetTexture(this AssetHandle handle, RawImage image)
        {
            if (image == null)
            {
                throw new System.ArgumentNullException(nameof(image));
            }

            image.texture = handle.GetAsset<Texture2D>();
            ReferenceHandle.Binding(handle, image.gameObject);
        }
    }
}
