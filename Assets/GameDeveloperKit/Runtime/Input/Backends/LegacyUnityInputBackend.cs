using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 旧版Unity输入后端，基于Unity传统输入系统实现
    /// </summary>
    public sealed class LegacyUnityInputBackend : IInputBackend
    {
        /// <summary>
        /// 单例实例
        /// </summary>
        public static readonly LegacyUnityInputBackend Instance = new();

        private LegacyUnityInputBackend()
        {
        }

        /// <summary>
        /// 检查按键是否被按住
        /// </summary>
        /// <param name="key">按键码</param>
        /// <returns>如果按键被按住返回true，否则返回false</returns>
        public bool GetKey(KeyCode key)
        {
            return Input.GetKey(key);
        }

        /// <summary>
        /// 检查按键是否在当前帧被按下
        /// </summary>
        /// <param name="key">按键码</param>
        /// <returns>如果按键在当前帧被按下返回true，否则返回false</returns>
        public bool GetKeyDown(KeyCode key)
        {
            return Input.GetKeyDown(key);
        }

        /// <summary>
        /// 检查按键是否在当前帧被释放
        /// </summary>
        /// <param name="key">按键码</param>
        /// <returns>如果按键在当前帧被释放返回true，否则返回false</returns>
        public bool GetKeyUp(KeyCode key)
        {
            return Input.GetKeyUp(key);
        }

        /// <summary>
        /// 获取虚拟轴的值
        /// </summary>
        /// <param name="axisName">轴名称</param>
        /// <returns>轴的值（-1.0到1.0之间）</returns>
        public float GetAxis(string axisName)
        {
            return Input.GetAxis(axisName);
        }

        /// <summary>
        /// 获取虚拟轴的原始值（无平滑处理）
        /// </summary>
        /// <param name="axisName">轴名称</param>
        /// <returns>轴的原始值（-1.0到1.0之间）</returns>
        public float GetAxisRaw(string axisName)
        {
            return Input.GetAxisRaw(axisName);
        }
    }
}
