namespace GameDeveloperKit.Input
{
    /// <summary>
    /// 输入绑定类型。
    /// </summary>
    public enum InputBindingKind : byte
    {
        /// <summary>
        /// 键盘按键。
        /// </summary>
        Key = 0,

        /// <summary>
        /// 鼠标按钮。
        /// </summary>
        MouseButton = 1,

        /// <summary>
        /// 输入轴。
        /// </summary>
        Axis = 2,
    }
}
