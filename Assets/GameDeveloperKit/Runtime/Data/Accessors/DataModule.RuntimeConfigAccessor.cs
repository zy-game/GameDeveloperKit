namespace GameDeveloperKit.Runtime
{
    public sealed partial class DataModule
    {
        /// <summary>
        /// 运行时配置访问器，提供运行时配置的读写能力。
        /// </summary>
        public sealed class RuntimeConfigAccessor
        {
            private readonly DataModule _module;

            /// <summary>
            /// 初始化运行时配置访问器。
            /// </summary>
            /// <param name="module">所属的数据模块实例。</param>
            internal RuntimeConfigAccessor(DataModule module)
            {
                _module = module;
            }

            /// <summary>
            /// 获取运行时配置根路径。
            /// </summary>
            public string RootPath => _module.ConfigRootPath;

            /// <summary>
            /// 获取指定配置键对应的文件路径。
            /// </summary>
            /// <param name="key">配置键。</param>
            /// <param name="environment">环境标识；为空时返回默认路径。</param>
            /// <returns>配置文件路径。</returns>
            public string GetPath(string key, string environment = null)
            {
                return string.IsNullOrWhiteSpace(environment)
                    ? _module.GetConfigPath(key)
                    : _module.GetConfigPath(_module.GetEnvironmentScopedKey(key, environment));
            }

            /// <summary>
            /// 保存运行时配置为 JSON 文件。
            /// </summary>
            /// <typeparam name="T">配置值类型。</typeparam>
            /// <param name="key">配置键。</param>
            /// <param name="value">要保存的配置值。</param>
            /// <param name="cache">是否更新缓存。</param>
            /// <param name="prettyPrint">是否格式化输出 JSON。</param>
            /// <param name="environment">环境标识；为空时保存到默认配置。</param>
            public void SaveJson<T>(string key, T value, bool cache = true, bool prettyPrint = true, string environment = null)
            {
                if (string.IsNullOrWhiteSpace(environment))
                {
                    _module.SaveConfigJson(key, value, cache, prettyPrint);
                    return;
                }

                _module.SaveEnvironmentConfigJson(key, value, cache, prettyPrint, environment);
            }

            /// <summary>
            /// 加载指定运行时配置的 JSON 内容。
            /// </summary>
            /// <typeparam name="T">配置值类型。</typeparam>
            /// <param name="key">配置键。</param>
            /// <param name="cache">是否使用缓存。</param>
            /// <param name="defaultValue">未找到配置时返回的默认值。</param>
            /// <param name="environment">环境标识；为空时加载默认配置。</param>
            /// <returns>加载到的配置值。</returns>
            public T LoadJson<T>(string key, bool cache = true, T defaultValue = default, string environment = null)
            {
                return string.IsNullOrWhiteSpace(environment)
                    ? _module.LoadConfigWithHotUpdate(key, cache, defaultValue)
                    : _module.LoadEnvironmentConfigJson(key, cache, defaultValue, environment);
            }

            /// <summary>
            /// 保存热更新配置为 JSON 文件。
            /// </summary>
            /// <typeparam name="T">配置值类型。</typeparam>
            /// <param name="key">配置键。</param>
            /// <param name="value">要保存的配置值。</param>
            /// <param name="prettyPrint">是否格式化输出 JSON。</param>
            public void SaveHotUpdateJson<T>(string key, T value, bool prettyPrint = true)
            {
                _module.SaveHotUpdateConfigJson(key, value, prettyPrint);
            }
        }
    }
}
