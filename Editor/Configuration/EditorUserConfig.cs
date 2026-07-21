using UnityEngine;
using System.IO;
using System.Linq;
using UnityEditorInternal;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;

namespace GameDeveloperKit.EditorConfiguration
{
    public sealed class EditorUserConfig : ScriptableObject
    {
        public const int CurrentVersion = EditorConfigMigration.CurrentMigrationVersion;
        public const string SettingsPath = "UserSettings/GameDeveloperKitEditorUserConfig.asset";
        public const string DefaultLubanDllPath = "Luban/Luban.dll";

        [SerializeField] private int m_Version = CurrentVersion;
        [SerializeField] private string m_LubanDllPath = DefaultLubanDllPath;

        private static EditorUserConfig s_Instance;

        public int Version => m_Version;

        public string LubanDllPath
        {
            get => m_LubanDllPath;
            set => m_LubanDllPath = value;
        }

        public static EditorUserConfig LoadOrCreate()
        {
            if (s_Instance != null)
            {
                s_Instance.EnsureDefaults();
                return s_Instance;
            }

            var fileExists = IOFile.Exists(SettingsPath);
            if (fileExists)
            {
                s_Instance = InternalEditorUtility.LoadSerializedFileAndForget(SettingsPath)
                    .OfType<EditorUserConfig>()
                    .FirstOrDefault();
                if (s_Instance == null)
                {
                    throw new InvalidDataException($"Editor user config has an unexpected type: {SettingsPath}");
                }
            }
            else
            {
                s_Instance = CreateInstance<EditorUserConfig>();
            }

            var sourceVersion = fileExists ? s_Instance.m_Version : 0;
            s_Instance.hideFlags = HideFlags.HideAndDontSave;
            s_Instance.EnsureDefaults();
            var migrated = EditorConfigMigration.MigrateUser(s_Instance, sourceVersion);
            if (sourceVersion < CurrentVersion)
            {
                s_Instance.m_Version = CurrentVersion;
            }

            if (fileExists is false || migrated || sourceVersion < CurrentVersion)
            {
                s_Instance.Save();
            }

            return s_Instance;
        }

        public void Save()
        {
            EnsureDefaults();
            m_Version = CurrentVersion;
            IODirectory.CreateDirectory(Path.GetDirectoryName(SettingsPath) ?? ".");
            InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { this }, SettingsPath, true);
        }

        internal void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(m_LubanDllPath))
            {
                m_LubanDllPath = DefaultLubanDllPath;
            }

            m_LubanDllPath = m_LubanDllPath.Trim().Replace('\\', '/');
        }

        internal static void ResetInstance()
        {
            s_Instance = null;
        }
    }
}
