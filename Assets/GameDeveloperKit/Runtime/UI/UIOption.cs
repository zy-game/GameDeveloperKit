using System;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// 定义 UI Option 类型。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class UIOption : Attribute
    {
        /// <summary>
        /// 初始化 UI Option。
        /// </summary>
        /// <param name="uiPath">ui Path 参数。</param>
        /// <param name="layer">layer 参数。</param>
        public UIOption(string uiPath, UILayer layer = UILayer.Background)
        {
            Path = uiPath;
            Layer = layer;
        }

        public string Path { get; }

        public UILayer Layer { get; }
    }
}
