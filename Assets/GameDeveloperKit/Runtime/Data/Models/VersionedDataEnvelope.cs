using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 版本化数据信封，用于封装带有版本信息的数据。
    /// </summary>
    [Serializable]
    public sealed class VersionedDataEnvelope
    {
        /// <summary>
        /// 获取或设置数据版本号。
        /// </summary>
        public int Version;

        /// <summary>
        /// 获取或设置环境名称。
        /// </summary>
        public string Environment;

        /// <summary>
        /// 获取或设置数据负载的 JSON 字符串。
        /// </summary>
        public string PayloadJson;
    }
}
