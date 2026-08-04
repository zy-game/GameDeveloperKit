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
        [SerializeField] private UiPrefabStudioProjectConfig m_UiPrefabStudio;
        [SerializeField] private LubanProjectConfig m_Luban;
        [SerializeField] private LocalizationProjectConfig m_Localization;
        [SerializeField] private StoryMediaProjectConfig m_StoryMedia;
        [SerializeField] private CloudProjectConfig m_Cloud;

        private static EditorGlobalConfig s_Instance;

        public int Version => m_Version;

        public UiPrefabStudioProjectConfig UiPrefabStudio => m_UiPrefabStudio;

        public LubanProjectConfig Luban => m_Luban;

        public LocalizationProjectConfig Localization => m_Localization;

        public StoryMediaProjectConfig StoryMedia => m_StoryMedia;

        public CloudProjectConfig Cloud => m_Cloud;

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
            m_UiPrefabStudio ??= new UiPrefabStudioProjectConfig();
            m_Luban ??= new LubanProjectConfig();
            m_Localization ??= new LocalizationProjectConfig();
            m_StoryMedia ??= new StoryMediaProjectConfig();
            m_Cloud ??= new CloudProjectConfig();
            m_UiPrefabStudio.EnsureDefaults();
            m_Luban.EnsureDefaults();
            m_Localization.EnsureDefaults();
            m_StoryMedia.EnsureDefaults();
            m_Cloud.EnsureDefaults();
        }

        internal static void ResetInstance()
        {
            s_Instance = null;
        }
    }
}
