using UnityEngine;

namespace GameDeveloperKit.Log
{
    /// <summary>
    /// 性能面板 - IMGUI 版本
    /// </summary>
    public class ProfilePanel : IDebugPanelIMGUI
    {
        public string Name => "Profile";
        public int Order => 20;

        private readonly ProfilerCollector _profiler;
        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _headerStyle;
        private bool _stylesInitialized;

        public ProfilePanel(ProfilerCollector profiler)
        {
            _profiler = profiler;
        }

        public void OnGUI()
        {
            InitStyles();

            if (_profiler == null)
            {
                GUILayout.Label("Profiler not available");
                return;
            }

            // FPS Section
            GUILayout.Label("Performance", _headerStyle);
            GUILayout.Space(5);

            DrawRow("FPS", $"{_profiler.CurrentFPS:F1}");
            DrawRow("Avg FPS", $"{_profiler.AvgFPS:F1}");
            DrawRow("Min FPS", $"{_profiler.MinFPS:F1}");
            DrawRow("Max FPS", $"{_profiler.MaxFPS:F1}");

            GUILayout.Space(15);

            // Memory Section
            GUILayout.Label("Memory", _headerStyle);
            GUILayout.Space(5);

            DrawRow("Used Memory", $"{_profiler.UsedMemoryMB:F1} MB");
            DrawRow("Reserved Memory", $"{_profiler.ReservedMemoryMB:F1} MB");
            DrawRow("Mono Heap", $"{_profiler.MonoHeapMB:F1} MB");
            DrawRow("Mono Used", $"{_profiler.MonoUsedMB:F1} MB");

            GUILayout.Space(15);

            if (GUILayout.Button("Reset Stats", GUILayout.Width(100)))
            {
                _profiler.Reset();
            }
        }

        private void DrawRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle, GUILayout.Width(150));
            GUILayout.Label(value, _valueStyle);
            GUILayout.EndHorizontal();
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024f * 1024f):F1} MB";
            return $"{bytes / (1024f * 1024f * 1024f):F2} GB";
        }

        private string FormatNumber(long num)
        {
            if (num < 1000) return num.ToString();
            if (num < 1000000) return $"{num / 1000f:F1}K";
            return $"{num / 1000000f:F2}M";
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.4f, 0.8f, 1f) }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            _valueStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white },
                fontStyle = FontStyle.Bold
            };
        }
    }
}
