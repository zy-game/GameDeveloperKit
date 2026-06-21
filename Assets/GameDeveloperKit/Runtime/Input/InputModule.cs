using System;
using System.Collections.Generic;
using GameDeveloperKit.Timer;

namespace GameDeveloperKit.Input
{
    /// <summary>
    /// 输入模块，按 Timer Update 轮询输入源并更新动作状态。
    /// </summary>
    [ModuleDependency(typeof(TimerModule))]
    public sealed class InputModule : GameModuleBase
    {
        /// <summary>
        /// 存储输入源。
        /// </summary>
        private readonly IInputSource m_InputSource;
        /// <summary>
        /// 存储动作组。
        /// </summary>
        private readonly Dictionary<string, InputActionMap> m_Maps = new Dictionary<string, InputActionMap>(StringComparer.Ordinal);
        /// <summary>
        /// 存储更新句柄。
        /// </summary>
        private UpdateTimerHandle m_UpdateHandle;

        /// <summary>
        /// 初始化输入模块，使用 Unity legacy Input 输入源。
        /// </summary>
        public InputModule() : this(new UnityInputSource())
        {
        }

        /// <summary>
        /// 初始化输入模块。
        /// </summary>
        /// <param name="inputSource">输入源。</param>
        public InputModule(IInputSource inputSource)
        {
            m_InputSource = inputSource ?? throw new ArgumentNullException(nameof(inputSource));
        }

        /// <summary>
        /// 输入模块是否启用。
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 启动输入模块并注册 Timer Update。
        /// </summary>
        public override void Startup()
        {
            Enabled = true;
            if (m_UpdateHandle != null && !m_UpdateHandle.IsCancelled && !m_UpdateHandle.IsCompleted)
            {
                return;
            }

            m_UpdateHandle = App.Timer.OnUpdate(Update, this, nameof(InputModule));
        }

        /// <summary>
        /// 关闭输入模块并释放动作组。
        /// </summary>
        public override void Shutdown()
        {
            if (m_UpdateHandle != null && App.TryGetRegistered<TimerModule>(out var timer))
            {
                timer.Cancel(m_UpdateHandle);
            }

            m_UpdateHandle = null;
            ReleaseMaps();
            Enabled = false;
        }

        /// <summary>
        /// 注册输入动作组。
        /// </summary>
        /// <param name="map">输入动作组。</param>
        public void RegisterMap(InputActionMap map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (string.IsNullOrWhiteSpace(map.Name))
            {
                throw new ArgumentException("Action map name cannot be empty.", nameof(map));
            }

            if (m_Maps.ContainsKey(map.Name))
            {
                throw new GameException($"Input action map '{map.Name}' has already been registered.");
            }

            ValidateMap(map);
            m_Maps.Add(map.Name, map);
        }

        /// <summary>
        /// 注销输入动作组。
        /// </summary>
        /// <param name="mapName">动作组名称。</param>
        /// <returns>成功注销时返回 true。</returns>
        public bool UnregisterMap(string mapName)
        {
            ValidateName(mapName, nameof(mapName), "Action map name cannot be empty.");
            if (!m_Maps.TryGetValue(mapName, out var map))
            {
                return false;
            }

            m_Maps.Remove(mapName);
            map.Release();
            return true;
        }

        /// <summary>
        /// 设置输入动作组启用状态。
        /// </summary>
        /// <param name="mapName">动作组名称。</param>
        /// <param name="enabled">是否启用。</param>
        /// <returns>状态发生变化时返回 true。</returns>
        public bool SetMapEnabled(string mapName, bool enabled)
        {
            var map = GetMap(mapName);
            if (map.Enabled == enabled)
            {
                return false;
            }

            map.Enabled = enabled;
            return true;
        }

        /// <summary>
        /// 查询动作是否按住。
        /// </summary>
        /// <param name="mapName">动作组名称。</param>
        /// <param name="actionName">动作名称。</param>
        /// <returns>按住时返回 true。</returns>
        public bool IsPressed(string mapName, string actionName)
        {
            return GetAction(mapName, actionName).State.IsPressed;
        }

        /// <summary>
        /// 查询动作是否在当前帧按下。
        /// </summary>
        /// <param name="mapName">动作组名称。</param>
        /// <param name="actionName">动作名称。</param>
        /// <returns>当前帧按下时返回 true。</returns>
        public bool WasPressedThisFrame(string mapName, string actionName)
        {
            return GetAction(mapName, actionName).State.WasPressedThisFrame;
        }

        /// <summary>
        /// 查询动作是否在当前帧松开。
        /// </summary>
        /// <param name="mapName">动作组名称。</param>
        /// <param name="actionName">动作名称。</param>
        /// <returns>当前帧松开时返回 true。</returns>
        public bool WasReleasedThisFrame(string mapName, string actionName)
        {
            return GetAction(mapName, actionName).State.WasReleasedThisFrame;
        }

        /// <summary>
        /// 获取动作输入值。
        /// </summary>
        /// <param name="mapName">动作组名称。</param>
        /// <param name="actionName">动作名称。</param>
        /// <returns>动作输入值。</returns>
        public float GetValue(string mapName, string actionName)
        {
            return GetAction(mapName, actionName).State.Value;
        }

        /// <summary>
        /// 获取输入模块快照。
        /// </summary>
        /// <returns>输入模块快照。</returns>
        public InputSnapshot Snapshot()
        {
            var maps = new List<InputActionMapSnapshot>();
            foreach (var map in m_Maps.Values)
            {
                var actions = new List<InputActionSnapshot>();
                foreach (var action in map.Actions)
                {
                    actions.Add(new InputActionSnapshot(action.Name, action.State));
                }

                maps.Add(new InputActionMapSnapshot(map.Name, map.Enabled, actions));
            }

            return new InputSnapshot(Enabled, maps);
        }

        /// <summary>
        /// 获取已注册动作组。
        /// </summary>
        /// <param name="mapName">动作组名称。</param>
        /// <returns>输入动作组。</returns>
        private InputActionMap GetMap(string mapName)
        {
            ValidateName(mapName, nameof(mapName), "Action map name cannot be empty.");
            if (!m_Maps.TryGetValue(mapName, out var map))
            {
                throw new GameException($"Input action map '{mapName}' is not registered.");
            }

            return map;
        }

        /// <summary>
        /// 获取已注册动作。
        /// </summary>
        /// <param name="mapName">动作组名称。</param>
        /// <param name="actionName">动作名称。</param>
        /// <returns>输入动作。</returns>
        private InputAction GetAction(string mapName, string actionName)
        {
            var map = GetMap(mapName);
            ValidateName(actionName, nameof(actionName), "Action name cannot be empty.");
            if (!map.TryGetAction(actionName, out var action))
            {
                throw new GameException($"Input action '{actionName}' is not registered in map '{mapName}'.");
            }

            return action;
        }

        /// <summary>
        /// Timer Update 回调。
        /// </summary>
        /// <param name="context">计时器更新上下文。</param>
        private void Update(TimerUpdateContext context)
        {
            foreach (var map in m_Maps.Values)
            {
                UpdateMap(map);
            }
        }

        /// <summary>
        /// 更新动作组状态。
        /// </summary>
        /// <param name="map">输入动作组。</param>
        private void UpdateMap(InputActionMap map)
        {
            if (!Enabled || !map.Enabled)
            {
                ClearMap(map);
                return;
            }

            foreach (var action in map.Actions)
            {
                UpdateAction(action);
            }
        }

        /// <summary>
        /// 更新动作状态。
        /// </summary>
        /// <param name="action">输入动作。</param>
        private void UpdateAction(InputAction action)
        {
            var pressed = false;
            var value = 0f;
            foreach (var binding in action.Bindings)
            {
                var bindingValue = GetBindingValue(binding);
                if (Math.Abs(bindingValue) > Math.Abs(value))
                {
                    value = bindingValue;
                }

                if (Math.Abs(bindingValue) > 0.0001f)
                {
                    pressed = true;
                }
            }

            action.SetState(pressed, value);
        }

        /// <summary>
        /// 获取单个绑定的输入值。
        /// </summary>
        /// <param name="binding">输入绑定。</param>
        /// <returns>绑定输入值。</returns>
        private float GetBindingValue(InputBinding binding)
        {
            switch (binding.Kind)
            {
                case InputBindingKind.Key:
                    return m_InputSource.GetKey(binding.KeyCode) ? 1f : 0f;
                case InputBindingKind.MouseButton:
                    return m_InputSource.GetMouseButton(binding.MouseButtonIndex) ? 1f : 0f;
                case InputBindingKind.Axis:
                    return m_InputSource.GetAxisRaw(binding.AxisName) * binding.Scale;
                default:
                    throw new ArgumentException("Input binding kind is not supported.", nameof(binding));
            }
        }

        /// <summary>
        /// 清空动作组状态。
        /// </summary>
        /// <param name="map">输入动作组。</param>
        private static void ClearMap(InputActionMap map)
        {
            foreach (var action in map.Actions)
            {
                action.ClearState();
            }
        }

        /// <summary>
        /// 校验动作组。
        /// </summary>
        /// <param name="map">输入动作组。</param>
        private static void ValidateMap(InputActionMap map)
        {
            foreach (var action in map.Actions)
            {
                if (action == null)
                {
                    throw new ArgumentNullException(nameof(map), "Input action cannot be null.");
                }

                ValidateName(action.Name, nameof(action), "Action name cannot be empty.");
                foreach (var binding in action.Bindings)
                {
                    ValidateBinding(binding);
                }
            }
        }

        /// <summary>
        /// 校验输入绑定。
        /// </summary>
        /// <param name="binding">输入绑定。</param>
        private static void ValidateBinding(InputBinding binding)
        {
            switch (binding.Kind)
            {
                case InputBindingKind.Key:
                case InputBindingKind.MouseButton when binding.MouseButtonIndex >= 0:
                    return;
                case InputBindingKind.MouseButton:
                    throw new ArgumentException("Mouse button cannot be negative.", nameof(binding));
                case InputBindingKind.Axis:
                    ValidateName(binding.AxisName, nameof(binding), "Axis name cannot be empty.");
                    return;
                default:
                    throw new ArgumentException("Input binding kind is not supported.", nameof(binding));
            }
        }

        /// <summary>
        /// 释放所有动作组。
        /// </summary>
        private void ReleaseMaps()
        {
            foreach (var map in m_Maps.Values)
            {
                map.Release();
            }

            m_Maps.Clear();
        }

        /// <summary>
        /// 校验名称。
        /// </summary>
        /// <param name="value">名称值。</param>
        /// <param name="parameterName">参数名。</param>
        /// <param name="emptyMessage">空白名称异常消息。</param>
        private static void ValidateName(string value, string parameterName, string emptyMessage)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(emptyMessage, parameterName);
            }
        }
    }
}
