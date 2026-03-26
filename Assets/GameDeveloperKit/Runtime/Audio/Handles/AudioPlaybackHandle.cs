using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 音频播放句柄，用于控制音频的停止和释放
    /// </summary>
    public sealed class AudioPlaybackHandle : IDisposable
    {
        private readonly Action _stopAction;
        private bool _released;

        /// <summary>
        /// 初始化音频播放句柄
        /// </summary>
        /// <param name="group">音频组</param>
        /// <param name="location">资源位置</param>
        /// <param name="packageName">资源包名称</param>
        /// <param name="stopAction">停止动作</param>
        internal AudioPlaybackHandle(AudioGroup group, ResourceLocation location, string packageName, Action stopAction)
        {
            Group = group;
            Location = location?.Clone();
            PackageName = packageName;
            _stopAction = stopAction;
        }

        /// <summary>
        /// 获取音频组
        /// </summary>
        public AudioGroup Group { get; }

        /// <summary>
        /// 获取资源位置
        /// </summary>
        public ResourceLocation Location { get; }

        /// <summary>
        /// 获取资源包名称
        /// </summary>
        public string PackageName { get; }

        /// <summary>
        /// 获取是否已释放
        /// </summary>
        public bool IsReleased => _released;

        /// <summary>
        /// 停止音频播放
        /// </summary>
        public void Stop()
        {
            if (_released)
            {
                return;
            }

            _stopAction?.Invoke();
            _released = true;
        }

        /// <summary>
        /// 释放音频播放句柄
        /// </summary>
        public void Dispose()
        {
            Stop();
        }
    }
}
