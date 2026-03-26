using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源句柄，用于管理已加载的资源引用。
    /// </summary>
    public sealed class AssetHandle : IDisposable
    {
        private readonly ResourcePackage.AssetRecord _record;
        private bool _released;

        /// <summary>
        /// 初始化资源句柄的新实例。
        /// </summary>
        /// <param name="record">资源记录。</param>
        /// <exception cref="ArgumentNullException">当record为null时抛出。</exception>
        internal AssetHandle(ResourcePackage.AssetRecord record)
        {
            _record = record ?? throw new ArgumentNullException(nameof(record));
        }

        /// <summary>
        /// 获取包名称。
        /// </summary>
        public string PackageName => _record.PackageName;

        /// <summary>
        /// 获取资源位置。
        /// </summary>
        public ResourceLocation Location => _record.Location.Clone();

        /// <summary>
        /// 获取资源对象。
        /// </summary>
        public UnityEngine.Object Asset => _record.Asset;

        /// <summary>
        /// 获取引用计数。
        /// </summary>
        public int RefCount => _record.RefCount;

        /// <summary>
        /// 获取依赖项数量。
        /// </summary>
        public int DependencyCount => _record.DependencyCount;

        /// <summary>
        /// 获取句柄是否有效。
        /// </summary>
        public bool IsValid => !_released && _record.IsValid;

        /// <summary>
        /// 获取指定类型的资源对象。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <returns>资源对象，如果不匹配类型则返回null。</returns>
        public T GetAsset<T>()
            where T : UnityEngine.Object
        {
            return Asset as T;
        }

        /// <summary>
        /// 增加引用计数。
        /// </summary>
        /// <exception cref="ObjectDisposedException">当句柄已释放时抛出。</exception>
        public void Retain()
        {
            EnsureNotDisposed();
            _record.Retain();
        }

        /// <summary>
        /// 创建一个新的引用句柄。
        /// </summary>
        /// <returns>新的资源句柄。</returns>
        /// <exception cref="ObjectDisposedException">当句柄已释放时抛出。</exception>
        internal AssetHandle CreateReference()
        {
            EnsureNotDisposed();
            _record.Retain();
            return new AssetHandle(_record);
        }

        /// <summary>
        /// 实例化资源（假设为GameObject prefab）。
        /// </summary>
        /// <param name="parent">父级变换。</param>
        /// <param name="worldPositionStays">是否保持世界坐标位置。</param>
        /// <returns>实例化的游戏对象。</returns>
        /// <exception cref="ObjectDisposedException">当句柄已释放时抛出。</exception>
        /// <exception cref="InvalidOperationException">当资源不是GameObject prefab时抛出。</exception>
        public GameObject Instantiate(Transform parent = null, bool worldPositionStays = false)
        {
            EnsureNotDisposed();

            if (!(Asset is GameObject prefab))
            {
                throw new InvalidOperationException("The asset is not a GameObject prefab.");
            }

            GameObject instance;
            if (parent == null)
            {
                instance = UnityEngine.Object.Instantiate(prefab);
            }
            else
            {
                instance = UnityEngine.Object.Instantiate(prefab, parent, worldPositionStays);
            }

            GetOrAddTracker(instance).TrackInstance(CreateReference());
            return instance;
        }

        /// <summary>
        /// 异步实例化资源。
        /// </summary>
        /// <param name="parent">父级变换。</param>
        /// <param name="worldPositionStays">是否保持世界坐标位置。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>实例化的游戏对象。</returns>
        public UniTask<GameObject> InstantiateAsync(Transform parent = null, bool worldPositionStays = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.FromResult(Instantiate(parent, worldPositionStays));
        }

        /// <summary>
        /// 将资源设置为Image组件的sprite。
        /// </summary>
        /// <param name="target">目标Image组件。</param>
        /// <exception cref="ArgumentNullException">当target为null时抛出。</exception>
        public void SetSprite(Image target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var sprite = RequireAsset<Sprite>();
            target.sprite = sprite;
            GetOrAddTracker(target.gameObject).TrackBinding("Image.sprite", CreateReference());
        }

        /// <summary>
        /// 将资源设置为SpriteRenderer组件的sprite。
        /// </summary>
        /// <param name="target">目标SpriteRenderer组件。</param>
        /// <exception cref="ArgumentNullException">当target为null时抛出。</exception>
        public void SetSprite(SpriteRenderer target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var sprite = RequireAsset<Sprite>();
            target.sprite = sprite;
            GetOrAddTracker(target.gameObject).TrackBinding("SpriteRenderer.sprite", CreateReference());
        }

        /// <summary>
        /// 将资源设置为RawImage组件的texture。
        /// </summary>
        /// <param name="target">目标RawImage组件。</param>
        /// <exception cref="ArgumentNullException">当target为null时抛出。</exception>
        public void SetTexture(RawImage target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var texture = RequireAsset<Texture>();
            target.texture = texture;
            GetOrAddTracker(target.gameObject).TrackBinding("RawImage.texture", CreateReference());
        }

        /// <summary>
        /// 将资源设置为Renderer组件的纹理属性。
        /// </summary>
        /// <param name="target">目标Renderer组件。</param>
        /// <param name="propertyName">纹理属性名称。</param>
        /// <exception cref="ArgumentNullException">当target为null时抛出。</exception>
        public void SetTexture(Renderer target, string propertyName = "_MainTex")
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var texture = RequireAsset<Texture>();
            target.material.SetTexture(propertyName, texture);
            GetOrAddTracker(target.gameObject).TrackBinding($"Renderer.texture:{propertyName}", CreateReference());
        }

        /// <summary>
        /// 将资源设置为Renderer组件的材质。
        /// </summary>
        /// <param name="target">目标Renderer组件。</param>
        /// <param name="materialIndex">材质索引。</param>
        /// <exception cref="ArgumentNullException">当target为null时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException">当materialIndex超出范围时抛出。</exception>
        public void SetMaterial(Renderer target, int materialIndex = 0)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var material = RequireAsset<Material>();
            var materials = target.materials;
            if (materialIndex < 0 || materialIndex >= materials.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(materialIndex));
            }

            materials[materialIndex] = material;
            target.materials = materials;
            GetOrAddTracker(target.gameObject).TrackBinding($"Renderer.material:{materialIndex}", CreateReference());
        }

        /// <summary>
        /// 将资源设置为AudioSource组件的clip。
        /// </summary>
        /// <param name="target">目标AudioSource组件。</param>
        /// <exception cref="ArgumentNullException">当target为null时抛出。</exception>
        public void SetAudioClip(AudioSource target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var clip = RequireAsset<AudioClip>();
            target.clip = clip;
            GetOrAddTracker(target.gameObject).TrackBinding("AudioSource.clip", CreateReference());
        }

        /// <summary>
        /// 释放资源句柄。
        /// </summary>
        public void Release()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            if (Game.HasModule<ResourceModule>())
            {
                Game.Resource.NotifyHandleReleased(PackageName, Location);
            }

            _record.Release();
        }

        /// <summary>
        /// 释放资源句柄。
        /// </summary>
        public void Dispose()
        {
            Release();
        }

        private T RequireAsset<T>()
            where T : UnityEngine.Object
        {
            EnsureNotDisposed();

            if (!(Asset is T asset))
            {
                throw new InvalidOperationException($"The asset is not assignable to '{typeof(T).FullName}'.");
            }

            return asset;
        }

        private void EnsureNotDisposed()
        {
            if (_released)
            {
                throw new ObjectDisposedException(nameof(AssetHandle));
            }
        }

        private static ResourceOwnerTracker GetOrAddTracker(GameObject gameObject)
        {
            var tracker = gameObject.GetComponent<ResourceOwnerTracker>();
            if (tracker == null)
            {
                tracker = gameObject.AddComponent<ResourceOwnerTracker>();
            }

            return tracker;
        }
    }
}
