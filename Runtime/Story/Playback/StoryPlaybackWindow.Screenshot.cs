using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.Story.Playback
{
    /// <summary>
    /// StoryPlaybackWindow 的截图能力 partial。
    /// </summary>
    /// <remarks>
    /// 职责：扫描 prefab 上的 "ScreenshotButton" 节点（可选，缺失则不绑定），点击时触发
    /// <see cref="ScreenshotRequested"/> 事件；提供 <see cref="CaptureCurrentFrameAsync"/> 抓帧原语。
    /// 业务侧（播放流程）订阅事件组合"暂停 → 抓帧 → 开预览 → 恢复"完整流程，GDK 不依赖业务侧 UI。
    /// </remarks>
    public partial class StoryPlaybackWindow
    {
        /// <summary>prefab 上截图按钮的节点名。缺失时该按钮不被绑定（向后兼容）。</summary>
        private const string ScreenshotButtonName = "ScreenshotButton";

        private Button m_ScreenshotButton;

        /// <summary>
        /// 截图按钮被点击时触发。业务侧订阅后组合预览/保存/恢复播放流程。
        /// </summary>
        public event Action ScreenshotRequested;

        /// <summary>
        /// 尝试绑定 prefab 上的截图按钮。无该节点时静默跳过（向后兼容旧 prefab）。
        /// </summary>
        private void TryBindScreenshotButton()
        {
            if (Document == null)
            {
                return;
            }

            if (Document.TryGetComponent(ScreenshotButtonName, out Button button) && button != null)
            {
                m_ScreenshotButton = button;
                m_ScreenshotButton.onClick.AddListener(OnScreenshotButtonClicked);
            }
        }

        /// <summary>
        /// 解绑截图按钮。
        /// </summary>
        private void UnbindScreenshotButton()
        {
            if (m_ScreenshotButton != null)
            {
                m_ScreenshotButton.onClick.RemoveListener(OnScreenshotButtonClicked);
                m_ScreenshotButton = null;
            }
        }

        private void OnScreenshotButtonClicked()
        {
            ScreenshotRequested?.Invoke();
        }

        /// <summary>
        /// 抓取当前视频帧。
        /// </summary>
        /// <remarks>
        /// 内部等待本帧渲染结束（<see cref="UniTask.WaitForEndOfFrame"/>），从 <see cref="VideoPlayableHandle.Texture"/>
        /// 读取像素并编码为 JPG。调用方负责在抓帧前暂停播放、抓帧后恢复（暂停/恢复不在此原语内）。
        /// 返回的 <see cref="Texture2D"/> 由调用方持有并负责销毁。
        /// </remarks>
        /// <exception cref="GameException">视频未就绪（handle 为空或无首帧）。</exception>
        public async UniTask<(Texture2D frame, byte[] jpg)> CaptureCurrentFrameAsync()
        {
            var handle = Playback;
            if (handle == null || !handle.HasFirstFrame)
            {
                throw new GameException("Video frame is not ready for screenshot capture.");
            }

            var source = handle.Texture;
            if (source == null)
            {
                throw new GameException("Video frame texture is null.");
            }

            var width = source.width;
            var height = source.height;
            if (width <= 0 || height <= 0)
            {
                throw new GameException($"Video frame has invalid dimensions: {width}x{height}.");
            }

            await UniTask.WaitForEndOfFrame();

            var previousActive = RenderTexture.active;
            RenderTexture sourceRt = null;
            RenderTexture tempRt = null;
            try
            {
                // AVPro 在部分平台返回外部 Texture2D（非 RenderTexture），此时 RenderTexture.active
                // 不会指向它，直接 ReadPixels 会读到错误目标。统一先 Blit 到临时 RenderTexture 再读取。
                sourceRt = source as RenderTexture;
                if (sourceRt == null)
                {
                    tempRt = RenderTexture.GetTemporary(
                        width,
                        height,
                        0,
                        RenderTextureFormat.ARGB32,
                        RenderTextureReadWrite.Default);
                    Graphics.Blit(source, tempRt);
                    RenderTexture.active = tempRt;
                }
                else
                {
                    RenderTexture.active = sourceRt;
                }

                var frame = new Texture2D(width, height, TextureFormat.RGB24, mipChain: false);
                frame.ReadPixels(new Rect(0, 0, width, height), destX: 0, destY: 0);
                frame.Apply();
                var jpg = frame.EncodeToJPG();
                return (frame, jpg);
            }
            finally
            {
                if (tempRt != null)
                {
                    RenderTexture.active = previousActive;
                    RenderTexture.ReleaseTemporary(tempRt);
                }
                else if (sourceRt != null)
                {
                    RenderTexture.active = previousActive;
                }
            }
        }
    }
}
