namespace GameDeveloperKit.Input
{
    /// <summary>
    /// 输入动作状态。
    /// </summary>
    public struct InputActionState
    {
        /// <summary>
        /// 当前是否按住。
        /// </summary>
        public bool IsPressed { get; private set; }

        /// <summary>
        /// 当前帧是否刚按下。
        /// </summary>
        public bool WasPressedThisFrame { get; private set; }

        /// <summary>
        /// 当前帧是否刚松开。
        /// </summary>
        public bool WasReleasedThisFrame { get; private set; }

        /// <summary>
        /// 当前输入值。
        /// </summary>
        public float Value { get; private set; }

        /// <summary>
        /// 设置当前帧状态。
        /// </summary>
        /// <param name="isPressed">是否按住。</param>
        /// <param name="value">输入值。</param>
        internal void Set(bool isPressed, float value)
        {
            WasPressedThisFrame = isPressed && !IsPressed;
            WasReleasedThisFrame = !isPressed && IsPressed;
            IsPressed = isPressed;
            Value = value;
        }

        /// <summary>
        /// 清空当前状态并保留释放边沿。
        /// </summary>
        internal void Clear()
        {
            Set(false, 0f);
        }

        /// <summary>
        /// 重置所有状态。
        /// </summary>
        internal void Reset()
        {
            IsPressed = false;
            WasPressedThisFrame = false;
            WasReleasedThisFrame = false;
            Value = 0f;
        }
    }
}
