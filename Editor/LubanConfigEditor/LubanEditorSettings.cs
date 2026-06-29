using System.Collections.Generic;
using System.Linq;
using UnityEditorInternal;
using UnityEngine;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace GameDeveloperKit.LubanConfigEditor
{
    /// <summary>
    /// 定义 Luban Editor Settings 类型。
    /// </summary>
    public sealed class LubanEditorSettings : ScriptableObject
    {
        /// <summary>
        /// 定义 Settings Path 常量。
        /// </summary>
        private const string SettingsPath = "ProjectSettings/GameDeveloperKitLubanEditorSettings.asset";
        /// <summary>
        /// 定义 Default Release Path 常量。
        /// </summary>
        public const string DefaultReleasePath = "Luban/Luban.dll";

        /// <summary>
        /// 存储 Instance。
        /// </summary>
        private static LubanEditorSettings s_Instance;

        [SerializeField] private string m_ReleasePath = DefaultReleasePath;

        [SerializeField] private List<LubanWorkspaceProfile> m_Workspaces;

        [SerializeField] private List<LubanGenerationProfile> m_GenerationProfiles;

        [SerializeField] private int m_SelectedWorkspaceIndex;

        [SerializeField] private int m_SelectedGenerationProfileIndex;

        public string ReleasePath
        {
            get => m_ReleasePath;
            set => m_ReleasePath = value;
        }

        /// <summary>
        /// 存储 Workspaces。
        /// </summary>
        public List<LubanWorkspaceProfile> Workspaces => m_Workspaces;

        /// <summary>
        /// 存储 Generation Profiles。
        /// </summary>
        public List<LubanGenerationProfile> GenerationProfiles => m_GenerationProfiles;

        public int SelectedWorkspaceIndex
        {
            get => m_SelectedWorkspaceIndex;
            set => m_SelectedWorkspaceIndex = value;
        }

        public int SelectedGenerationProfileIndex
        {
            get => m_SelectedGenerationProfileIndex;
            set => m_SelectedGenerationProfileIndex = value;
        }

        /// <summary>
        /// 加载 Or Create。
        /// </summary>
        /// <returns>执行结果。</returns>
        public static LubanEditorSettings LoadOrCreate()
        {
            if (s_Instance != null)
            {
                s_Instance.EnsureDefaults();
                return s_Instance;
            }

            if (IOFile.Exists(SettingsPath))
            {
                s_Instance = InternalEditorUtility.LoadSerializedFileAndForget(SettingsPath)
                    .OfType<LubanEditorSettings>()
                    .FirstOrDefault();
            }

            if (s_Instance == null)
            {
                s_Instance = CreateInstance<LubanEditorSettings>();
            }

            s_Instance.hideFlags = HideFlags.HideAndDontSave;
            s_Instance.EnsureDefaults();
            if (IOFile.Exists(SettingsPath) is false)
            {
                s_Instance.SaveSettings();
            }

            return s_Instance;
        }

        /// <summary>
        /// 保存 Settings。
        /// </summary>
        public void SaveSettings()
        {
            EnsureDefaults();
            IODirectory.CreateDirectory(IOPath.GetDirectoryName(SettingsPath) ?? ".");
            InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { this }, SettingsPath, true);
        }

        /// <summary>
        /// 确保 Defaults。
        /// </summary>
        public void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(m_ReleasePath))
            {
                m_ReleasePath = DefaultReleasePath;
            }

            m_Workspaces ??= new List<LubanWorkspaceProfile>();
            m_GenerationProfiles ??= new List<LubanGenerationProfile>();

            foreach (var workspace in m_Workspaces)
            {
                workspace?.EnsureDefaults();
            }

            foreach (var generationProfile in m_GenerationProfiles)
            {
                generationProfile?.EnsureDefaults();
            }

            if (m_Workspaces.Count == 0)
            {
                m_SelectedWorkspaceIndex = -1;
            }
            else if (m_SelectedWorkspaceIndex < 0 || m_SelectedWorkspaceIndex >= m_Workspaces.Count)
            {
                m_SelectedWorkspaceIndex = 0;
            }

            if (m_GenerationProfiles.Count == 0)
            {
                m_SelectedGenerationProfileIndex = -1;
            }
            else if (m_SelectedGenerationProfileIndex < 0 || m_SelectedGenerationProfileIndex >= m_GenerationProfiles.Count)
            {
                m_SelectedGenerationProfileIndex = 0;
            }
        }
    }
}
