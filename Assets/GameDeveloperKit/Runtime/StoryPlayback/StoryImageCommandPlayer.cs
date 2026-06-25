using System;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;
using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 使用 UGUI RawImage 显示 Story 图片命令。
    /// </summary>
    public sealed class StoryImageCommandPlayer : IStoryImageCommandPlayer, IDisposable
    {
        private readonly Func<RawImage> m_OutputProvider;
        private readonly ResourceModule m_ResourceModule;
        private readonly bool m_ClearOnStop;

        private AssetHandle m_CurrentAsset;
        private StoryCommandHandle m_CurrentHandle;
        private bool m_Disposed;

        /// <summary>
        /// 初始化 Story 图片命令播放器。
        /// </summary>
        /// <param name="output">图片输出控件。</param>
        /// <param name="resourceModule">资源模块。</param>
        /// <param name="clearOnStop">停止或取消时是否清空图片。</param>
        public StoryImageCommandPlayer(RawImage output, ResourceModule resourceModule, bool clearOnStop = true)
            : this(() => output, resourceModule, clearOnStop)
        {
        }

        /// <summary>
        /// 初始化 Story 图片命令播放器。
        /// </summary>
        /// <param name="outputProvider">图片输出控件提供器。</param>
        /// <param name="resourceModule">资源模块。</param>
        /// <param name="clearOnStop">停止或取消时是否清空图片。</param>
        public StoryImageCommandPlayer(Func<RawImage> outputProvider, ResourceModule resourceModule, bool clearOnStop = true)
        {
            m_OutputProvider = outputProvider ?? throw new ArgumentNullException(nameof(outputProvider));
            m_ResourceModule = resourceModule ?? throw new ArgumentNullException(nameof(resourceModule));
            m_ClearOnStop = clearOnStop;
        }

        /// <summary>
        /// 使用 App.Resource 创建播放器。
        /// </summary>
        /// <param name="output">图片输出控件。</param>
        /// <param name="clearOnStop">停止或取消时是否清空图片。</param>
        /// <returns>图片命令播放器。</returns>
        public static StoryImageCommandPlayer FromApp(RawImage output, bool clearOnStop = true)
        {
            return new StoryImageCommandPlayer(output, App.Resource, clearOnStop);
        }

        /// <inheritdoc />
        public IStoryCommandHandle ShowImage(StoryCommand command, StoryRuntimeContext context, string imagePath)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (string.IsNullOrWhiteSpace(imagePath))
            {
                throw new ArgumentException("Image path cannot be empty.", nameof(imagePath));
            }

            EnsureNotDisposed();
            var handle = new StoryCommandHandle(command);
            ShowImageAsync(handle, imagePath).Forget();
            return handle;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            StopCurrentHandle();
            ClearOutput();
            ReleaseCurrentAsset();
        }

        private async UniTaskVoid ShowImageAsync(StoryCommandHandle handle, string imagePath)
        {
            AssetHandle loadedAsset = null;
            var keepHandleActive = ShouldKeepHandleActiveAfterDisplay(handle.Command);
            SetCurrentHandle(handle);
            try
            {
                loadedAsset = await m_ResourceModule.LoadAssetAsync(imagePath);
                if (StoryMediaCommandUtility.IsTerminal(handle))
                {
                    ReleaseAsset(loadedAsset);
                    return;
                }

                if (!ReferenceEquals(m_CurrentHandle, handle))
                {
                    ReleaseAsset(loadedAsset);
                    handle.Stop();
                    return;
                }

                var texture = ResolveTexture(loadedAsset);
                if (texture == null)
                {
                    throw new GameException($"Story image asset is not a Texture or Sprite. command:{handle.Command.CommandId} path:{imagePath}");
                }

                ReleaseCurrentAsset();
                m_CurrentAsset = loadedAsset;
                loadedAsset = null;
                ApplyTexture(texture);
                if (keepHandleActive is false)
                {
                    handle.Complete(StoryMediaCommandUtility.GetCompletedOutcome(handle.Command));
                    UnbindHandle(handle);
                }
            }
            catch (Exception exception)
            {
                ReleaseAsset(loadedAsset);
                if (StoryMediaCommandUtility.IsTerminal(handle) is false)
                {
                    handle.Fail(exception);
                }
            }

            if (keepHandleActive && StoryMediaCommandUtility.IsTerminal(handle))
            {
                UnbindHandle(handle);
            }
        }

        private void SetCurrentHandle(StoryCommandHandle handle)
        {
            StopCurrentHandle();
            m_CurrentHandle = handle;
            handle.Canceled += OnCurrentHandleFinished;
            handle.Stopped += OnCurrentHandleFinished;
        }

        private void StopCurrentHandle()
        {
            if (m_CurrentHandle == null)
            {
                return;
            }

            var handle = m_CurrentHandle;
            UnbindHandle(handle);
            if (StoryMediaCommandUtility.IsTerminal(handle) is false)
            {
                handle.Stop();
            }
        }

        private void UnbindHandle(StoryCommandHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            handle.Canceled -= OnCurrentHandleFinished;
            handle.Stopped -= OnCurrentHandleFinished;
            if (ReferenceEquals(m_CurrentHandle, handle))
            {
                m_CurrentHandle = null;
            }
        }

        private void OnCurrentHandleFinished(IStoryCommandHandle handle)
        {
            if (ReferenceEquals(m_CurrentHandle, handle) is false)
            {
                return;
            }

            UnbindHandle(m_CurrentHandle);
            if (m_ClearOnStop)
            {
                ClearOutput();
                ReleaseCurrentAsset();
            }
        }

        private void ClearOutput()
        {
            var output = ResolveOutput();
            if (output != null)
            {
                output.texture = null;
                output.gameObject.SetActive(false);
            }
        }

        private void ApplyTexture(Texture texture)
        {
            var output = ResolveOutput();
            if (output == null)
            {
                throw new GameException("Story image output surface is missing.");
            }

            output.texture = texture;
            output.gameObject.SetActive(texture != null);
        }

        private void ReleaseCurrentAsset()
        {
            var asset = m_CurrentAsset;
            m_CurrentAsset = null;
            ReleaseAsset(asset);
        }

        private void ReleaseAsset(AssetHandle asset)
        {
            if (asset == null)
            {
                return;
            }

            try
            {
                m_ResourceModule.UnloadAsset(asset).Forget();
            }
            catch
            {
                asset.Release();
            }
        }

        private static Texture ResolveTexture(AssetHandle assetHandle)
        {
            if (assetHandle == null)
            {
                return null;
            }

            var sprite = assetHandle.GetAsset<Sprite>();
            if (sprite != null)
            {
                return sprite.texture;
            }

            return assetHandle.GetAsset<Texture>();
        }

        private static bool ShouldKeepHandleActiveAfterDisplay(StoryCommand command)
        {
            if (command == null)
            {
                return false;
            }

            if (command.WaitForCompletion)
            {
                return false;
            }

            return string.IsNullOrEmpty(StoryMediaCommandUtility.GetCompletedOutcome(command));
        }

        private RawImage ResolveOutput()
        {
            return m_OutputProvider();
        }

        private void EnsureNotDisposed()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(nameof(StoryImageCommandPlayer));
            }
        }
    }
}
