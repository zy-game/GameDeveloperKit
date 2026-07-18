using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;
using UnityEngine;

namespace GameDeveloperKit.Playable
{
    public sealed class ImagePlayableRequest
    {
        public ImagePlayableRequest(string location, Action<Texture> output)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("Location cannot be empty.", nameof(location));
            }

            Location = location;
            Output = output ?? throw new ArgumentNullException(nameof(output));
        }

        public string Location { get; }

        public Action<Texture> Output { get; }
    }

    public sealed class ImagePlayable : PlayableBase<ImagePlayableRequest, ImagePlayableHandle>
    {
        private bool m_Disposed;

        public override async UniTask<ImagePlayableHandle> PlayAsync(
            ImagePlayableRequest request,
            CancellationToken cancellationToken = default)
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(nameof(ImagePlayable));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var assetHandle = await App.Resource.LoadAssetAsync(request.Location);
            if (assetHandle == null || assetHandle.Status != ResourceStatus.Succeeded)
            {
                var exception = assetHandle?.Error ?? new GameException($"Image load failed: {request.Location}");
                assetHandle?.Release();
                throw exception;
            }

            var sprite = assetHandle.GetAsset<Sprite>();
            var texture = sprite != null ? sprite.texture : assetHandle.GetAsset<Texture>();
            if (texture == null)
            {
                assetHandle.Release();
                throw new GameException($"Image asset is not a Texture or Sprite: {request.Location}");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                assetHandle.Release();
                cancellationToken.ThrowIfCancellationRequested();
            }

            return StartLoadedImage(request, texture, assetHandle);
        }

        internal static ImagePlayableHandle StartLoadedImage(
            ImagePlayableRequest request,
            Texture texture,
            AssetHandle assetHandle)
        {
            try
            {
                request.Output(texture);
                var handle = new ImagePlayableHandle(request.Location, texture, request.Output, assetHandle);
                handle.Start();
                assetHandle = null;
                return handle;
            }
            finally
            {
                assetHandle?.Release();
            }
        }

        public override void Dispose()
        {
            m_Disposed = true;
        }
    }

    public sealed class ImagePlayableHandle : PlayableHandle
    {
        private readonly Action<Texture> m_Output;
        private AssetHandle m_AssetHandle;

        internal ImagePlayableHandle(string location, Texture texture, Action<Texture> output, AssetHandle assetHandle)
        {
            Location = location;
            Texture = texture;
            m_Output = output;
            m_AssetHandle = assetHandle;
        }

        public string Location { get; }

        public Texture Texture { get; }

        internal void Start()
        {
            SetPlaying();
        }

        protected override void OnPause()
        {
        }

        protected override void OnResume()
        {
            m_Output(Texture);
        }

        protected override void OnStop()
        {
            m_Output(null);
        }

        protected override void OnDispose()
        {
            var assetHandle = m_AssetHandle;
            m_AssetHandle = null;
            if (assetHandle != null)
            {
                App.Resource.UnloadAsset(assetHandle).Forget(_ => assetHandle.Release());
            }
        }
    }
}
