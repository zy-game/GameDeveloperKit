using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.ResourceEditor
{
    [FilePath("ProjectSettings/GameDeveloperKitResourceEditorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class ResourceEditorSettings : ScriptableSingleton<ResourceEditorSettings>
    {
        private const string SettingsPath = "ProjectSettings/GameDeveloperKitResourceEditorSettings.asset";

        [SerializeField] private List<ResourceEditorPackage> m_Packages = new List<ResourceEditorPackage>();

        [SerializeField] private string m_ManifestOutputPath = "Assets/StreamingAssets/manifest.json";

        [SerializeField] private int m_SelectedPackageIndex = -1;

        public List<ResourceEditorPackage> Packages => m_Packages;

        public string ManifestOutputPath
        {
            get => m_ManifestOutputPath;
            set => m_ManifestOutputPath = value;
        }

        public int SelectedPackageIndex
        {
            get => m_SelectedPackageIndex;
            set => m_SelectedPackageIndex = value;
        }

        public static ResourceEditorSettings LoadOrCreate()
        {
            var exists = System.IO.File.Exists(SettingsPath);
            var settings = instance;
            settings.EnsureDefaults();
            if (exists is false)
            {
                settings.Save(true);
            }

            return settings;
        }

        public void SaveSettings()
        {
            EnsureDefaults();
            Save(true);
        }

        public void EnsureDefaults()
        {
            m_Packages ??= new List<ResourceEditorPackage>();

            if (string.IsNullOrWhiteSpace(m_ManifestOutputPath))
            {
                m_ManifestOutputPath = "Assets/StreamingAssets/manifest.json";
            }

            if (m_Packages.Count == 0)
            {
                m_SelectedPackageIndex = -1;
                return;
            }

            if (m_SelectedPackageIndex < 0 || m_SelectedPackageIndex >= m_Packages.Count)
            {
                m_SelectedPackageIndex = 0;
            }

            foreach (var package in m_Packages)
            {
                package?.EnsureDefaults();
            }
        }
    }

    [Serializable]
    public sealed class ResourceEditorPackage
    {
        [SerializeField] private string m_Name = "NewPackage";

        [SerializeField] private string m_Version = "1.0.0";

        [SerializeField] private bool m_IsHotUpdate;

        [SerializeField] private string m_CollectorId;

        [SerializeField] private string m_BuildStrategyId;

        [SerializeField] private List<ResourceEditorBundle> m_Bundles = new List<ResourceEditorBundle>();

        public string Name
        {
            get => m_Name;
            set => m_Name = value;
        }

        public string Version
        {
            get => m_Version;
            set => m_Version = value;
        }

        public bool IsHotUpdate
        {
            get => m_IsHotUpdate;
            set => m_IsHotUpdate = value;
        }

        public string CollectorId
        {
            get => m_CollectorId;
            set => m_CollectorId = value;
        }

        public string BuildStrategyId
        {
            get => m_BuildStrategyId;
            set => m_BuildStrategyId = value;
        }

        public List<ResourceEditorBundle> Bundles => m_Bundles;

        public void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(m_Name))
            {
                m_Name = "NewPackage";
            }

            if (string.IsNullOrWhiteSpace(m_Version))
            {
                m_Version = "1.0.0";
            }

            m_Bundles ??= new List<ResourceEditorBundle>();
            foreach (var bundle in m_Bundles)
            {
                bundle?.EnsureDefaults();
            }
        }
    }

    [Serializable]
    public sealed class ResourceEditorBundle
    {
        [SerializeField] private string m_Name = "NewBundle";

        [SerializeField] private string m_Group = "Default";

        [SerializeField] private List<string> m_Dependencies = new List<string>();

        [SerializeField] private List<string> m_Labels = new List<string>();

        [SerializeField] private List<string> m_AssetPaths = new List<string>();

        [SerializeField] private string m_CollectorParameter;

        public string Name
        {
            get => m_Name;
            set => m_Name = value;
        }

        public string Group
        {
            get => m_Group;
            set => m_Group = value;
        }

        public List<string> Dependencies => m_Dependencies;

        public List<string> Labels => m_Labels;

        public List<string> AssetPaths => m_AssetPaths;

        public string CollectorParameter
        {
            get => m_CollectorParameter;
            set => m_CollectorParameter = value;
        }

        public void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(m_Name))
            {
                m_Name = "NewBundle";
            }

            if (string.IsNullOrWhiteSpace(m_Group))
            {
                m_Group = "Default";
            }

            m_Dependencies ??= new List<string>();
            m_Labels ??= new List<string>();
            m_AssetPaths ??= new List<string>();
        }
    }
}