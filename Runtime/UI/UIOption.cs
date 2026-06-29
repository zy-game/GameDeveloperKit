using System;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// UI 窗口配置特性，声明 prefab 路径和渲染层级。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class UIOption : Attribute
    {
        public UIOption(string uiPath, int layerOrder = 0)
        {
            Path = uiPath;
            LayerOrder = layerOrder;
        }

        public string Path { get; }
        public int LayerOrder { get; }
    }
}
