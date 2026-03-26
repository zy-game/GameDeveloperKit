using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// UI窗口特性，用于标记UI窗口类并配置窗口属性。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class UIWindowAttribute : Attribute
    {
        /// <summary>
        /// 初始化UI窗口特性的新实例，使用默认配置。
        /// </summary>
        public UIWindowAttribute()
            : this(null)
        {
        }

        /// <summary>
        /// 初始化UI窗口特性的新实例，使用指定的层配置。
        /// </summary>
        /// <param name="layer">UI层。</param>
        /// <param name="mode">UI模式。</param>
        /// <param name="toStack">是否加入堆栈。</param>
        /// <param name="sortingOrder">排序顺序。</param>
        /// <param name="cacheOnClose">关闭时是否缓存。</param>
        /// <param name="openStrategy">打开策略。</param>
        public UIWindowAttribute(
            UILayer layer,
            UIMode mode = UIMode.Normal,
            bool toStack = true,
            int sortingOrder = 0,
            bool cacheOnClose = false,
            UIOpenStrategy openStrategy = UIOpenStrategy.RefreshExisting)
            : this(null, layer, mode, toStack, sortingOrder, cacheOnClose, openStrategy)
        {
        }

        /// <summary>
        /// 初始化UI窗口特性的新实例，使用指定的资源和配置。
        /// </summary>
        /// <param name="assetPath">资源路径。</param>
        /// <param name="layer">UI层。</param>
        /// <param name="mode">UI模式。</param>
        /// <param name="toStack">是否加入堆栈。</param>
        /// <param name="sortingOrder">排序顺序。</param>
        /// <param name="cacheOnClose">关闭时是否缓存。</param>
        /// <param name="openStrategy">打开策略。</param>
        public UIWindowAttribute(
            string assetPath,
            UILayer layer = UILayer.Window,
            UIMode mode = UIMode.Normal,
            bool toStack = true,
            int sortingOrder = 0,
            bool cacheOnClose = false,
            UIOpenStrategy openStrategy = UIOpenStrategy.RefreshExisting)
        {
            AssetPath = assetPath;
            Layer = layer;
            Mode = mode;
            ToStack = toStack;
            SortingOrder = sortingOrder;
            CacheOnClose = cacheOnClose;
            OpenStrategy = openStrategy;
        }

        /// <summary>
        /// 获取资源路径。
        /// </summary>
        public string AssetPath { get; }

        /// <summary>
        /// 获取UI层。
        /// </summary>
        public UILayer Layer { get; }

        /// <summary>
        /// 获取UI模式。
        /// </summary>
        public UIMode Mode { get; }

        /// <summary>
        /// 获取是否加入堆栈。
        /// </summary>
        public bool ToStack { get; }

        /// <summary>
        /// 获取排序顺序。
        /// </summary>
        public int SortingOrder { get; }

        /// <summary>
        /// 获取关闭时是否缓存。
        /// </summary>
        public bool CacheOnClose { get; }

        /// <summary>
        /// 获取打开策略。
        /// </summary>
        public UIOpenStrategy OpenStrategy { get; }
    }
}
