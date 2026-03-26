using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 数据模块的存档访问扩展定义。
    /// </summary>
    public sealed partial class DataModule
    {
        /// <summary>
        /// 存档数据访问器，提供便捷的存档数据访问接口。
        /// </summary>
        public sealed class SaveDataAccessor
        {
            private readonly DataModule _module;

            /// <summary>
            /// 初始化 SaveDataAccessor 的新实例。
            /// </summary>
            /// <param name="module">所属的数据模块。</param>
            internal SaveDataAccessor(DataModule module)
            {
                _module = module;
            }

            /// <summary>
            /// 获取存档根路径。
            /// </summary>
            public string RootPath => _module.SaveRootPath;

            /// <summary>
            /// 获取指定键的存档文件路径。
            /// </summary>
            /// <param name="key">存档键。</param>
            /// <returns>存档文件路径。</returns>
            public string GetPath(string key)
            {
                return _module.GetSavePath(key);
            }

            /// <summary>
            /// 将对象保存为JSON格式的存档。
            /// </summary>
            /// <typeparam name="T">对象类型。</typeparam>
            /// <param name="key">存档键。</param>
            /// <param name="value">要保存的值。</param>
            /// <param name="prettyPrint">是否格式化输出。</param>
            public void SaveJson<T>(string key, T value, bool prettyPrint = false)
            {
                _module.SaveJson(key, value, prettyPrint);
            }

            /// <summary>
            /// 从JSON格式的存档加载数据。
            /// </summary>
            /// <typeparam name="T">对象类型。</typeparam>
            /// <param name="key">存档键。</param>
            /// <param name="defaultValue">默认值。</param>
            /// <returns>加载的值，如果存档不存在则返回默认值。</returns>
            public T LoadJson<T>(string key, T defaultValue = default)
            {
                return _module.LoadJson(key, defaultValue);
            }

            /// <summary>
            /// 保存带版本号的JSON格式存档。
            /// </summary>
            /// <typeparam name="T">对象类型。</typeparam>
            /// <param name="key">存档键。</param>
            /// <param name="value">要保存的值。</param>
            /// <param name="version">版本号。</param>
            /// <param name="validator">验证函数。</param>
            /// <param name="prettyPrint">是否格式化输出。</param>
            public void SaveVersionedJson<T>(string key, T value, int version, Func<T, bool> validator = null, bool prettyPrint = false)
            {
                _module.SaveVersionedJson(key, value, version, validator, prettyPrint);
            }

            /// <summary>
            /// 加载或迁移JSON格式的存档数据。
            /// </summary>
            /// <typeparam name="T">对象类型。</typeparam>
            /// <param name="key">存档键。</param>
            /// <param name="currentVersion">当前版本号。</param>
            /// <param name="migrate">迁移函数。</param>
            /// <param name="validator">验证函数。</param>
            /// <param name="defaultValue">默认值。</param>
            /// <returns>加载或迁移后的值。</returns>
            public T LoadOrMigrateJson<T>(string key, int currentVersion, Func<int, T, T> migrate, Func<T, bool> validator = null, T defaultValue = default)
            {
                return _module.LoadOrMigrateJson(key, currentVersion, migrate, validator, defaultValue);
            }
        }
    }
}
