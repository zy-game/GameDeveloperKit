using System;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// UI表单配置特性，用于标记UI表单的显示配置
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class UIFormAttribute : Attribute
    {
        /// <summary>
        /// UI名称（对应Prefab名称，不带扩展名）
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// UI层级
        /// </summary>
        public EUILayer Layer { get; }

        /// <summary>
        /// UI显示模式
        /// </summary>
        public EUIMode Mode { get; }

        /// <summary>
        /// 是否添加到导航栈
        /// </summary>
        public bool ToStack { get; }

        /// <summary>
        /// 排序顺序
        /// </summary>
        public int SortingOrder { get; }
        
        /// <summary>
        /// 创建UI表单配置特性
        /// </summary>
        /// <param name="name">UI名称（对应Prefab名称，不带扩展名）</param>
        /// <param name="layer">UI层级</param>
        /// <param name="mode">UI显示模式</param>
        /// <param name="toStack">是否添加到导航栈</param>
        public UIFormAttribute(string name, EUILayer layer = EUILayer.Window, EUIMode mode = EUIMode.Normal, bool toStack = true,int sortingOrder=0)
        {
            Name = name;
            Layer = layer;
            Mode = mode;
            ToStack = toStack;
            SortingOrder = sortingOrder;
        }
    }
}