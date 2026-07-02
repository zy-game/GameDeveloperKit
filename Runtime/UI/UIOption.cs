using System;

namespace GameDeveloperKit.UI
{
    public enum UICacheStrategy
    {
        Time,
        Heat,
        None,
    }

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
        public bool CacheEnabled { get; set; } = true;
        public UICacheStrategy CacheStrategy { get; set; } = UICacheStrategy.Time;
        public float CacheTimeToLive { get; set; } = 30f;
        public int CacheCapacity { get; set; } = 1;
    }
}
