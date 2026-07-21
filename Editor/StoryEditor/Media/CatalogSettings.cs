using System;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using GameDeveloperKit.EditorConfiguration;

namespace GameDeveloperKit.StoryEditor.Media
{
    public sealed class CatalogSettings : ScriptableObject
    {
        internal const string SettingsPath = "ProjectSettings/GameDeveloperKitStoryMediaSettings.asset";
        private const int DefaultTimeoutSeconds = 15;
        private static CatalogSettings s_Instance;

        [SerializeField] private string m_CatalogApiUrl;
        [SerializeField] private string m_CdnBaseUrl;
        [SerializeField] private string m_PreviewLocale = "zh-CN";
        [SerializeField] private int m_TimeoutSeconds = DefaultTimeoutSeconds;

        public string CatalogApiUrl
        {
            get => m_CatalogApiUrl;
            set => m_CatalogApiUrl = value;
        }

        public string CdnBaseUrl
        {
            get => m_CdnBaseUrl;
            set => m_CdnBaseUrl = value;
        }

        public string PreviewLocale
        {
            get => EditorGlobalConfig.LoadOrCreate().Localization.PreviewLocale;
        }

        public int TimeoutSeconds
        {
            get => m_TimeoutSeconds;
            set => m_TimeoutSeconds = value;
        }

        public static CatalogSettings LoadOrCreate()
        {
            if (s_Instance != null)
            {
                s_Instance.EnsureDefaults();
                return s_Instance;
            }

            if (System.IO.File.Exists(SettingsPath))
            {
                s_Instance = InternalEditorUtility.LoadSerializedFileAndForget(SettingsPath)
                    .OfType<CatalogSettings>()
                    .FirstOrDefault();
            }

            if (s_Instance == null)
            {
                s_Instance = CreateInstance<CatalogSettings>();
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
            EnsureDefaults();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingsPath) ?? ".");
            InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { this }, SettingsPath, true);
        }

        public void EnsureDefaults()
        {
            m_CatalogApiUrl = m_CatalogApiUrl?.Trim() ?? string.Empty;
            m_CdnBaseUrl = m_CdnBaseUrl?.Trim() ?? string.Empty;
            m_PreviewLocale = string.IsNullOrWhiteSpace(m_PreviewLocale) ? "zh-CN" : m_PreviewLocale.Trim();
            if (m_TimeoutSeconds <= 0)
            {
                m_TimeoutSeconds = DefaultTimeoutSeconds;
            }
        }

        internal void Validate()
        {
            ValidateHttpsUrl(m_CatalogApiUrl, nameof(CatalogApiUrl));
            ValidateHttpsUrl(m_CdnBaseUrl, nameof(CdnBaseUrl));
            if (m_TimeoutSeconds <= 0)
            {
                throw new CatalogException(CatalogErrorKind.InvalidSettings, "Catalog timeout must be greater than zero.");
            }
        }

        private static void ValidateHttpsUrl(string value, string name)
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) is false ||
                string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) is false ||
                string.IsNullOrWhiteSpace(uri.Host) ||
                string.IsNullOrWhiteSpace(uri.UserInfo) is false)
            {
                throw new CatalogException(CatalogErrorKind.InvalidSettings, $"{name} must be an absolute HTTPS URL.");
            }
        }
    }

    internal sealed class CatalogSettingsProvider : SettingsProvider
    {
        private SerializedObject m_SerializedSettings;

        private CatalogSettingsProvider()
            : base("Project/GameDeveloperKit/Story Media", SettingsScope.Project)
        {
            label = "Story Media";
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new CatalogSettingsProvider
            {
                keywords = GetSearchKeywordsFromSerializedObject(new SerializedObject(CatalogSettings.LoadOrCreate()))
            };
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            m_SerializedSettings = new SerializedObject(CatalogSettings.LoadOrCreate());
        }

        public override void OnGUI(string searchContext)
        {
            m_SerializedSettings.Update();
            EditorGUILayout.PropertyField(m_SerializedSettings.FindProperty("m_CatalogApiUrl"), new GUIContent("Catalog API URL"));
            EditorGUILayout.PropertyField(m_SerializedSettings.FindProperty("m_CdnBaseUrl"), new GUIContent("CDN Base URL"));
            EditorGUILayout.PropertyField(m_SerializedSettings.FindProperty("m_TimeoutSeconds"), new GUIContent("Timeout Seconds"));
            if (m_SerializedSettings.ApplyModifiedProperties())
            {
                CatalogSettings.LoadOrCreate().SaveSettings();
            }
        }
    }
}

namespace GameDeveloperKit.LocalizationEditor
{
    public sealed class LocalizationEditorSettings : ScriptableObject
    {
        internal const string SettingsPath = "ProjectSettings/GameDeveloperKitLocalizationSettings.asset";

        [SerializeField] private string m_PreviewLocale = "zh-CN";
        [SerializeField] private string m_PreviewPackGuid;
    }
}
