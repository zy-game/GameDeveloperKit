using UnityEngine;
using System;
using System.IO;
using System.Linq;
using UnityEditorInternal;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;

namespace GameDeveloperKit.EditorConfiguration
{
    public sealed class EditorGlobalConfig : ScriptableObject
    {
        public const int CurrentVersion = EditorConfigMigration.CurrentMigrationVersion;
        public const string SettingsPath = "ProjectSettings/GameDeveloperKitEditorGlobalConfig.asset";
        public const string CacheRoot = "Library/GameDeveloperKit/EditorConfig";

        [SerializeField] private int m_Version = CurrentVersion;
        [SerializeField] private LubanProjectConfig m_Luban;
        [SerializeField] private LocalizationProjectConfig m_Localization;

        private static EditorGlobalConfig s_Instance;

        public int Version => m_Version;

        public LubanProjectConfig Luban => m_Luban;

        public LocalizationProjectConfig Localization => m_Localization;

        public static EditorGlobalConfig LoadOrCreate()
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
                    .OfType<EditorGlobalConfig>()
                    .FirstOrDefault();
                if (s_Instance == null)
                {
                    throw new InvalidDataException($"Editor global config has an unexpected type: {SettingsPath}");
                }
            }
            else
            {
                s_Instance = CreateInstance<EditorGlobalConfig>();
            }

            var sourceVersion = fileExists ? s_Instance.m_Version : 0;
            s_Instance.hideFlags = HideFlags.HideAndDontSave;
            s_Instance.EnsureDefaults();
            var migrated = EditorConfigMigration.MigrateProject(s_Instance, sourceVersion);
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
            if (TryValidate(out var error) is false)
            {
                throw new ArgumentException(error, nameof(EditorGlobalConfig));
            }

            m_Version = CurrentVersion;
            IODirectory.CreateDirectory(Path.GetDirectoryName(SettingsPath) ?? ".");
            InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { this }, SettingsPath, true);
        }

        public bool TryValidate(out string error)
        {
            EnsureDefaults();
            return EditorConfigValidation.TryNormalize(this, out error);
        }

        internal void EnsureDefaults()
        {
            m_Luban ??= new LubanProjectConfig();
            m_Localization ??= new LocalizationProjectConfig();
            m_Luban.EnsureDefaults();
            m_Localization.EnsureDefaults();
        }

        internal static void ResetInstance()
        {
            s_Instance = null;
        }
    }
}
