using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.Runtime
{
    public sealed class AssetHandle : IDisposable
    {
        private readonly ResourcePackage.AssetRecord _record;
        private bool _released;

        internal AssetHandle(ResourcePackage.AssetRecord record)
        {
            _record = record ?? throw new ArgumentNullException(nameof(record));
        }

        public string PackageName => _record.PackageName;

        public ResourceLocation Location => _record.Location.Clone();

        public UnityEngine.Object Asset => _record.Asset;

        public int RefCount => _record.RefCount;

        public bool IsValid => !_released && _record.IsValid;

        public T GetAsset<T>()
            where T : UnityEngine.Object
        {
            return Asset as T;
        }

        public void Retain()
        {
            EnsureNotDisposed();
            _record.Retain();
        }

        internal AssetHandle CreateReference()
        {
            EnsureNotDisposed();
            _record.Retain();
            return new AssetHandle(_record);
        }

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

        public UniTask<GameObject> InstantiateAsync(Transform parent = null, bool worldPositionStays = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.FromResult(Instantiate(parent, worldPositionStays));
        }

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

        public void Release()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            _record.Release();
        }

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
