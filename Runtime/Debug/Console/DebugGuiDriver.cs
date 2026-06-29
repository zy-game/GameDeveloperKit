using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Timer;
using UnityEngine;

namespace GameDeveloperKit.Debugger
{
    internal sealed class DebugGuiDriver : MonoBehaviour
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const float MinScale = 0.85f;
        private const float MaxScale = 2f;
        private const float OverlayButtonWidth = 112f;
        private const float OverlayButtonHeight = 36f;
        private const float OverlayButtonMargin = 12f;
        private const string DefaultSkinResourcePath = "DefaultGUISkin";

        private static readonly string[] GuiTabs =
        {
            "Logs",
            "Profiles",
            "Timers",
            "Tools",
            "Settings",
        };
        private DebugModule m_Module;
        private string m_CommandLine = string.Empty;
        private string m_CommandMessage = string.Empty;
        private Vector2 m_LogScroll;
        private Vector2 m_ProfileScroll;
        private Vector2 m_TimerScroll;
        private Vector2 m_ToolScroll;
        private Vector2 m_SettingsScroll;
        private GUIStyle m_WindowStyle;
        private GUIStyle m_ToolbarStyle;
        private GUIStyle m_TitleStyle;
        private GUIStyle m_CloseButtonStyle;
        private GUIStyle m_OverlayButtonStyle;
        private GUISkin m_StyleSkin;
        private GUISkin m_DefaultSkin;
        private bool m_DefaultSkinLoaded;

        /// <summary>
        /// 初始化 member。
        /// </summary>
        public void Initialize(DebugModule module)
        {
            m_Module = module;
        }

        /// <summary>
        /// 绘制 Gui。
        /// </summary>
        public void DrawGui()
        {
            if (m_Module == null || !m_Module.Enabled || (!m_Module.Settings.ConsoleEnabled && !m_Module.ConsoleVisible))
            {
                return;
            }

            var previousMatrix = GUI.matrix;
            var previousSkin = GUI.skin;
            var scale = GetGuiScale();
            var skin = GetDefaultSkin();
            if (skin != null)
            {
                GUI.skin = skin;
            }

            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            try
            {
                EnsureStylesForCurrentSkin();
                var width = Mathf.Max(1f, Screen.width / scale);
                var height = Mathf.Max(1f, Screen.height / scale);
                if (!m_Module.ConsoleVisible)
                {
                    DrawCollapsedButton(width);
                    return;
                }

                GUILayout.BeginArea(new Rect(0f, 0f, width, height), WindowStyle());
                GUILayout.BeginHorizontal(ToolbarStyle(), GUILayout.Height(40f));
                GUILayout.Label("Debug Console", TitleStyle(), GUILayout.Width(150f));
                m_Module.Console.SelectedTab = GUILayout.Toolbar(m_Module.Console.SelectedTab, GuiTabs, GUILayout.Height(30f));
                if (GUILayout.Button("Close", CloseButtonStyle(), GUILayout.Width(78f), GUILayout.Height(30f)))
                {
                    m_Module.ConsoleVisible = false;
                    GUILayout.EndHorizontal();
                    GUILayout.EndArea();
                    return;
                }

                GUILayout.EndHorizontal();
                switch (m_Module.Console.SelectedTab)
                {
                    case 0:
                        DrawLogsTab();
                        break;
                    case 1:
                        DrawProfilesTab();
                        break;
                    case 2:
                        DrawTimersTab();
                        break;
                    case 3:
                        DrawToolsTab();
                        break;
                    case 4:
                        DrawSettingsTab();
                        break;
                }

                GUILayout.EndArea();
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.skin = previousSkin;
            }
        }

        /// <summary>
        /// Unity OnGUI 回调。
        /// </summary>
        private void OnGUI()
        {
            m_Module?.DrawGui();
        }

        /// <summary>
        /// 绘制 Logs Tab。
        /// </summary>
        private void DrawLogsTab()
        {
            m_LogScroll = GUILayout.BeginScrollView(m_LogScroll);
            foreach (var entry in m_Module.Logs.Snapshot())
            {
                GUILayout.Label($"#{entry.Sequence} F{entry.FrameCount} T{entry.TimerTick} [{entry.Level}] [{entry.Category}] {entry.Message}");
            }

            GUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制 Profiles Tab。
        /// </summary>
        private void DrawProfilesTab()
        {
            m_ProfileScroll = GUILayout.BeginScrollView(m_ProfileScroll);
            var profiles = m_Module.Profiles.Snapshot();
            if (profiles.Count == 0)
            {
                GUILayout.Label("No profiles registered.");
                GUILayout.EndScrollView();
                return;
            }

            m_Module.Profiles.Draw();
            GUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制 Timers Tab。
        /// </summary>
        private void DrawTimersTab()
        {
            m_TimerScroll = GUILayout.BeginScrollView(m_TimerScroll);
            if (!App.TryGetRegistered<TimerModule>(out var timer))
            {
                GUILayout.Label("TimerModule is not registered.");
                GUILayout.EndScrollView();
                return;
            }

            var snapshot = timer.Snapshot();
            GUILayout.Label($"Tick: {snapshot.Tick}");
            GUILayout.Label($"Time: {snapshot.Time:0.000}s");
            GUILayout.Label($"Delta: {snapshot.DeltaTime:0.000}s / Unscaled: {snapshot.UnscaledDeltaTime:0.000}s");
            DrawDelayTimerHandles(snapshot.Delays);
            DrawCountdownTimerHandles(snapshot.Countdowns);
            DrawIntervalTimerHandles(snapshot.Intervals);
            DrawUpdateTimerHandles(snapshot.Updates);
            GUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制 Delay Timer Handles。
        /// </summary>
        private static void DrawDelayTimerHandles(IReadOnlyList<TimerDelayHandle> handles)
        {
            GUILayout.Label($"Delay: {handles.Count}");
            foreach (var handle in handles)
            {
                GUILayout.Label($"{handle.Tag ?? string.Empty} remaining={handle.Remaining:0.000}s progress={handle.Progress:0.00} next={handle.NextFireTime:0.000} paused={handle.IsPaused}");
            }
        }

        /// <summary>
        /// 绘制 Countdown Timer Handles。
        /// </summary>
        private static void DrawCountdownTimerHandles(IReadOnlyList<TimerCountdownHandle> handles)
        {
            GUILayout.Label($"Countdown: {handles.Count}");
            foreach (var handle in handles)
            {
                GUILayout.Label($"{handle.Tag ?? string.Empty} remaining={handle.Remaining:0.000}s progress={handle.Progress:0.00} next={handle.NextFireTime:0.000} paused={handle.IsPaused}");
            }
        }

        /// <summary>
        /// 绘制 Interval Timer Handles。
        /// </summary>
        private static void DrawIntervalTimerHandles(IReadOnlyList<TimerIntervalHandle> handles)
        {
            GUILayout.Label($"Interval: {handles.Count}");
            foreach (var handle in handles)
            {
                GUILayout.Label($"{handle.Tag ?? string.Empty} remaining={handle.Remaining:0.000}s progress={handle.Progress:0.00} next={handle.NextFireTime:0.000} paused={handle.IsPaused}");
            }
        }

        /// <summary>
        /// 绘制 Update Timer Handles。
        /// </summary>
        private static void DrawUpdateTimerHandles(IReadOnlyList<TimerUpdateHandle> handles)
        {
            GUILayout.Label($"Update: {handles.Count}");
            foreach (var handle in handles)
            {
                var error = handle.HasError ? handle.LastException.Message : "none";
                GUILayout.Label($"{handle.Tag ?? string.Empty} kind={handle.TickKind} enabled={handle.Enabled} last={handle.LastTick} paused={handle.IsPaused} error={error}");
            }
        }

        /// <summary>
        /// 绘制 Tools Tab。
        /// </summary>
        private void DrawToolsTab()
        {
            m_ToolScroll = GUILayout.BeginScrollView(m_ToolScroll);
            DrawCommandTab();
            GUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制 Settings Tab。
        /// </summary>
        private void DrawSettingsTab()
        {
            m_SettingsScroll = GUILayout.BeginScrollView(m_SettingsScroll);
            GUILayout.Label($"Enabled: {m_Module.Enabled}");
            GUILayout.Label($"Minimum Level: {m_Module.MinimumLevel}");
            GUILayout.Label($"Console: {m_Module.ConsoleVisible}");
            GUILayout.Label($"Console Enabled: {m_Module.Settings.ConsoleEnabled}");
            GUILayout.Label($"Unity Log Capture: {m_Module.Settings.UnityLogCaptureEnabled}");
            GUILayout.Label($"Command: {m_Module.Settings.CommandEnabled}");
            GUILayout.Label($"Redaction: {m_Module.Settings.RedactionEnabled}");
            GUILayout.Label($"Log Capacity: {m_Module.Settings.LogCapacity}");
            GUILayout.Label($"FPS: {m_Module.Metrics.Fps:0.0}");
            GUILayout.Label($"Frame: {m_Module.Metrics.FrameTimeMs:0.00}ms");
            GUILayout.Label($"Managed: {m_Module.Metrics.ManagedMemoryBytes / 1024f / 1024f:0.0}MB");
            GUILayout.Label(m_Module.Metrics.GraphicsMemoryBytes.HasValue
                ? $"Graphics: {m_Module.Metrics.GraphicsMemoryBytes.Value / 1024f / 1024f:0.0}MB"
                : "Graphics: unavailable");
            GUILayout.Label(m_Module.Metrics.GpuFrameTimeMs.HasValue
                ? $"GPU Frame: {m_Module.Metrics.GpuFrameTimeMs.Value:0.00}ms"
                : "GPU Frame: unavailable");
            GUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制 Command Tab。
        /// </summary>
        private void DrawCommandTab()
        {
            GUILayout.Label(m_Module.Settings.CommandEnabled ? "Command input enabled." : "Command input disabled.");
            GUILayout.BeginHorizontal();
            m_CommandLine = GUILayout.TextField(m_CommandLine);
            if (GUILayout.Button("Run", GUILayout.Width(60f)))
            {
                ExecuteGuiCommandAsync(m_CommandLine).Forget();
            }

            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(m_CommandMessage))
            {
                GUILayout.Label(m_CommandMessage);
            }
        }

        /// <summary>
        /// 执行 Execute Gui Command Async。
        /// </summary>
        /// <param name="commandLine">command Line 参数。</param>
        private async UniTaskVoid ExecuteGuiCommandAsync(string commandLine)
        {
            var result = await m_Module.ExecuteCommandAsync(commandLine);
            m_CommandMessage = result.Message;
            m_Module.Info(result.Message, "Command");
        }

        /// <summary>
        /// 确保 Styles For Current Skin。
        /// </summary>
        private void EnsureStylesForCurrentSkin()
        {
            if (m_StyleSkin == GUI.skin)
            {
                return;
            }

            m_StyleSkin = GUI.skin;
            m_WindowStyle = null;
            m_ToolbarStyle = null;
            m_TitleStyle = null;
            m_CloseButtonStyle = null;
            m_OverlayButtonStyle = null;
        }

        /// <summary>
        /// 获取 Default Skin。
        /// </summary>
        private GUISkin GetDefaultSkin()
        {
            if (!m_DefaultSkinLoaded)
            {
                m_DefaultSkin = Resources.Load<GUISkin>(DefaultSkinResourcePath);
                m_DefaultSkinLoaded = true;
            }

            return m_DefaultSkin;
        }

        /// <summary>
        /// 绘制 Collapsed Button。
        /// </summary>
        private void DrawCollapsedButton(float width)
        {
            var rect = new Rect(
                width - OverlayButtonWidth - OverlayButtonMargin,
                OverlayButtonMargin,
                OverlayButtonWidth,
                OverlayButtonHeight);
            if (GUI.Button(rect, $"FPS {m_Module.Metrics.Fps:0}", OverlayButtonStyle()))
            {
                m_Module.ConsoleVisible = true;
            }
        }

        /// <summary>
        /// 获取 Gui Scale。
        /// </summary>
        private static float GetGuiScale()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return 1f;
            }

            var widthScale = Screen.width / ReferenceWidth;
            var heightScale = Screen.height / ReferenceHeight;
            return Mathf.Clamp(Mathf.Min(widthScale, heightScale), MinScale, MaxScale);
        }

        /// <summary>
        /// 执行 Window Style。
        /// </summary>
        private GUIStyle WindowStyle()
        {
            if (m_WindowStyle == null)
            {
                m_WindowStyle = new GUIStyle(GUI.skin.window)
                {
                    padding = new RectOffset(10, 10, 8, 10),
                    margin = new RectOffset(0, 0, 0, 0),
                };
            }

            return m_WindowStyle;
        }

        /// <summary>
        /// 执行 Toolbar Style。
        /// </summary>
        private GUIStyle ToolbarStyle()
        {
            if (m_ToolbarStyle == null)
            {
                m_ToolbarStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(8, 8, 5, 5),
                    margin = new RectOffset(0, 0, 0, 4),
                };
            }

            return m_ToolbarStyle;
        }

        /// <summary>
        /// 执行 Title Style。
        /// </summary>
        private GUIStyle TitleStyle()
        {
            if (m_TitleStyle == null)
            {
                m_TitleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Bold,
                    clipping = TextClipping.Clip,
                };
            }

            return m_TitleStyle;
        }

        /// <summary>
        /// 执行 Close Button Style。
        /// </summary>
        private GUIStyle CloseButtonStyle()
        {
            if (m_CloseButtonStyle == null)
            {
                m_CloseButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    clipping = TextClipping.Clip,
                };
            }

            return m_CloseButtonStyle;
        }

        /// <summary>
        /// 执行 Overlay Button Style。
        /// </summary>
        private GUIStyle OverlayButtonStyle()
        {
            if (m_OverlayButtonStyle == null)
            {
                m_OverlayButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(8, 8, 4, 4),
                };
            }

            return m_OverlayButtonStyle;
        }

    }
}
