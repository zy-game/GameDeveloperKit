using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Runtime
{
    public sealed class SceneHandle
    {
        private readonly string _scenePath;
        private bool _released;

        internal SceneHandle(string packageName, ResourceLocation location, Scene scene, string scenePath)
        {
            PackageName = packageName;
            Location = location?.Clone() ?? new ResourceLocation();
            Scene = scene;
            _scenePath = scenePath;
        }

        public string PackageName { get; }

        public ResourceLocation Location { get; }

        public Scene Scene { get; }

        public async UniTask UnloadAsync(CancellationToken cancellationToken = default)
        {
            if (_released)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (Scene.IsValid() && Scene.isLoaded)
            {
                await SceneManager.UnloadSceneAsync(Scene);
            }
            else if (!string.IsNullOrWhiteSpace(_scenePath))
            {
                await SceneManager.UnloadSceneAsync(_scenePath);
            }

            _released = true;
        }
    }
}
