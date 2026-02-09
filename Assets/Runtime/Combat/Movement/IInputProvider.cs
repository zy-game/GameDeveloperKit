using UnityEngine;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 输入提供者接口
    /// 负责处理输入并直接调用 Character 的方法
    /// 业务层可以实现此接口来自定义输入处理逻辑
    /// </summary>
    public interface IInputProvider
    {
        /// <summary>
        /// 处理输入（每帧调用）
        /// 实现者应在此方法中读取输入并调用 character 的相应方法
        /// </summary>
        /// <param name="character">角色</param>
        /// <param name="deltaTime">帧间隔时间</param>
        void ProcessInput(Character character, float deltaTime);
    }

    /// <summary>
    /// Unity 默认输入提供者（用于测试）
    /// 业务层应创建自己的实现来自定义输入处理
    /// </summary>
    public class UnityInputProvider : IInputProvider
    {
        public void ProcessInput(Character character, float deltaTime)
        {
            if (character == null) return;

            // 移动输入
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            character.SetMoveInput(new Vector3(horizontal, 0, vertical));

            // 跳跃输入
            if (Input.GetButtonDown("Jump"))
            {
                character.Jump();
            }
        }
    }
}
