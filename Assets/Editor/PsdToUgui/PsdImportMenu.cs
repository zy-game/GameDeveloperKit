using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.PsdToUgui
{
    /// <summary>
    /// PSD 导入菜单
    /// </summary>
    public static class PsdImportMenu
    {
        [MenuItem("GameDeveloperKit/导入 PSD", false, 100)]
        public static void ImportPsd()
        {
            var settings = PsdToUguiSettings.Instance;
            var path = EditorUtility.OpenFilePanel("选择 PSD 文件", 
                settings.LastImportPath, "psd");
            
            if (string.IsNullOrEmpty(path)) return;
            
            settings.LastImportPath = Path.GetDirectoryName(path);
            settings.Save();
            
            PsdImporter.Import(path, settings);
        }

        [MenuItem("GameDeveloperKit/PSD 导入设置", false, 101)]
        public static void OpenSettings()
        {
            PsdToUguiSettingsWindow.ShowWindow();
        }
    }
}
