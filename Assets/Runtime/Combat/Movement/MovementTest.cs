using UnityEngine;
using GameDeveloperKit.Combat;

namespace GameDeveloperKit.Combat.Tests
{
    /// <summary>
    /// 简单的移动测试脚本
    /// 用于验证 Character 移动集成
    /// </summary>
    public class MovementTest : MonoBehaviour
    {
        [Header("Character")]
        public Character Character;

        [Header("Debug Info")]
        public bool ShowDebugInfo = true;

        private void Start()
        {
            // 创建角色
            if (Character == null)
            {
                Character = new Character("TestCharacter");
            }

            // 启用移动能力
            Character.EnableMovement();

            // 创建 Motor
            var motor = gameObject.GetComponent<KinematicCharacterController.KinematicCharacterMotor>();
            if (motor == null)
            {
                motor = gameObject.AddComponent<KinematicCharacterController.KinematicCharacterMotor>();
                motor.MaxStableSlopeAngle = 45f;
                motor.MaxStepHeight = 0.3f;
            }

            // 直接设置 Character 的 Unity 引用
            Character.Motor = motor;
            Character.Transform = transform;
            
            // 将 Character 设置为 Motor 的控制器
            motor.CharacterController = Character;
            
            // 设置输入
            Character.InputProvider = new UnityInputProvider();

            Debug.Log($"[MovementTest] Movement enabled for {Character.Name}");
            Debug.Log($"[MovementTest] MoveSpeed: {Character.MovementAttributes.GetMoveSpeed()}");
            Debug.Log($"[MovementTest] JumpHeight: {Character.MovementAttributes.GetJumpHeight()}");
            Debug.Log($"[MovementTest] Gravity: {Character.MovementAttributes.GetGravity()}");
        }

        private void Update()
        {
            if (Character == null)
                return;

            // 更新角色（技能、效果等）
            Character.Tick(Time.deltaTime);
        }

        private void OnGUI()
        {
            if (ShowDebugInfo && Character != null)
            {
                GUIStyle style = new GUIStyle();
                style.fontSize = 16;
                style.normal.textColor = Color.white;

                string info = $"Is Grounded: {Character.IsGrounded}\n" +
                             $"Position: {Character.CachedPosition}\n" +
                             $"Velocity: {Character.GetVelocity()}\n" +
                             $"Speed: {Character.GetVelocity().magnitude:F2} m/s\n\n" +
                             $"Controls:\n" +
                             $"WASD - Move\n" +
                             $"Space - Jump";

                GUI.Label(new Rect(10, 10, 300, 200), info, style);
            }
        }

        // 测试方法：应用减速效果
        [ContextMenu("Test: Apply Slow (50%)")]
        public void TestApplySlow()
        {
            if (Character != null && Character.MovementAttributes != null)
            {
                var modifier = AttributeModifier.Create(
                    MovementAttributeSet.MoveSpeed,
                    ModifierOp.Multiply,
                    0.5f,
                    0,
                    this
                );
                Character.MovementAttributes.AddModifier(modifier);
                Debug.Log("[MovementTest] Applied 50% slow");
            }
        }

        // 测试方法：移除减速效果
        [ContextMenu("Test: Remove Slow")]
        public void TestRemoveSlow()
        {
            if (Character != null && Character.MovementAttributes != null)
            {
                Character.MovementAttributes.RemoveModifiersFromSource(this);
                Debug.Log("[MovementTest] Removed slow");
            }
        }

        // 测试方法：设置外部速度（模拟冲刺）
        [ContextMenu("Test: External Velocity Forward")]
        public void TestExternalVelocity()
        {
            if (Character != null)
            {
                Character.SetExternalVelocity(transform.forward * 15f);
                Debug.Log("[MovementTest] Set external velocity");
                
                // 0.5秒后清除
                Invoke(nameof(ClearExternalVelocity), 0.5f);
            }
        }

        private void ClearExternalVelocity()
        {
            if (Character != null)
            {
                Character.ClearExternalVelocity();
                Debug.Log("[MovementTest] Cleared external velocity");
            }
        }

        // 测试方法：瞬移
        [ContextMenu("Test: Teleport Forward 5m")]
        public void TestTeleport()
        {
            if (Character != null)
            {
                Vector3 targetPos = transform.position + transform.forward * 5f;
                Character.Teleport(targetPos);
                Debug.Log("[MovementTest] Teleported forward 5m");
            }
        }
    }
}
