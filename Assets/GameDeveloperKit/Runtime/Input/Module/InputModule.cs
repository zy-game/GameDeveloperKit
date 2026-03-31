using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 输入管理模块，提供按键绑定、输入上下文管理和输入拦截功能。
    /// </summary>
    public sealed partial class InputModule : IGameFrameworkLifecycleModule
    {
        private const string BindingsSaveKey = "GameDeveloperKit/Input/Bindings";
        private readonly Dictionary<string, InputBindingData> _bindings = new(StringComparer.Ordinal);
        private readonly Dictionary<string, InputBindingData> _defaultBindings = new(StringComparer.Ordinal);
        private readonly Dictionary<int, InputContext> _blockContexts = new();
        private bool _isInitialized;
        private bool _diagnosticsRegistered;
        private int _nextBlockTokenId = 1;

        /// <summary>
        /// 初始化 InputModule 的新实例。
        /// </summary>
        public InputModule()
        {
            Backend = LegacyUnityInputBackend.Instance;
            EnabledContexts = InputContext.All;
        }

        /// <summary>
        /// 获取或设置是否启用输入。
        /// </summary>
        public bool InputEnabled { get; private set; } = true;

        /// <summary>
        /// 获取或设置启用的输入上下文。
        /// </summary>
        public InputContext EnabledContexts { get; private set; }

        /// <summary>
        /// 获取绑定的按键数量。
        /// </summary>
        public int BindingCount => _bindings.Count;

        /// <summary>
        /// 获取输入拦截器数量。
        /// </summary>
        public int BlockCount => _blockContexts.Count;

        /// <summary>
        /// 获取或设置输入后端实现。
        /// </summary>
        public IInputBackend Backend { get; private set; }

        /// <summary>
        /// 获取动作命名约定。
        /// </summary>
        public string ActionNameConvention => "Domain.ActionName";

        /// <summary>
        /// 获取模块状态。
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 当按键绑定改变时触发。
        /// </summary>
        public event Action<string, KeyCode> BindingChanged;

        /// <summary>
        /// 异步初始化输入模块。
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
                Game.EnsureModuleReady<DataModule>();
                ReloadBindingsFromStorage();
                RegisterDiagnosticsSnapshotProviders();
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
        /// 异步关闭输入模块。
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
        /// 注册输入动作。
        /// </summary>
        /// <param name="actionName">动作名称（格式：Domain.ActionName）。</param>
        /// <param name="defaultKey">默认按键。</param>
        /// <param name="context">输入上下文。</param>
        /// <exception cref="ArgumentException">当动作名称无效时抛出。</exception>
        public void RegisterAction(string actionName, KeyCode defaultKey, InputContext context = InputContext.Gameplay)
        {
            actionName = NormalizeActionName(actionName);
            ValidateActionName(actionName);

            var defaultBinding = new InputBindingData
            {
                ActionName = actionName,
                Key = defaultKey,
                Context = context
            };

            _defaultBindings[actionName] = CloneBinding(defaultBinding);
            if (_bindings.TryGetValue(actionName, out var existingBinding))
            {
                existingBinding.Context = context;
                return;
            }

            _bindings[actionName] = CloneBinding(defaultBinding);
        }

        /// <summary>
        /// 检查是否存在指定的动作。
        /// </summary>
        /// <param name="actionName">动作名称。</param>
        /// <returns>如果存在则返回 true，否则返回 false。</returns>
        public bool HasAction(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName) && _bindings.ContainsKey(NormalizeActionName(actionName));
        }

        /// <summary>
        /// 获取指定动作的按键绑定。
        /// </summary>
        /// <param name="actionName">动作名称。</param>
        /// <returns>按键代码。</returns>
        /// <exception cref="InvalidOperationException">当动作不存在时抛出。</exception>
        public KeyCode GetBinding(string actionName)
        {
            actionName = NormalizeActionName(actionName);
            if (!_bindings.TryGetValue(actionName, out var binding))
            {
                throw new InvalidOperationException($"Input action '{actionName}' is not registered.");
            }

            return binding.Key;
        }

        /// <summary>
        /// 设置指定动作的按键绑定。
        /// </summary>
        /// <param name="actionName">动作名称。</param>
        /// <param name="key">按键代码。</param>
        /// <param name="save">是否保存到存储。</param>
        /// <exception cref="InvalidOperationException">当动作不存在时抛出。</exception>
        public void SetBinding(string actionName, KeyCode key, bool save = true)
        {
            actionName = NormalizeActionName(actionName);
            if (!_bindings.TryGetValue(actionName, out var binding))
            {
                throw new InvalidOperationException($"Input action '{actionName}' is not registered.");
            }

            binding.Key = key;
            BindingChanged?.Invoke(actionName, key);

            if (save)
            {
                SaveBindings();
            }
        }

        /// <summary>
        /// 设置是否启用输入。
        /// </summary>
        /// <param name="enabled">是否启用。</param>
        public void SetInputEnabled(bool enabled)
        {
            InputEnabled = enabled;
        }

        /// <summary>
        /// 设置输入后端实现。
        /// </summary>
        /// <param name="backend">输入后端。</param>
        /// <exception cref="ArgumentNullException">当后端为 null 时抛出。</exception>
        public void SetBackend(IInputBackend backend)
        {
            Backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        /// <summary>
        /// 启用游戏玩法上下文。
        /// </summary>
        public void EnableGameplay()
        {
            EnabledContexts |= InputContext.Gameplay;
        }

        /// <summary>
        /// 禁用游戏玩法上下文。
        /// </summary>
        public void DisableGameplay()
        {
            EnabledContexts &= ~InputContext.Gameplay;
        }

        /// <summary>
        /// 启用 UI 上下文。
        /// </summary>
        public void EnableUI()
        {
            EnabledContexts |= InputContext.UI;
        }

        /// <summary>
        /// 禁用 UI 上下文。
        /// </summary>
        public void DisableUI()
        {
            EnabledContexts &= ~InputContext.UI;
        }

        /// <summary>
        /// 设置指定上下文的启用状态。
        /// </summary>
        /// <param name="context">输入上下文。</param>
        /// <param name="enabled">是否启用。</param>
        public void SetContextEnabled(InputContext context, bool enabled)
        {
            EnabledContexts = enabled ? EnabledContexts | context : EnabledContexts & ~context;
        }

        /// <summary>
        /// 检查指定动作是否被按下（持续）。
        /// </summary>
        /// <param name="actionName">动作名称。</param>
        /// <returns>如果被按下则返回 true，否则返回 false。</returns>
        public bool IsActionPressed(string actionName)
        {
            return TryGetBinding(actionName, out var binding) && Backend.GetKey(binding.Key);
        }

        /// <summary>
        /// 检查指定动作是否刚刚按下（单次）。
        /// </summary>
        /// <param name="actionName">动作名称。</param>
        /// <returns>如果刚刚按下则返回 true，否则返回 false。</returns>
        public bool IsActionDown(string actionName)
        {
            return TryGetBinding(actionName, out var binding) && Backend.GetKeyDown(binding.Key);
        }

        /// <summary>
        /// 检查指定动作是否刚刚松开。
        /// </summary>
        /// <param name="actionName">动作名称。</param>
        /// <returns>如果刚刚松开则返回 true，否则返回 false。</returns>
        public bool IsActionUp(string actionName)
        {
            return TryGetBinding(actionName, out var binding) && Backend.GetKeyUp(binding.Key);
        }

        /// <summary>
        /// 获取轴输入值。
        /// </summary>
        /// <param name="axisName">轴名称。</param>
        /// <param name="context">输入上下文。</param>
        /// <param name="raw">是否使用原始值。</param>
        /// <returns>轴输入值。</returns>
        /// <exception cref="ArgumentException">当轴名称为空时抛出。</exception>
        public float GetAxis(string axisName, InputContext context = InputContext.Gameplay, bool raw = false)
        {
            if (string.IsNullOrWhiteSpace(axisName))
            {
                throw new ArgumentException("Axis name can not be empty.", nameof(axisName));
            }

            if (!CanUseContext(context))
            {
                return 0f;
            }

            return raw ? Backend.GetAxisRaw(axisName) : Backend.GetAxis(axisName);
        }

        /// <summary>
        /// 获取输入拦截器。
        /// </summary>
        /// <param name="context">要拦截的上下文。</param>
        /// <returns>拦截器令牌。</returns>
        public IDisposable AcquireBlock(InputContext context = InputContext.All)
        {
            var tokenId = _nextBlockTokenId++;
            _blockContexts[tokenId] = context;
            return new InputBlockToken(this, tokenId);
        }

        /// <summary>
        /// 检查指定上下文是否被拦截。
        /// </summary>
        /// <param name="context">输入上下文。</param>
        /// <returns>如果被拦截则返回 true，否则返回 false。</returns>
        public bool IsBlocked(InputContext context = InputContext.All)
        {
            if (_blockContexts.Count == 0)
            {
                return false;
            }

            foreach (var blockedContext in _blockContexts.Values)
            {
                if ((blockedContext & context) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 保存按键绑定到存储。
        /// </summary>
        public void SaveBindings()
        {
            var collection = new InputBindingCollection
            {
                Bindings = new InputBindingData[_bindings.Count]
            };

            var index = 0;
            foreach (var pair in _bindings)
            {
                collection.Bindings[index++] = new InputBindingData
                {
                    ActionName = pair.Value.ActionName,
                    Key = pair.Value.Key,
                    Context = pair.Value.Context
                };
            }

            Game.Data.SaveJson(BindingsSaveKey, collection, true);
        }

        /// <summary>
        /// 从存储加载按键绑定。
        /// </summary>
        public void LoadBindings()
        {
            InputBindingCollection collection;
            try
            {
                collection = Game.Data.LoadJson(BindingsSaveKey, new InputBindingCollection());
            }
            catch
            {
                collection = new InputBindingCollection();
            }

            if (collection?.Bindings == null)
            {
                return;
            }

            for (var i = 0; i < collection.Bindings.Length; i++)
            {
                var binding = collection.Bindings[i];
                if (binding == null || string.IsNullOrWhiteSpace(binding.ActionName))
                {
                    continue;
                }

                _bindings[binding.ActionName] = CloneBinding(binding);
            }
        }

        /// <summary>
        /// 从存储重新加载按键绑定（先重置为默认值）。
        /// </summary>
        public void ReloadBindingsFromStorage()
        {
            ResetBindingsToDefaults(false);
            LoadBindings();
        }

        /// <summary>
        /// 重置按键绑定为默认值。
        /// </summary>
        /// <param name="save">是否保存到存储。</param>
        public void ResetBindingsToDefaults(bool save = true)
        {
            _bindings.Clear();
            foreach (var pair in _defaultBindings)
            {
                _bindings[pair.Key] = CloneBinding(pair.Value);
            }

            if (save)
            {
                SaveBindings();
            }
        }

        /// <summary>
        /// 释放输入模块资源。
        /// </summary>
        public void Dispose()
        {
            _bindings.Clear();
            _defaultBindings.Clear();
            _blockContexts.Clear();
            BindingChanged = null;
            RemoveDiagnosticsSnapshotProviders();
            _isInitialized = false;
        }

        private bool TryGetBinding(string actionName, out InputBindingData binding)
        {
            actionName = NormalizeActionName(actionName);
            if (_bindings.TryGetValue(actionName, out binding))
            {
                return InputEnabled && CanUseContext(binding.Context);
            }

            return false;
        }

        private bool CanUseContext(InputContext context)
        {
            if (!InputEnabled || (EnabledContexts & context) != context)
            {
                return false;
            }

            if (_blockContexts.Count == 0)
            {
                return true;
            }

            foreach (var blockedContext in _blockContexts.Values)
            {
                if ((blockedContext & context) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private void ReleaseBlock(int tokenId)
        {
            _blockContexts.Remove(tokenId);
        }

        private void RegisterDiagnosticsSnapshotProviders()
        {
            if (_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RegisterSnapshotProvider("Input.Enabled", () => InputEnabled.ToString());
            diagnostics.RegisterSnapshotProvider("Input.EnabledContexts", () => EnabledContexts.ToString());
            diagnostics.RegisterSnapshotProvider("Input.BlockCount", () => BlockCount.ToString());
            diagnostics.RegisterSnapshotProvider("Input.BlockedContexts", () => GetBlockedContexts().ToString());
            diagnostics.RegisterSnapshotProvider("Input.BindingCount", () => BindingCount.ToString());
            diagnostics.RegisterSnapshotProvider("Input.Backend", () => Backend?.GetType().Name ?? string.Empty);
            _diagnosticsRegistered = true;
        }

        private void RemoveDiagnosticsSnapshotProviders()
        {
            if (!_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RemoveSnapshotProvider("Input.Enabled");
            diagnostics.RemoveSnapshotProvider("Input.EnabledContexts");
            diagnostics.RemoveSnapshotProvider("Input.BlockCount");
            diagnostics.RemoveSnapshotProvider("Input.BlockedContexts");
            diagnostics.RemoveSnapshotProvider("Input.BindingCount");
            diagnostics.RemoveSnapshotProvider("Input.Backend");
            _diagnosticsRegistered = false;
        }

        private InputContext GetBlockedContexts()
        {
            var blocked = InputContext.None;
            foreach (var blockContext in _blockContexts.Values)
            {
                blocked |= blockContext;
            }

            return blocked;
        }

        private static InputBindingData CloneBinding(InputBindingData binding)
        {
            if (binding == null)
            {
                return null;
            }

            return new InputBindingData
            {
                ActionName = binding.ActionName,
                Key = binding.Key,
                Context = binding.Context
            };
        }

        private static string NormalizeActionName(string actionName)
        {
            return string.IsNullOrWhiteSpace(actionName) ? string.Empty : actionName.Trim();
        }

        private static void ValidateActionName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                throw new ArgumentException("Action name can not be empty.", nameof(actionName));
            }

            var segments = actionName.Split('.');
            if (segments.Length < 2)
            {
                throw new ArgumentException($"Action name '{actionName}' must follow the 'Domain.ActionName' convention.", nameof(actionName));
            }

            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (string.IsNullOrWhiteSpace(segment) || !char.IsLetter(segment[0]))
                {
                    throw new ArgumentException($"Action name '{actionName}' contains an invalid segment '{segment}'.", nameof(actionName));
                }
            }
        }
    }
}

