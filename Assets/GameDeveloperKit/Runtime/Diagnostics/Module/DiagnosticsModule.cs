using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 诊断模块，提供日志记录、快照捕获和错误聚合功能。
    /// 支持多级别日志、自定义快照提供者和错误摘要。
    /// </summary>
    public sealed class DiagnosticsModule : IGameFrameworkLifecycleModule
    {
        private readonly List<DiagnosticsLogEntry> _entries = new();
        private readonly Dictionary<string, Func<string>> _snapshotProviders = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _snapshots = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DiagnosticsErrorSummary> _errorSummaries = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _requiredSnapshotKeys = new(StringComparer.Ordinal);
        private bool _isInitialized;

        /// <summary>
        /// 获取或设置最大日志条目数。
        /// </summary>
        public int MaxEntryCount { get; set; } = 200;

        /// <summary>
        /// 获取日志条目列表。
        /// </summary>
        public IReadOnlyList<DiagnosticsLogEntry> Entries => _entries;

        /// <summary>
        /// 获取模块状态。
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 当记录日志时触发。
        /// </summary>
        public event Action<DiagnosticsLogEntry> Logged;

        /// <summary>
        /// 异步初始化诊断模块。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>初始化任务。</returns>
        public UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
            {
                return UniTask.CompletedTask;
            }

            try
            {
                RegisterDefaultSnapshotStandards();
                _isInitialized = true;
                return UniTask.CompletedTask;
            }
            catch
            {
                _isInitialized = false;
                throw;
            }
        }

        /// <summary>
        /// 异步关闭诊断模块。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>关闭任务。</returns>
        public UniTask ShutdownAsync(CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
            {
                return UniTask.CompletedTask;
            }

            Dispose();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 记录调试日志。
        /// </summary>
        /// <param name="message">日志消息。</param>
        /// <param name="context">上下文。</param>
        public void LogDebug(string message, string context = null)
        {
            Log(DiagnosticsLogLevel.Debug, message, context);
        }

        /// <summary>
        /// 记录信息日志。
        /// </summary>
        /// <param name="message">日志消息。</param>
        /// <param name="context">上下文。</param>
        public void LogInfo(string message, string context = null)
        {
            Log(DiagnosticsLogLevel.Info, message, context);
        }

        /// <summary>
        /// 记录警告日志。
        /// </summary>
        /// <param name="message">日志消息。</param>
        /// <param name="context">上下文。</param>
        public void LogWarning(string message, string context = null)
        {
            Log(DiagnosticsLogLevel.Warning, message, context);
        }

        /// <summary>
        /// 记录错误日志。
        /// </summary>
        /// <param name="message">日志消息。</param>
        /// <param name="context">上下文。</param>
        public void LogError(string message, string context = null)
        {
            Log(DiagnosticsLogLevel.Error, message, context);
        }

        /// <summary>
        /// 记录日志。
        /// </summary>
        /// <param name="level">日志级别。</param>
        /// <param name="message">日志消息。</param>
        /// <param name="context">上下文。</param>
        public void Log(DiagnosticsLogLevel level, string message, string context = null)
        {
            Log(level, message, context, null, null);
        }

        /// <summary>
        /// 记录日志（带扩展字段）。
        /// </summary>
        /// <param name="level">日志级别。</param>
        /// <param name="message">日志消息。</param>
        /// <param name="context">上下文。</param>
        /// <param name="scope">作用域。</param>
        /// <param name="fields">扩展字段。</param>
        public void Log(DiagnosticsLogLevel level, string message, string context, string scope, IReadOnlyDictionary<string, string> fields)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Log message can not be empty.", nameof(message));
            }

            var entry = new DiagnosticsLogEntry(level, message, context, scope, fields);
            _entries.Add(entry);
            TrimEntries();
            if (level == DiagnosticsLogLevel.Error)
            {
                AggregateError(entry);
            }

            WriteToUnityConsole(entry);
            Logged?.Invoke(entry);
        }

        /// <summary>
        /// 报告异常。
        /// </summary>
        /// <param name="code">错误代码。</param>
        /// <param name="exception">异常对象。</param>
        /// <param name="context">上下文。</param>
        /// <param name="scope">作用域。</param>
        /// <exception cref="ArgumentNullException">当异常为 null 时抛出。</exception>
        public void ReportException(string code, Exception exception, string context = null, string scope = null)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            var fields = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Code"] = code ?? string.Empty,
                ["ExceptionType"] = exception.GetType().FullName ?? string.Empty
            };

            Log(DiagnosticsLogLevel.Error, exception.Message, context, scope, fields);
        }

        /// <summary>
        /// 捕获快照值。
        /// </summary>
        /// <param name="key">快照键。</param>
        /// <param name="value">快照值。</param>
        /// <exception cref="ArgumentException">当快照键为空时抛出。</exception>
        public void CaptureSnapshot(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Snapshot key can not be empty.", nameof(key));
            }

            _snapshots[key] = value ?? string.Empty;
        }

        /// <summary>
        /// 注册快照提供者。
        /// </summary>
        /// <param name="key">快照键。</param>
        /// <param name="provider">提供者函数。</param>
        /// <exception cref="ArgumentException">当快照键为空时抛出。</exception>
        /// <exception cref="ArgumentNullException">当提供者为 null 时抛出。</exception>
        public void RegisterSnapshotProvider(string key, Func<string> provider)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Snapshot key can not be empty.", nameof(key));
            }

            _snapshotProviders[key] = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// 移除快照提供者。
        /// </summary>
        /// <param name="key">快照键。</param>
        /// <returns>如果移除成功则返回 true，否则返回 false。</returns>
        public bool RemoveSnapshotProvider(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return _snapshotProviders.Remove(key);
        }

        /// <summary>
        /// 尝试获取快照值。
        /// </summary>
        /// <param name="key">快照键。</param>
        /// <param name="value">输出快照值。</param>
        /// <returns>如果获取成功则返回 true，否则返回 false。</returns>
        public bool TryGetSnapshot(string key, out string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = null;
                return false;
            }

            if (_snapshotProviders.TryGetValue(key, out var provider))
            {
                value = provider?.Invoke() ?? string.Empty;
                return true;
            }

            return _snapshots.TryGetValue(key, out value);
        }

        /// <summary>
        /// 获取所有快照值。
        /// </summary>
        /// <returns>快照字典。</returns>
        public IReadOnlyDictionary<string, string> GetSnapshots()
        {
            var results = new Dictionary<string, string>(_snapshots, StringComparer.Ordinal);
            foreach (var pair in _snapshotProviders)
            {
                results[pair.Key] = pair.Value?.Invoke() ?? string.Empty;
            }

            return results;
        }

        /// <summary>
        /// 注册快照键集合。
        /// </summary>
        /// <param name="moduleName">模块名称。</param>
        /// <param name="keys">快照键数组。</param>
        /// <exception cref="ArgumentException">当模块名称为空时抛出。</exception>
        public void RegisterSnapshotKeys(string moduleName, params string[] keys)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
            {
                throw new ArgumentException("Module name can not be empty.", nameof(moduleName));
            }

            if (!_requiredSnapshotKeys.TryGetValue(moduleName, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                _requiredSnapshotKeys.Add(moduleName, set);
            }

            if (keys == null)
            {
                return;
            }

            for (var i = 0; i < keys.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(keys[i]))
                {
                    set.Add(keys[i]);
                }
            }
        }

        /// <summary>
        /// 获取快照键注册表。
        /// </summary>
        /// <returns>键注册表（模块名 -> 键数组）。</returns>
        public IReadOnlyDictionary<string, string[]> GetSnapshotKeyRegistry()
        {
            var results = new Dictionary<string, string[]>(_requiredSnapshotKeys.Count, StringComparer.Ordinal);
            foreach (var pair in _requiredSnapshotKeys)
            {
                var keys = new string[pair.Value.Count];
                pair.Value.CopyTo(keys);
                results[pair.Key] = keys;
            }

            return results;
        }

        /// <summary>
        /// 获取错误摘要列表。
        /// </summary>
        /// <returns>错误摘要字典。</returns>
        public IReadOnlyDictionary<string, DiagnosticsErrorSummary> GetErrorSummaries()
        {
            return _errorSummaries;
        }

        /// <summary>
        /// 捕获处理阶段信息。
        /// </summary>
        /// <param name="pipeline">流水线名称。</param>
        /// <param name="stage">阶段名称。</param>
        /// <param name="detail">阶段详情。</param>
        /// <exception cref="ArgumentException">当流水线名称为空时抛出。</exception>
        public void CaptureStage(string pipeline, string stage, string detail = null)
        {
            if (string.IsNullOrWhiteSpace(pipeline))
            {
                throw new ArgumentException("Pipeline can not be empty.", nameof(pipeline));
            }

            CaptureSnapshot($"{pipeline}.Stage", stage ?? string.Empty);
            if (detail != null)
            {
                CaptureSnapshot($"{pipeline}.StageDetail", detail);
            }
        }

        /// <summary>
        /// 清除所有日志条目。
        /// </summary>
        public void ClearEntries()
        {
            _entries.Clear();
        }

        /// <summary>
        /// 释放诊断模块资源。
        /// </summary>
        public void Dispose()
        {
            _entries.Clear();
            _snapshotProviders.Clear();
            _snapshots.Clear();
            _errorSummaries.Clear();
            _requiredSnapshotKeys.Clear();
            Logged = null;
            _isInitialized = false;
        }

        private void TrimEntries()
        {
            while (_entries.Count > MaxEntryCount)
            {
                _entries.RemoveAt(0);
            }
        }

        private void AggregateError(DiagnosticsLogEntry entry)
        {
            var key = string.Concat(entry.Scope, "|", entry.Context, "|", entry.Message);
            if (!_errorSummaries.TryGetValue(key, out var summary))
            {
                summary = new DiagnosticsErrorSummary(key, entry.Message, entry.Context, entry.Scope);
                _errorSummaries.Add(key, summary);
            }

            summary.Track(entry.Message, entry.Context, entry.Scope);
        }

        private void RegisterDefaultSnapshotStandards()
        {
            RegisterSnapshotKeys("Startup", "Startup.Stage", "Startup.Error");
            RegisterSnapshotKeys("Download", "Download.LastStage", "Download.LastError", "Download.AggregateProgress");
            RegisterSnapshotKeys("Resource", "Resource.PackageCount", "Resource.PreparedPackageCount", "Resource.PlayMode");
            RegisterSnapshotKeys("Network", "Network.LastStage", "Network.LastError", "Network.LastUrl");
        }

        private static void WriteToUnityConsole(DiagnosticsLogEntry entry)
        {
            var message = string.IsNullOrWhiteSpace(entry.Context) ? entry.Message : $"{entry.Message} | {entry.Context}";
            if (!string.IsNullOrWhiteSpace(entry.Scope))
            {
                message = $"[{entry.Scope}] {message}";
            }
            switch (entry.Level)
            {
                case DiagnosticsLogLevel.Debug:
                case DiagnosticsLogLevel.Info:
                    Debug.Log(message);
                    break;
                case DiagnosticsLogLevel.Warning:
                    Debug.LogWarning(message);
                    break;
                case DiagnosticsLogLevel.Error:
                    Debug.LogError(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }
    }
}

