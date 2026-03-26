using System;

namespace GameDeveloperKit.Runtime
{
    [Serializable]
    /// <summary>
    /// 定义热更新配置的边界规则。
    /// </summary>
    public sealed class HotUpdateConfigBoundary
    {
        /// <summary>
        /// 配置项键名。
        /// </summary>
        public string Key;

        /// <summary>
        /// 配置项所属的数据层级。
        /// </summary>
        public DataContentLayer Layer;

        /// <summary>
        /// 是否允许运行时覆盖该配置。
        /// </summary>
        public bool AllowRuntimeOverride;

        /// <summary>
        /// 是否优先使用热更新来源。
        /// </summary>
        public bool PreferHotUpdateSource = true;
    }
}
