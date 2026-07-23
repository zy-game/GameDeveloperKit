using UnityEngine;

namespace GameDeveloperKit.LocalizationEditor
{
    public sealed class LocalizationEditorSettings : ScriptableObject
    {
        internal const string SettingsPath = "ProjectSettings/GameDeveloperKitLocalizationSettings.asset";

        [SerializeField] private string m_PreviewLocale = "zh-CN";
        [SerializeField] private string m_PreviewPackGuid;
    }
}
