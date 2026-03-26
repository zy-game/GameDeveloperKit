using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 输入后端接口，提供底层输入系统的抽象
    /// </summary>
    public interface IInputBackend
    {
        /// <summary>
        /// 检查按键是否被按住
        /// </summary>
        /// <param name="key">按键码</param>
        /// <returns>如果按键被按住返回true，否则返回false</returns>
        bool GetKey(KeyCode key);

        /// <summary>
        /// 检查按键是否在当前帧被按下
        /// </summary>
        /// <param name="key">按键码</param>
        /// <returns>如果按键在当前帧被按下返回true，否则返回false</returns>
        bool GetKeyDown(KeyCode key);

        /// <summary>
        /// 检查按键是否在当前帧被释放
        /// </summary>
        /// <param name="key">按键码</param>
        /// <returns>如果按键在当前帧被释放返回true，否则返回false</returns>
        bool GetKeyUp(KeyCode key);

        /// <summary>
        /// 获取虚拟轴的值
        /// </summary>
        /// <param name="axisName">轴名称</param>
        /// <returns>轴的值（-1.0到1.0之间）</returns>
        float GetAxis(string axisName);

        /// <summary>
        /// 获取虚拟轴的原始值（无平滑处理）
        /// </summary>
        /// <param name="axisName">轴名称</param>
        /// <returns>轴的原始值（-1.0到1.0之间）</returns>
        float GetAxisRaw(string axisName);
    }
}
