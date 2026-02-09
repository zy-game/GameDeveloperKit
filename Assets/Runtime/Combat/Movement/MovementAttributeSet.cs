namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 移动属性集
    /// 控制角色的移动参数，所有属性支持通过修改器动态调整
    /// </summary>
    public class MovementAttributeSet : AttributeSet
    {
        public const string MoveSpeed = "MoveSpeed";
        public const string Acceleration = "Acceleration";
        public const string JumpHeight = "JumpHeight";
        public const string Gravity = "Gravity";
        public const string Mass = "Mass";

        public MovementAttributeSet()
        {
            // 基础移动
            DefineAttribute(MoveSpeed, 5.0f, 0f);           // 默认: 5.0 m/s
            DefineAttribute(Acceleration, 10.0f, 0f);       // 默认: 10.0（用作插值锐度系数）

            // 跳跃和重力
            DefineAttribute(JumpHeight, 1.5f, 0f);          // 默认: 1.5 m
            DefineAttribute(Gravity, -20.0f);               // 默认: -20.0 m/s²

            // 物理参数（Phase 3 才使用）
            DefineAttribute(Mass, 70.0f, 0.1f);             // 默认: 70.0 kg（用于推挤，MVP 不使用）
        }

        /// <summary>
        /// 获取当前移动速度
        /// </summary>
        public float GetMoveSpeed() => GetCurrentValue(MoveSpeed);

        /// <summary>
        /// 获取当前加速度（插值锐度）
        /// </summary>
        public float GetAcceleration() => GetCurrentValue(Acceleration);

        /// <summary>
        /// 获取当前跳跃高度
        /// </summary>
        public float GetJumpHeight() => GetCurrentValue(JumpHeight);

        /// <summary>
        /// 获取当前重力加速度
        /// </summary>
        public float GetGravity() => GetCurrentValue(Gravity);

        /// <summary>
        /// 获取当前质量（Phase 3 使用）
        /// </summary>
        public float GetMass() => GetCurrentValue(Mass);
    }
}
