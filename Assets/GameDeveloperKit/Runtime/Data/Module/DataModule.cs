using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 数据管理模块，负责配置、运行时配置、保存数据的读写和管理。
    /// 支持热更新配置、环境隔离、数据加密和版本化数据迁移。
    /// </summary>
    public sealed partial class DataModule : IGameFrameworkLifecycleModule
    {

        private readonly Dictionary<string, object> _configs = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HotUpdateConfigBoundary> _hotUpdateBoundaries = new(StringComparer.Ordinal);
        private GameFrameworkModuleStatus _status = GameFrameworkModuleStatus.Created;
        private bool _diagnosticsRegistered;
        private IDataEncryption _encryption;
        private string _hotUpdateConfigRootPath;

        /// <summary>
        /// 初始化 DataModule 的新实例。
        /// </summary>
        public DataModule()
        {
            Configure();
            ConfigTable = new ConfigTableAccessor(this);
            RuntimeConfig = new RuntimeConfigAccessor(this);
            SaveData = new SaveDataAccessor(this);
        }

        /// <summary>
        /// 获取数据模块的根路径。
        /// </summary>
        public string RootPath { get; private set; }

        /// <summary>
        /// 获取保存数据的根路径。
        /// </summary>
        public string SaveRootPath { get; private set; }

        /// <summary>
        /// 获取配置数据的根路径。
        /// </summary>
        public string ConfigRootPath { get; private set; }

        /// <summary>
        /// 获取热更新配置的根路径。
        /// </summary>
        public string HotUpdateConfigRootPath { get; private set; }

        /// <summary>
        /// 获取配置表访问器。
        /// </summary>
        public ConfigTableAccessor ConfigTable { get; }

        /// <summary>
        /// 获取运行时配置访问器。
        /// </summary>
        public RuntimeConfigAccessor RuntimeConfig { get; }

        /// <summary>
        /// 获取保存数据访问器。
        /// </summary>
        public SaveDataAccessor SaveData { get; }

        /// <summary>
        /// 获取已注册的配置数量。
        /// </summary>
        public int ConfigCount => _configs.Count;

        /// <summary>
        /// 获取或设置当前环境名称。
        /// </summary>
        public string CurrentEnvironment { get; private set; } = "Default";

        /// <summary>
        /// 获取是否启用了数据加密。
        /// </summary>
        public bool EncryptionEnabled => _encryption != null;

        /// <summary>
        /// 获取模块状态。
        /// </summary>
        public GameFrameworkModuleStatus Status => _status;

        /// <summary>
        /// 异步初始化数据模块。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>初始化任务。</returns>
        public UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (!GameFrameworkModuleLifecycleUtility.TryEnterInitialization(nameof(DataModule), ref _status, cancellationToken))
            {
                return UniTask.CompletedTask;
            }

            try
            {
                Configure();
                RegisterDiagnosticsSnapshotProviders();
                GameFrameworkModuleLifecycleUtility.CompleteInitialization(ref _status);
                return UniTask.CompletedTask;
            }
            catch
            {
                GameFrameworkModuleLifecycleUtility.FailInitialization(ref _status);
                throw;
            }
        }

        /// <summary>
        /// 异步关闭数据模块。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>关闭任务。</returns>
        public UniTask ShutdownAsync(CancellationToken cancellationToken = default)
        {
            if (!GameFrameworkModuleLifecycleUtility.TryEnterShutdown(nameof(DataModule), ref _status, cancellationToken))
            {
                return UniTask.CompletedTask;
            }

            Dispose();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 配置数据模块的存储路径。
        /// </summary>
        /// <param name="rootPath">根路径，如果为 null 则使用默认路径。</param>
        /// <param name="saveDirectoryName">保存数据目录名称。</param>
        /// <param name="configDirectoryName">配置数据目录名称。</param>
        public void Configure(string rootPath = null, string saveDirectoryName = "Save", string configDirectoryName = "Config")
        {
            RootPath = string.IsNullOrWhiteSpace(rootPath)
                ? Path.Combine(Application.persistentDataPath, "GameDeveloperKit")
                : rootPath;
            SaveRootPath = Path.Combine(RootPath, string.IsNullOrWhiteSpace(saveDirectoryName) ? "Save" : saveDirectoryName);
            ConfigRootPath = Path.Combine(RootPath, string.IsNullOrWhiteSpace(configDirectoryName) ? "Config" : configDirectoryName);
            _hotUpdateConfigRootPath = Path.Combine(ConfigRootPath, "HotUpdate");
            HotUpdateConfigRootPath = _hotUpdateConfigRootPath;

            Directory.CreateDirectory(RootPath);
            Directory.CreateDirectory(SaveRootPath);
            Directory.CreateDirectory(ConfigRootPath);
            Directory.CreateDirectory(HotUpdateConfigRootPath);
        }

        /// <summary>
        /// 注册配置数据。
        /// </summary>
        /// <typeparam name="T">配置数据类型。</typeparam>
        /// <param name="key">配置键。</param>
        /// <param name="value">配置值。</param>
        /// <exception cref="ArgumentException">当配置键为空时抛出。</exception>
        public void RegisterConfig<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Config key can not be empty.", nameof(key));
            }

            _configs[key] = value;
        }

        /// <summary>
        /// 设置当前环境。
        /// </summary>
        /// <param name="environment">环境名称。</param>
        public void SetEnvironment(string environment)
        {
            CurrentEnvironment = string.IsNullOrWhiteSpace(environment) ? "Default" : environment.Trim();
        }

        /// <summary>
        /// 设置数据加密器。
        /// </summary>
        /// <param name="encryption">数据加密器实现。</param>
        public void SetEncryption(IDataEncryption encryption)
        {
            _encryption = encryption;
        }

        /// <summary>
        /// 检查是否存在指定键的配置。
        /// </summary>
        /// <param name="key">配置键。</param>
        /// <returns>如果存在则返回 true，否则返回 false。</returns>
        public bool HasConfig(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && _configs.ContainsKey(key);
        }

        /// <summary>
        /// 获取指定键的配置。
        /// </summary>
        /// <typeparam name="T">配置数据类型。</typeparam>
        /// <param name="key">配置键。</param>
        /// <returns>配置值。</returns>
        /// <exception cref="InvalidOperationException">当配置不存在时抛出。</exception>
        public T GetConfig<T>(string key)
        {
            if (!TryGetConfig<T>(key, out var value))
            {
                throw new InvalidOperationException($"Config '{key}' is not registered.");
            }

            return value;
        }

        /// <summary>
        /// 尝试获取指定键的配置。
        /// </summary>
        /// <typeparam name="T">配置数据类型。</typeparam>
        /// <param name="key">配置键。</param>
        /// <param name="value">输出配置值。</param>
        /// <returns>如果获取成功则返回 true，否则返回 false。</returns>
        public bool TryGetConfig<T>(string key, out T value)
        {
            if (!string.IsNullOrWhiteSpace(key) && _configs.TryGetValue(key, out var item) && item is T typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// 移除指定键的配置。
        /// </summary>
        /// <param name="key">配置键。</param>
        /// <returns>如果移除成功则返回 true，否则返回 false。</returns>
        public bool RemoveConfig(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            _hotUpdateBoundaries.Remove(key);
            return _configs.Remove(key);
        }

        /// <summary>
        /// 清除所有配置。
        /// </summary>
        public void ClearConfigs()
        {
            _configs.Clear();
            _hotUpdateBoundaries.Clear();
        }

        /// <summary>
        /// 注册热更新配置边界。
        /// </summary>
        /// <param name="key">配置键。</param>
        /// <param name="layer">数据内容层。</param>
        /// <param name="allowRuntimeOverride">是否允许运行时覆盖。</param>
        /// <param name="preferHotUpdateSource">是否优先使用热更新源。</param>
        /// <exception cref="ArgumentException">当配置键为空时抛出。</exception>
        public void RegisterHotUpdateBoundary(string key, DataContentLayer layer, bool allowRuntimeOverride = false, bool preferHotUpdateSource = true)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Boundary key can not be empty.", nameof(key));
            }

            _hotUpdateBoundaries[key] = new HotUpdateConfigBoundary
            {
                Key = key,
                Layer = layer,
                AllowRuntimeOverride = allowRuntimeOverride,
                PreferHotUpdateSource = preferHotUpdateSource
            };
        }

        /// <summary>
        /// 注册配置表热更新边界。
        /// </summary>
        /// <param name="key">配置键。</param>
        /// <param name="preferHotUpdateSource">是否优先使用热更新源。</param>
        public void RegisterConfigTable(string key, bool preferHotUpdateSource = true)
        {
            RegisterHotUpdateBoundary(key, DataContentLayer.ConfigTable, false, preferHotUpdateSource);
        }

        /// <summary>
        /// 注册运行时配置热更新边界。
        /// </summary>
        /// <param name="key">配置键。</param>
        /// <param name="allowRuntimeOverride">是否允许运行时覆盖。</param>
        /// <param name="preferHotUpdateSource">是否优先使用热更新源。</param>
        public void RegisterRuntimeConfig(string key, bool allowRuntimeOverride = true, bool preferHotUpdateSource = true)
        {
            RegisterHotUpdateBoundary(key, DataContentLayer.RuntimeConfig, allowRuntimeOverride, preferHotUpdateSource);
        }

        /// <summary>
        /// 注册保存数据热更新边界。
        /// </summary>
        /// <param name="key">配置键。</param>
        public void RegisterSaveData(string key)
        {
            RegisterHotUpdateBoundary(key, DataContentLayer.SaveData, false, false);
        }

        /// <summary>
        /// 尝试获取热更新配置边界。
        /// </summary>
        /// <param name="key">配置键。</param>
        /// <param name="boundary">输出配置边界。</param>
        /// <returns>如果获取成功则返回 true，否则返回 false。</returns>
        public bool TryGetHotUpdateBoundary(string key, out HotUpdateConfigBoundary boundary)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                boundary = null;
                return false;
            }

            return _hotUpdateBoundaries.TryGetValue(key, out boundary);
        }

        /// <summary>
        /// 保存热更新配置到 JSON 文件。
        /// </summary>
        /// <typeparam name="T">配置数据类型。</typeparam>
        /// <param name="key">配置键。</param>
        /// <param name="value">配置值。</param>
        /// <param name="prettyPrint">是否格式化输出。</param>
        /// <exception cref="InvalidOperationException">当不允许运行时覆盖时抛出。</exception>
        public void SaveHotUpdateConfigJson<T>(string key, T value, bool prettyPrint = true)
        {
            var boundary = RequireHotUpdateBoundary(key);
            if (boundary.Layer == DataContentLayer.SaveData)
            {
                throw new InvalidOperationException($"Hot-update config '{key}' can not target SaveData layer.");
            }

            if (!boundary.AllowRuntimeOverride)
            {
                throw new InvalidOperationException($"Hot-update config '{key}' does not allow runtime override writes.");
            }

            var path = GetHotUpdateConfigPath(key);
            EnsureDirectory(path);
            File.WriteAllText(path, JsonUtility.ToJson(value, prettyPrint));
        }

        /// <summary>
        /// 加载配置（支持热更新）。
        /// </summary>
        /// <typeparam name="T">配置数据类型。</typeparam>
        /// <param name="key">配置键。</param>
        /// <param name="cache">是否缓存到内存。</param>
        /// <param name="defaultValue">默认值。</param>
        /// <returns>配置值。</returns>
        public T LoadConfigWithHotUpdate<T>(string key, bool cache = true, T defaultValue = default)
        {
            if (!TryGetHotUpdateBoundary(key, out var boundary))
            {
                return LoadConfigJson(key, cache, defaultValue);
            }

            if (boundary.PreferHotUpdateSource)
            {
                var hotValue = LoadHotUpdateConfigJsonInternal(key, defaultValue);
                if (!EqualityComparer<T>.Default.Equals(hotValue, defaultValue))
                {
                    if (cache)
                    {
                        RegisterConfig(key, hotValue);
                    }

                    return hotValue;
                }
            }

            return LoadConfigJson(key, cache, defaultValue);
        }

        /// <summary>
        /// 保存数据到 JSON 文件。
        /// </summary>
        /// <typeparam name="T">数据类型。</typeparam>
        /// <param name="key">数据键。</param>
        /// <param name="value">数据值。</param>
        /// <param name="prettyPrint">是否格式化输出。</param>
        public void SaveJson<T>(string key, T value, bool prettyPrint = false)
        {
            var path = GetSavePath(key);
            EnsureDirectory(path);
            File.WriteAllText(path, JsonUtility.ToJson(value, prettyPrint));
        }

        /// <summary>
        /// 从 JSON 文件加载数据。
        /// </summary>
        /// <typeparam name="T">数据类型。</typeparam>
        /// <param name="key">数据键。</param>
        /// <param name="defaultValue">默认值。</param>
        /// <returns>数据值。</returns>
        public T LoadJson<T>(string key, T defaultValue = default)
        {
            var path = GetSavePath(key);
            if (!File.Exists(path))
            {
                return defaultValue;
            }

            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return defaultValue;
            }

            return JsonUtility.FromJson<T>(json);
        }

        /// <summary>
        /// 保存文本数据。
        /// </summary>
        /// <param name="key">数据键。</param>
        /// <param name="text">文本内容。</param>
        public void SaveText(string key, string text)
        {
            var path = GetSavePath(key);
            EnsureDirectory(path);
            File.WriteAllText(path, text ?? string.Empty);
        }

        /// <summary>
        /// 加载文本数据。
        /// </summary>
        /// <param name="key">数据键。</param>
        /// <param name="defaultValue">默认值。</param>
        /// <returns>文本内容。</returns>
        public string LoadText(string key, string defaultValue = "")
        {
            var path = GetSavePath(key);
            return File.Exists(path) ? File.ReadAllText(path) : defaultValue;
        }

        /// <summary>
        /// 保存字节数据。
        /// </summary>
        /// <param name="key">数据键。</param>
        /// <param name="data">字节数组。</param>
        public void SaveBytes(string key, byte[] data)
        {
            var path = GetSavePath(key);
            EnsureDirectory(path);
            File.WriteAllBytes(path, data ?? Array.Empty<byte>());
        }

        /// <summary>
        /// 加载字节数据。
        /// </summary>
        /// <param name="key">数据键。</param>
        /// <returns>字节数组。</returns>
        public byte[] LoadBytes(string key)
        {
            var path = GetSavePath(key);
            return File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>();
        }

        /// <summary>
        /// 检查是否存在指定的保存数据。
        /// </summary>
        /// <param name="key">数据键。</param>
        /// <returns>如果存在则返回 true，否则返回 false。</returns>
        public bool HasSave(string key)
        {
            return File.Exists(GetSavePath(key));
        }

        /// <summary>
        /// 删除指定的保存数据。
        /// </summary>
        /// <param name="key">数据键。</param>
        /// <returns>如果删除成功则返回 true，否则返回 false。</returns>
        public bool DeleteSave(string key)
        {
            var path = GetSavePath(key);
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }

        /// <summary>
        /// 获取保存数据的所有键。
        /// </summary>
        /// <param name="relativeDirectory">相对目录。</param>
        /// <param name="searchPattern">搜索模式。</param>
        /// <param name="searchOption">搜索选项。</param>
        /// <returns>保存数据键数组。</returns>
        public string[] GetSaveKeys(string relativeDirectory = null, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            var folder = ResolveStoragePath(SaveRootPath, relativeDirectory);
            if (!Directory.Exists(folder))
            {
                return Array.Empty<string>();
            }

            var files = Directory.GetFiles(folder, string.IsNullOrWhiteSpace(searchPattern) ? "*" : searchPattern, searchOption);
            for (var i = 0; i < files.Length; i++)
            {
                files[i] = files[i].Substring(SaveRootPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            return files;
        }

        /// <summary>
        /// 保存配置到 JSON 文件。
        /// </summary>
        /// <typeparam name="T">配置数据类型。</typeparam>
        /// <param name="key">配置键。</param>
        /// <param name="value">配置值。</param>
        /// <param name="cache">是否缓存到内存。</param>
        /// <param name="prettyPrint">是否格式化输出。</param>
        public void SaveConfigJson<T>(string key, T value, bool cache = true, bool prettyPrint = true)
        {
            var path = GetConfigPath(key);
            EnsureDirectory(path);
            File.WriteAllText(path, JsonUtility.ToJson(value, prettyPrint));

            if (cache)
            {
                RegisterConfig(key, value);
            }
        }

        /// <summary>
        /// 保存环境配置到 JSON 文件。
        /// </summary>
        /// <typeparam name="T">配置数据类型。</typeparam>
        /// <param name="key">配置键。</param>
        /// <param name="value">配置值。</param>
        /// <param name="cache">是否缓存到内存。</param>
        /// <param name="prettyPrint">是否格式化输出。</param>
        /// <param name="environment">环境名称。</param>
        public void SaveEnvironmentConfigJson<T>(string key, T value, bool cache = true, bool prettyPrint = true, string environment = null)
        {
            var environmentKey = GetEnvironmentScopedKey(key, environment);
            SaveConfigJson(environmentKey, value, cache, prettyPrint);
        }

        /// <summary>
        /// 从 JSON 文件加载配置。
        /// </summary>
        /// <typeparam name="T">配置数据类型。</typeparam>
        /// <param name="key">配置键。</param>
        /// <param name="cache">是否缓存到内存。</param>
        /// <param name="defaultValue">默认值。</param>
        /// <returns>配置值。</returns>
        public T LoadConfigJson<T>(string key, bool cache = true, T defaultValue = default)
        {
            if (cache && TryGetConfig<T>(key, out var cachedValue))
            {
                return cachedValue;
            }

            var path = GetConfigPath(key);
            if (!File.Exists(path))
            {
                return defaultValue;
            }

            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return defaultValue;
            }

            var value = JsonUtility.FromJson<T>(json);
            if (cache)
            {
                RegisterConfig(key, value);
            }

            return value;
        }

        /// <summary>
        /// 从 JSON 文件加载环境配置。
        /// </summary>
        /// <typeparam name="T">配置数据类型。</typeparam>
        /// <param name="key">配置键。</param>
        /// <param name="cache">是否缓存到内存。</param>
        /// <param name="defaultValue">默认值。</param>
        /// <param name="environment">环境名称。</param>
        /// <returns>配置值。</returns>
        public T LoadEnvironmentConfigJson<T>(string key, bool cache = true, T defaultValue = default, string environment = null)
        {
            return LoadConfigJson(GetEnvironmentScopedKey(key, environment), cache, defaultValue);
        }

        /// <summary>
        /// 保存版本化数据到 JSON 文件。
        /// </summary>
        /// <typeparam name="T">数据类型。</typeparam>
        /// <param name="key">数据键。</param>
        /// <param name="value">数据值。</param>
        /// <param name="version">版本号。</param>
        /// <param name="validator">数据验证器。</param>
        /// <param name="prettyPrint">是否格式化输出。</param>
        /// <exception cref="ArgumentOutOfRangeException">当版本号小于 0 时抛出。</exception>
        /// <exception cref="InvalidOperationException">当数据验证失败时抛出。</exception>
        public void SaveVersionedJson<T>(string key, T value, int version, Func<T, bool> validator = null, bool prettyPrint = false)
        {
            if (version < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(version));
            }

            if (validator != null && !validator(value))
            {
                throw new InvalidOperationException($"Versioned save '{key}' failed validation.");
            }

            var envelope = new VersionedDataEnvelope
            {
                Version = version,
                Environment = CurrentEnvironment,
                PayloadJson = JsonUtility.ToJson(value, prettyPrint)
            };

            var path = GetSavePath(key);
            EnsureDirectory(path);
            WriteStorageText(path, JsonUtility.ToJson(envelope, prettyPrint));
        }

        /// <summary>
        /// 加载或迁移版本化数据。
        /// </summary>
        /// <typeparam name="T">数据类型。</typeparam>
        /// <param name="key">数据键。</param>
        /// <param name="currentVersion">当前版本号。</param>
        /// <param name="migrate">数据迁移函数。</param>
        /// <param name="validator">数据验证器。</param>
        /// <param name="defaultValue">默认值。</param>
        /// <returns>数据值。</returns>
        /// <exception cref="ArgumentOutOfRangeException">当版本号小于 0 时抛出。</exception>
        /// <exception cref="InvalidOperationException">当数据验证失败时抛出。</exception>
        public T LoadOrMigrateJson<T>(string key, int currentVersion, Func<int, T, T> migrate, Func<T, bool> validator = null, T defaultValue = default)
        {
            if (currentVersion < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(currentVersion));
            }

            var path = GetSavePath(key);
            if (!File.Exists(path))
            {
                return defaultValue;
            }

            var json = ReadStorageText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return defaultValue;
            }

            var envelope = JsonUtility.FromJson<VersionedDataEnvelope>(json);
            if (envelope == null || string.IsNullOrWhiteSpace(envelope.PayloadJson))
            {
                return defaultValue;
            }

            var value = JsonUtility.FromJson<T>(envelope.PayloadJson);
            if (envelope.Version < currentVersion && migrate != null)
            {
                value = migrate(envelope.Version, value);
                SaveVersionedJson(key, value, currentVersion, validator);
            }

            if (validator != null && !validator(value))
            {
                throw new InvalidOperationException($"Versioned save '{key}' failed validation after load/migrate.");
            }

            return value;
        }

        /// <summary>
        /// 获取保存数据的完整路径。
        /// </summary>
        /// <param name="key">数据键。</param>
        /// <returns>完整路径。</returns>
        public string GetSavePath(string key)
        {
            return ResolveStorageFilePath(SaveRootPath, key, ".json");
        }

        /// <summary>
        /// 获取配置数据的完整路径。
        /// </summary>
        /// <param name="key">数据键。</param>
        /// <returns>完整路径。</returns>
        public string GetConfigPath(string key)
        {
            return ResolveStorageFilePath(ConfigRootPath, key, ".json");
        }

        /// <summary>
        /// 获取热更新配置的完整路径。
        /// </summary>
        /// <param name="key">数据键。</param>
        /// <returns>完整路径。</returns>
        public string GetHotUpdateConfigPath(string key)
        {
            return ResolveStorageFilePath(HotUpdateConfigRootPath, key, ".json");
        }

        /// <summary>
        /// 释放数据模块资源。
        /// </summary>
        public void Dispose()
        {
            RemoveDiagnosticsSnapshotProviders();
            _configs.Clear();
            _status = GameFrameworkModuleStatus.Disposed;
        }

        private static string ResolveStorageFilePath(string root, string key, string defaultExtension)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Storage key can not be empty.", nameof(key));
            }

            var normalized = key.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            if (Path.HasExtension(normalized))
            {
                return Path.Combine(root, normalized);
            }

            return Path.Combine(root, normalized + defaultExtension);
        }

        private static string ResolveStoragePath(string root, string relativeDirectory)
        {
            if (string.IsNullOrWhiteSpace(relativeDirectory))
            {
                return root;
            }

            var normalized = relativeDirectory.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(root, normalized);
        }

        private static void EnsureDirectory(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private string GetEnvironmentScopedKey(string key, string environment)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Config key can not be empty.", nameof(key));
            }

            var scope = string.IsNullOrWhiteSpace(environment) ? CurrentEnvironment : environment.Trim();
            return Path.Combine(scope, key);
        }

        private void RegisterDiagnosticsSnapshotProviders()
        {
            if (_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RegisterSnapshotProvider("Data.RootPath", () => RootPath ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Data.ConfigCount", () => ConfigCount.ToString());
            diagnostics.RegisterSnapshotProvider("Data.Environment", () => CurrentEnvironment ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Data.HotUpdateBoundaryCount", () => _hotUpdateBoundaries.Count.ToString());
            diagnostics.RegisterSnapshotProvider("Data.HotUpdateConfigRootPath", () => HotUpdateConfigRootPath ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Data.ConfigTableRootPath", () => ConfigTable.RootPath ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Data.RuntimeConfigRootPath", () => RuntimeConfig.RootPath ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Data.SaveDataRootPath", () => SaveData.RootPath ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Data.EncryptionEnabled", () => EncryptionEnabled.ToString());
            _diagnosticsRegistered = true;
        }

        private void RemoveDiagnosticsSnapshotProviders()
        {
            if (!_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RemoveSnapshotProvider("Data.RootPath");
            diagnostics.RemoveSnapshotProvider("Data.ConfigCount");
            diagnostics.RemoveSnapshotProvider("Data.Environment");
            diagnostics.RemoveSnapshotProvider("Data.HotUpdateBoundaryCount");
            diagnostics.RemoveSnapshotProvider("Data.HotUpdateConfigRootPath");
            diagnostics.RemoveSnapshotProvider("Data.ConfigTableRootPath");
            diagnostics.RemoveSnapshotProvider("Data.RuntimeConfigRootPath");
            diagnostics.RemoveSnapshotProvider("Data.SaveDataRootPath");
            diagnostics.RemoveSnapshotProvider("Data.EncryptionEnabled");
            _diagnosticsRegistered = false;
        }

        private HotUpdateConfigBoundary RequireHotUpdateBoundary(string key)
        {
            if (!TryGetHotUpdateBoundary(key, out var boundary))
            {
                throw new InvalidOperationException($"Hot-update boundary for '{key}' is not registered.");
            }

            return boundary;
        }

        private T LoadHotUpdateConfigJsonInternal<T>(string key, T defaultValue = default)
        {
            var path = GetHotUpdateConfigPath(key);
            if (!File.Exists(path))
            {
                return defaultValue;
            }

            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return defaultValue;
            }

            return JsonUtility.FromJson<T>(json);
        }

        private void WriteStorageText(string path, string text)
        {
            if (_encryption == null)
            {
                File.WriteAllText(path, text ?? string.Empty);
                return;
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(text ?? string.Empty);
            File.WriteAllBytes(path, _encryption.Encrypt(bytes));
        }

        private string ReadStorageText(string path)
        {
            if (_encryption == null)
            {
                return File.ReadAllText(path);
            }

            var encrypted = File.ReadAllBytes(path);
            var decrypted = _encryption.Decrypt(encrypted);
            return System.Text.Encoding.UTF8.GetString(decrypted ?? Array.Empty<byte>());
        }
    }
}
