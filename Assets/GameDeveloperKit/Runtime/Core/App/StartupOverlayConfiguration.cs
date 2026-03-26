using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 表示启动覆盖层的显示配置。
    /// </summary>
    [Serializable]
    public sealed class StartupOverlayConfiguration
    {
        /// <summary>
        /// 是否显示启动覆盖层。
        /// </summary>
        public bool ShowOverlay = true;

        /// <summary>
        /// 是否在启动完成后隐藏覆盖层。
        /// </summary>
        public bool HideOverlayOnComplete = true;

        /// <summary>
        /// 启动覆盖层的标题文本。
        /// </summary>
        public string OverlayTitle = "GameDeveloperKit Startup";
    }
}
