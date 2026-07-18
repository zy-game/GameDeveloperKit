using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情交互通道。
    /// </summary>
    public interface IInteractionChannel : IDisposable
    {
        /// <summary>
        /// 剧情预热前唤醒交互通道。
        /// </summary>
        /// <param name="context">交互上下文。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>唤醒任务。</returns>
        UniTask OnAwake(InteractionContext context, CancellationToken cancellationToken);

        /// <summary>
        /// 剧情预热完成后通知交互通道。
        /// </summary>
        /// <param name="context">交互上下文。</param>
        void OnStoryStarted(InteractionContext context);

        /// <summary>
        /// 章节变化时通知交互通道。
        /// </summary>
        /// <param name="context">章节交互上下文。</param>
        void OnChapterChanged(ChapterInteractionContext context);

        /// <summary>
        /// 帧变化时通知交互通道。
        /// </summary>
        /// <param name="frame">当前帧。</param>
        void OnFrameChanged(StoryFrame frame);

        /// <summary>
        /// 获取播放和输入所需的 UI surface。
        /// </summary>
        /// <param name="request">交互请求。</param>
        /// <returns>播放 surface。</returns>
        PlaybackSurfaceView GetPlaybackSurfaceView(InteractionRequest request);

        /// <summary>
        /// 每帧更新交互通道。
        /// </summary>
        /// <param name="deltaTime">时间增量。</param>
        void Tick(float deltaTime);

        /// <summary>
        /// 剧情停止时通知交互通道。
        /// </summary>
        void OnStoryStopped();
    }
}
