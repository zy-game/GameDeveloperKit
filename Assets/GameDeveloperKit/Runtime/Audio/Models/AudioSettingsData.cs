using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 音频设置数据模型
    /// </summary>
    [Serializable]
    public sealed class AudioSettingsData
    {
        /// <summary>
        /// 主音量（0.0-1.0）
        /// </summary>
        public float MasterVolume = 1f;

        /// <summary>
        /// 背景音乐音量（0.0-1.0）
        /// </summary>
        public float BgmVolume = 1f;

        /// <summary>
        /// 音效音量（0.0-1.0）
        /// </summary>
        public float SfxVolume = 1f;

        /// <summary>
        /// 语音音量（0.0-1.0）
        /// </summary>
        public float VoiceVolume = 1f;

        /// <summary>
        /// 场景切换时是否停止背景音乐
        /// </summary>
        public bool StopBgmOnSceneChange;
    }
}
