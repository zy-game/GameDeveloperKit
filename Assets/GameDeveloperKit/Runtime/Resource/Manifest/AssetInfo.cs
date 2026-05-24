using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源信息
    /// </summary>
    public sealed class AssetInfo
    {
        /// <summary>
        /// 资源地址
        /// </summary>
        public string Location;

        /// <summary>
        /// 资源类型
        /// </summary>
        public string TypeName;

        /// <summary>
        /// 资源标签
        /// </summary>
        public List<string> Labels;
    }
}
