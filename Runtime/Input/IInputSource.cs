using UnityEngine;

namespace GameDeveloperKit.Input
{
    /// <summary>
    /// 输入源接口。
    /// </summary>
    public interface IInputSource
    {
        /// <summary>
        /// 获取键盘按键是否按住。
        /// </summary>
        /// <param name="key">键盘按键。</param>
        /// <returns>按住时返回 true。</returns>
        bool GetKey(KeyCode key);

        /// <summary>
        /// 获取鼠标按钮是否按住。
        /// </summary>
        /// <param name="button">鼠标按钮索引。</param>
        /// <returns>按住时返回 true。</returns>
        bool GetMouseButton(int button);

        /// <summary>
        /// 获取输入轴原始值。
        /// </summary>
        /// <param name="axisName">输入轴名称。</param>
        /// <returns>输入轴原始值。</returns>
        float GetAxisRaw(string axisName);
    }
}
