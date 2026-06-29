using UnityEngine;

namespace GameDeveloperKit.Input
{
    /// <summary>
    /// Unity legacy Input 输入源。
    /// </summary>
    public sealed class UnityInputSource : IInputSource
    {
        /// <summary>
        /// 获取键盘按键是否按住。
        /// </summary>
        /// <param name="key">键盘按键。</param>
        /// <returns>按住时返回 true。</returns>
        public bool GetKey(KeyCode key)
        {
            return UnityEngine.Input.GetKey(key);
        }

        /// <summary>
        /// 获取鼠标按钮是否按住。
        /// </summary>
        /// <param name="button">鼠标按钮索引。</param>
        /// <returns>按住时返回 true。</returns>
        public bool GetMouseButton(int button)
        {
            return UnityEngine.Input.GetMouseButton(button);
        }

        /// <summary>
        /// 获取输入轴原始值。
        /// </summary>
        /// <param name="axisName">输入轴名称。</param>
        /// <returns>输入轴原始值。</returns>
        public float GetAxisRaw(string axisName)
        {
            return UnityEngine.Input.GetAxisRaw(axisName);
        }
    }
}
