using System.Linq;
using UnityEngine;
using UnityEditorInternal;

namespace GameDeveloperKit.UnityBridge
{
    public sealed class UnityBridgeSettings : ScriptableObject
    {
        private const string SettingsPath = "ProjectSettings/GameDeveloperKitUnityBridgeSettings.asset";

        private static UnityBridgeSettings s_Instance;

        [SerializeField] private bool m_AutoStart = true;

        public bool AutoStart
        {
            get => m_AutoStart;
            set => m_AutoStart = value;
        }

        public static UnityBridgeSettings LoadOrCreate()
        {
            if (s_Instance != null)
            {
                s_Instance.EnsureDefaults();
                return s_Instance;
            }

            if (System.IO.File.Exists(SettingsPath))
            {
                s_Instance = InternalEditorUtility.LoadSerializedFileAndForget(SettingsPath)
                    .OfType<UnityBridgeSettings>()
                    .FirstOrDefault();
            }

            if (s_Instance == null)
            {
                s_Instance = CreateInstance<UnityBridgeSettings>();
            }

            s_Instance.hideFlags = HideFlags.HideAndDontSave;
            s_Instance.EnsureDefaults();
            if (System.IO.File.Exists(SettingsPath) is false)
            {
                s_Instance.SaveSettings();
            }

            return s_Instance;
        }

        public void SaveSettings()
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingsPath) ?? ".");
            InternalEditorUtility.SaveToSerializedFileAndForget(new Object[] { this }, SettingsPath, true);
        }

        public void EnsureDefaults()
        {
        }
    }
}
