namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 平台能力枚举，定义平台支持的各种硬件和功能特性。
    /// </summary>
    public enum PlatformCapability
    {
        /// <summary>
        /// 触摸屏支持。
        /// </summary>
        Touch = 0,

        /// <summary>
        /// 键盘输入支持。
        /// </summary>
        Keyboard = 1,

        /// <summary>
        /// 鼠标输入支持。
        /// </summary>
        Mouse = 2,

        /// <summary>
        /// 麦克风支持。
        /// </summary>
        Microphone = 3,

        /// <summary>
        /// 摄像头支持。
        /// </summary>
        Camera = 4,

        /// <summary>
        /// 网络可达性检测。
        /// </summary>
        NetworkReachability = 5,

        /// <summary>
        /// 光标锁定支持。
        /// </summary>
        CursorLock = 6
    }
}
