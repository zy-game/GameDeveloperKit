namespace GameDeveloperKit.Resource
{
    using Cysharp.Threading.Tasks;
    using Cysharp.Threading.Tasks.Triggers;
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// 资源引用句柄
    /// </summary>
    /// <typeparam name="T">资源类型</typeparam>
    public class AssetHandle<T> : BaseHandle where T : UnityEngine.Object
    {
        private T _asset;
        private AssetInfo _manifest;
        private readonly bool _isValid;

        /// <summary>
        /// 句柄是否有效（资源加载成功）
        /// </summary>
        public bool IsValid => _isValid;

        /// <summary>
        /// 资源实例
        /// </summary>
        public T Asset => _asset;

        /// <summary>
        /// 资源名称
        /// </summary>
        public override string Name => _manifest.name;

        /// <summary>
        /// 资源地址
        /// </summary>
        public override string Address => _manifest.address;

        /// <summary>
        /// 资源GUID
        /// </summary>
        public override string GUID => _manifest.guid;

        private AssetHandle(T asset, AssetInfo manifest, bool isValid)
        {
            _asset = asset;
            _manifest = manifest;
            _isValid = isValid;
        }

        /// <summary>
        /// 创建成功的资源句柄
        /// </summary>
        public static AssetHandle<T> Success(T asset, AssetInfo manifest)
        {
            if (asset == null)
            {
                throw new System.ArgumentNullException(nameof(asset), "Asset cannot be null for success handle");
            }
            return new AssetHandle<T>(asset, manifest, true);
        }

        /// <summary>
        /// 创建失败的资源句柄
        /// </summary>
        internal static AssetHandle<T> Failure(string address)
        {
            var dummyManifest = new AssetInfo
            {
                address = address,
                name = System.IO.Path.GetFileNameWithoutExtension(address),
                guid = string.Empty
            };
            return new AssetHandle<T>(null, dummyManifest, false);
        }

        protected override void OnDispose()
        {
            Game.Resource.Unload(this);
        }

        public override void OnClearup()
        {
            base.OnClearup();
            Object.Destroy(_asset);
            _manifest = default;
            _asset = default;
        }

        /// <summary>
        /// 实例化资源
        /// </summary>
        /// <param name="isWorld"></param>
        /// <returns></returns>
        public GameObject Instantiate(bool isWorld = false)
        {
            if (!IsValid)
            {
                Game.Debug.Error($"[AssetHandle] Cannot instantiate failed asset: {Address}");
                return null;
            }
            return Instantiate(default, Vector3.zero, Quaternion.identity, Vector3.one, isWorld);
        }

        /// <summary>
        /// 实例化资源
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="position"></param>
        /// <param name="quaternion"></param>
        /// <param name="scale"></param>
        /// <param name="isWorld"></param>
        /// <returns></returns>
        public GameObject Instantiate(Transform parent, Vector3 position, Quaternion quaternion, Vector3 scale, bool isWorld = false)
        {
            if (!IsValid)
            {
                Game.Debug.Error($"[AssetHandle] Cannot instantiate failed asset: {Address}");
                return null;
            }

            if (Asset is not GameObject prefab)
            {
                Game.Debug.Warning($"[AssetHandle] Asset is not a GameObject: {Address}");
                return null;
            }

            var go = UnityEngine.Object.Instantiate(prefab, parent);
            var t = go.transform;
            if (parent != null && !isWorld)
            {
                t.localPosition = position;
                t.localRotation = quaternion;
                t.localScale = scale;
            }
            else
            {
                t.position = position;
                t.rotation = quaternion;
                t.localScale = scale;
            }

            AttachTracker(go);
            return go;
        }

        /// <summary>
        /// 异步实例化
        /// </summary>
        /// <param name="count"></param>
        /// <param name="isWorld"></param>
        /// <returns></returns>
        public UniTask<GameObject[]> InstantiateAsync(int count, bool isWorld = false)
        {
            if (!IsValid)
            {
                Game.Debug.Error($"[AssetHandle] Cannot instantiate async failed asset: {Address}");
                return UniTask.FromResult(System.Array.Empty<GameObject>());
            }
            return InstantiateAsync(count, default, Vector3.zero, Quaternion.identity, Vector3.one, isWorld);
        }

        /// <summary>
        /// 异步实例化
        /// </summary>
        /// <param name="count"></param>
        /// <param name="parent"></param>
        /// <param name="position"></param>
        /// <param name="quaternion"></param>
        /// <param name="scale"></param>
        /// <param name="isWorld"></param>
        /// <returns></returns>
        public async UniTask<GameObject[]> InstantiateAsync(int count, Transform parent, Vector3 position, Quaternion quaternion, Vector3 scale, bool isWorld = false)
        {
            if (!IsValid)
            {
                Game.Debug.Error($"[AssetHandle] Cannot instantiate async failed asset: {Address}");
                return System.Array.Empty<GameObject>();
            }

            if (count <= 0)
            {
                return System.Array.Empty<GameObject>();
            }

            var gameObjects = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                gameObjects[i] = Instantiate(parent, position, quaternion, scale, isWorld);
                await UniTask.Yield();
            }

            return gameObjects;
        }

        /// <summary>
        /// 设置精灵
        /// </summary>
        /// <param name="image">Image组件</param>
        public void SetSprite(Image image)
        {
            if (!IsValid)
            {
                Game.Debug.Error($"[AssetHandle] Cannot set sprite from failed asset: {Address}");
                return;
            }

            if (image != null && Asset is Sprite sprite)
            {
                image.sprite = sprite;
                AttachTracker(image.gameObject);
            }
        }

        /// <summary>
        /// 设置2D纹理
        /// </summary>
        /// <param name="rawImage">RawImage组件</param>
        public void SetTexture2D(RawImage rawImage)
        {
            if (!IsValid)
            {
                Game.Debug.Error($"[AssetHandle] Cannot set texture from failed asset: {Address}");
                return;
            }

            if (rawImage != null && Asset is Texture2D texture)
            {
                rawImage.texture = texture;
                AttachTracker(rawImage.gameObject);
            }
        }

        /// <summary>
        /// 设置音频剪辑
        /// </summary>
        /// <param name="audioSource">AudioSource组件</param>
        public void SetAudioClip(AudioSource audioSource)
        {
            if (!IsValid)
            {
                Game.Debug.Error($"[AssetHandle] Cannot set audio clip from failed asset: {Address}");
                return;
            }

            if (audioSource != null && Asset is AudioClip audioClip)
            {
                audioSource.clip = audioClip;
                AttachTracker(audioSource.gameObject);
            }
        }

        /// <summary>
        /// 设置文本
        /// </summary>
        /// <param name="text"></param>
        public void SetText(TMP_Text text)
        {
            if (!IsValid)
            {
                Game.Debug.Error($"[AssetHandle] Cannot set text from failed asset: {Address}");
                return;
            }

            if (text != null && Asset is TextAsset textAsset)
            {
                text.text = textAsset.text;
                AttachTracker(text.gameObject);
            }
        }

        private void AttachTracker(GameObject obj)
        {
            if (obj == null) return;
            var tracker = obj.GetComponent<ResourceLifecycleTracker>();
            if (tracker == null)
            {
                tracker = obj.AddComponent<ResourceLifecycleTracker>();
            }

            tracker.AddHandle(this);
        }
    }
    public class ResourceLifecycleTracker : MonoBehaviour
    {
        private readonly System.Collections.Generic.List<BaseHandle> _handles = new System.Collections.Generic.List<BaseHandle>();

        public void AddHandle(BaseHandle handle)
        {
            _handles.Add(handle);
            handle.Retain();
        }

        private void OnDestroy()
        {
            foreach (var handle in _handles)
            {
                Game.Resource.Unload(handle);
            }
        }
    }
}