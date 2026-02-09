using UnityEngine;

namespace GameDeveloperKit.Log
{
    /// <summary>
    /// 设备信息面板 - IMGUI 版本
    /// </summary>
    public class DevicePanel : IDebugPanelIMGUI
    {
        public string Name => "Device";
        public int Order => 30;

        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private bool _stylesInitialized;

        public void OnGUI()
        {
            InitStyles();

            // Device Info
            GUILayout.Label("Device", _headerStyle);
            GUILayout.Space(5);
            DrawRow("Device Model", SystemInfo.deviceModel);
            DrawRow("Device Name", SystemInfo.deviceName);
            DrawRow("Device Type", SystemInfo.deviceType.ToString());
            DrawRow("OS", SystemInfo.operatingSystem);

            GUILayout.Space(15);

            // CPU Info
            GUILayout.Label("CPU", _headerStyle);
            GUILayout.Space(5);
            DrawRow("Processor", SystemInfo.processorType);
            DrawRow("Cores", SystemInfo.processorCount.ToString());
            DrawRow("Frequency", $"{SystemInfo.processorFrequency} MHz");

            GUILayout.Space(15);

            // GPU Info
            GUILayout.Label("GPU", _headerStyle);
            GUILayout.Space(5);
            DrawRow("Graphics Device", SystemInfo.graphicsDeviceName);
            DrawRow("Graphics Vendor", SystemInfo.graphicsDeviceVendor);
            DrawRow("Graphics Memory", $"{SystemInfo.graphicsMemorySize} MB");
            DrawRow("Graphics API", SystemInfo.graphicsDeviceType.ToString());

            GUILayout.Space(15);

            // Memory Info
            GUILayout.Label("Memory", _headerStyle);
            GUILayout.Space(5);
            DrawRow("System Memory", $"{SystemInfo.systemMemorySize} MB");

            GUILayout.Space(15);

            // Screen Info
            GUILayout.Label("Screen", _headerStyle);
            GUILayout.Space(5);
            DrawRow("Resolution", $"{Screen.width} x {Screen.height}");
            DrawRow("DPI", Screen.dpi.ToString("F0"));
            DrawRow("Fullscreen", Screen.fullScreen.ToString());
            DrawRow("Orientation", Screen.orientation.ToString());

            GUILayout.Space(15);

            // Application Info
            GUILayout.Label("Application", _headerStyle);
            GUILayout.Space(5);
            DrawRow("Unity Version", Application.unityVersion);
            DrawRow("Platform", Application.platform.ToString());
            DrawRow("Target Frame Rate", Application.targetFrameRate.ToString());
            DrawRow("Run In Background", Application.runInBackground.ToString());
        }

        private void DrawRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle, GUILayout.Width(150));
            GUILayout.Label(value, _valueStyle);
            GUILayout.EndHorizontal();
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
                normal = { textColor = Color.white }
            };
        }
    }
}
