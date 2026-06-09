using UnityEngine;

namespace GameDeveloperKit.Logger
{
    public sealed class DeviceInfoProfileHandle : ProfileHandle
    {
        public override string Name => "Device Info";

        public void Refresh()
        {
        }

        protected internal override void Draw()
        {
            DrawRow("Platform", Application.platform.ToString());
            DrawRow("Unity", Application.unityVersion);
            DrawRow("Device Model", UnavailableIfEmpty(SystemInfo.deviceModel));
            DrawRow("Device Type", SystemInfo.deviceType.ToString());
            DrawRow("OS", UnavailableIfEmpty(SystemInfo.operatingSystem));
            DrawRow("CPU", UnavailableIfEmpty(SystemInfo.processorType));
            DrawRow("CPU Count", SystemInfo.processorCount.ToString());
            DrawRow("System Memory", $"{SystemInfo.systemMemorySize}MB");
            DrawRow("Graphics Device", UnavailableIfEmpty(SystemInfo.graphicsDeviceName));
            DrawRow("Graphics API", SystemInfo.graphicsDeviceType.ToString());
            DrawRow("Graphics Version", UnavailableIfEmpty(SystemInfo.graphicsDeviceVersion));
            DrawRow("Graphics Memory", $"{SystemInfo.graphicsMemorySize}MB");
            DrawRow("Compute Shaders", SystemInfo.supportsComputeShaders.ToString());
            DrawRow("Screen", $"{Screen.width}x{Screen.height}");
        }

        private static void DrawRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(150f));
            GUILayout.Label(value);
            GUILayout.EndHorizontal();
        }

        private static string UnavailableIfEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unavailable" : value;
        }
    }
}
