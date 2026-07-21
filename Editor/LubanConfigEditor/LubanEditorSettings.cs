using UnityEngine;

namespace GameDeveloperKit.LubanConfigEditor
{
    public sealed class LubanEditorSettings : ScriptableObject
    {
        internal const string SettingsPath = "ProjectSettings/GameDeveloperKitLubanEditorSettings.asset";
        public const string DefaultReleasePath = "Luban/Luban.dll";

        [SerializeField] private string m_ReleasePath = DefaultReleasePath;
    }
}
