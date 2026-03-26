using System.IO;

namespace GameDeveloperKit.Runtime
{
    public sealed partial class DataModule
    {
        /// <summary>
        /// 配置表访问器，提供配置表的查询与加载能力。
        /// </summary>
        public sealed class ConfigTableAccessor
        {
            private readonly DataModule _module;

            /// <summary>
            /// 初始化配置表访问器。
            /// </summary>
            /// <param name="module">所属的数据模块实例。</param>
            internal ConfigTableAccessor(DataModule module)
            {
                _module = module;
            }

            /// <summary>
            /// 获取配置表根路径。
            /// </summary>
            public string RootPath => _module.ConfigRootPath;

            /// <summary>
            /// 获取指定配置表键对应的文件路径。
            /// </summary>
            /// <param name="key">配置表键。</param>
            /// <returns>配置表文件路径。</returns>
            public string GetPath(string key)
            {
                return _module.GetConfigPath(key);
            }

            /// <summary>
            /// 检查指定配置表是否存在。
            /// </summary>
            /// <param name="key">配置表键。</param>
            /// <returns>存在返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
            public bool Has(string key)
            {
                return File.Exists(GetPath(key)) || File.Exists(_module.GetHotUpdateConfigPath(key));
            }

            /// <summary>
            /// 加载指定配置表的 JSON 内容。
            /// </summary>
            /// <typeparam name="T">配置值类型。</typeparam>
            /// <param name="key">配置表键。</param>
            /// <param name="cache">是否使用缓存。</param>
            /// <param name="defaultValue">未找到配置时返回的默认值。</param>
            /// <returns>加载到的配置值。</returns>
            public T LoadJson<T>(string key, bool cache = true, T defaultValue = default)
            {
                return _module.LoadConfigWithHotUpdate(key, cache, defaultValue);
            }
        }
    }
}
