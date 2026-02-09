using UnityEngine;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 冲刺数据
    /// </summary>
    public class DashData
    {
        /// <summary>
        /// 冲刺方向
        /// </summary>
        public Vector3 Direction;

        /// <summary>
        /// 冲刺距离
        /// </summary>
        public float Distance;

        /// <summary>
        /// 冲刺速度
        /// </summary>
        public float Speed;

        /// <summary>
        /// 已移动距离
        /// </summary>
        public float TraveledDistance;

        /// <summary>
        /// 是否激活
        /// </summary>
        public bool IsActive;

        /// <summary>
        /// 冷却时间
        /// </summary>
        public float CooldownDuration;

        /// <summary>
        /// 剩余冷却时间
        /// </summary>
        public float RemainingCooldown;

        public DashData()
        {
            Direction = Vector3.zero;
            Distance = 0f;
            Speed = 0f;
            TraveledDistance = 0f;
            IsActive = false;
            CooldownDuration = 1f;
            RemainingCooldown = 0f;
        }

        /// <summary>
        /// 是否可以冲刺
        /// </summary>
        public bool CanDash => !IsActive && RemainingCooldown <= 0f;

        /// <summary>
        /// 更新冷却
        /// </summary>
        public void UpdateCooldown(float deltaTime)
        {
            if (RemainingCooldown > 0f)
            {
                RemainingCooldown -= deltaTime;
                if (RemainingCooldown < 0f)
                    RemainingCooldown = 0f;
            }
        }
    }

    /// <summary>
    /// 击退数据
    /// </summary>
    public class KnockbackData
    {
        /// <summary>
        /// 击退方向
        /// </summary>
        public Vector3 Direction;

        /// <summary>
        /// 击退速度
        /// </summary>
        public float Speed;

        /// <summary>
        /// 击退持续时间
        /// </summary>
        public float Duration;

        /// <summary>
        /// 已持续时间
        /// </summary>
        public float ElapsedTime;

        /// <summary>
        /// 是否激活
        /// </summary>
        public bool IsActive;

        public KnockbackData()
        {
            Direction = Vector3.zero;
            Speed = 0f;
            Duration = 0f;
            ElapsedTime = 0f;
            IsActive = false;
        }

        /// <summary>
        /// 更新击退
        /// </summary>
        public void Update(float deltaTime)
        {
            if (IsActive)
            {
                ElapsedTime += deltaTime;
                if (ElapsedTime >= Duration)
                {
                    IsActive = false;
                }
            }
        }

        /// <summary>
        /// 获取当前击退速度（带衰减）
        /// </summary>
        public Vector3 GetCurrentVelocity()
        {
            if (!IsActive)
                return Vector3.zero;

            // 线性衰减
            float t = ElapsedTime / Duration;
            float currentSpeed = Speed * (1f - t);
            return Direction * currentSpeed;
        }
    }

    /// <summary>
    /// 位移数据（闪现、冲锋等）
    /// </summary>
    public class DisplacementData
    {
        /// <summary>
        /// 位移类型
        /// </summary>
        public enum DisplacementType
        {
            /// <summary>
            /// 瞬移（无碰撞检测）
            /// </summary>
            Blink,

            /// <summary>
            /// 冲锋（有碰撞检测）
            /// </summary>
            Charge
        }

        /// <summary>
        /// 类型
        /// </summary>
        public DisplacementType Type;

        /// <summary>
        /// 目标位置
        /// </summary>
        public Vector3 TargetPosition;

        /// <summary>
        /// 移动速度（仅用于 Charge）
        /// </summary>
        public float Speed;

        /// <summary>
        /// 是否激活
        /// </summary>
        public bool IsActive;

        /// <summary>
        /// 是否完成
        /// </summary>
        public bool IsCompleted;

        public DisplacementData()
        {
            Type = DisplacementType.Blink;
            TargetPosition = Vector3.zero;
            Speed = 0f;
            IsActive = false;
            IsCompleted = false;
        }
    }
}
