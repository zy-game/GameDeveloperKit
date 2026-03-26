namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// UI层枚举，定义UI窗口的显示层级顺序。
    /// </summary>
    public enum UILayer
    {
        /// <summary>
        /// 背景层，用于显示背景元素。
        /// </summary>
        Background = 0,

        /// <summary>
        /// 普通层，用于常规UI元素。
        /// </summary>
        Normal = 1,

        /// <summary>
        /// 窗口层，用于主要窗口。
        /// </summary>
        Window = 2,

        /// <summary>
        /// 弹出层，用于弹出窗口和对话框。
        /// </summary>
        Popup = 3,

        /// <summary>
        /// 覆盖层，用于顶层覆盖元素。
        /// </summary>
        Overlay = 4,

        /// <summary>
        /// 系统层，用于系统级UI元素。
        /// </summary>
        System = 5
    }
}
