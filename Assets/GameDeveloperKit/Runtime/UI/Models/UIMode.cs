namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// UI窗口模式枚举，定义窗口打开时的行为模式。
    /// </summary>
    public enum UIMode
    {
        /// <summary>
        /// 普通模式，不影响其他窗口。
        /// </summary>
        Normal = 0,

        /// <summary>
        /// 隐藏其他模式，打开此窗口时隐藏所有其他窗口。
        /// </summary>
        HideOthers = 1,

        /// <summary>
        /// 隐藏下层模式，打开此窗口时隐藏所有下层窗口。
        /// </summary>
        HideLower = 2,

        /// <summary>
        /// 独占模式，打开此窗口时关闭所有其他窗口。
        /// </summary>
        Exclusive = 3
    }
}
