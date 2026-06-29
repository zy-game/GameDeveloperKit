using UnityEngine;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// Story 播放视图宿主契约。由 UIModule 提供层级根节点，StoryPlayback
    /// 不参与窗口栈和 Back 栈，但通过此接口统一挂载点和生命周期通知。
    /// </summary>
    public interface IStoryPlaybackHost
    {
        /// <summary>
        /// 播放视图的挂载根节点。
        /// </summary>
        Transform GetPlaybackRoot();

        /// <summary>
        /// 播放开始时由宿主调用的通知。
        /// </summary>
        void OnPlaybackStarted();

        /// <summary>
        /// 播放停止时由宿主调用的通知。
        /// </summary>
        void OnPlaybackStopped();
    }
}
