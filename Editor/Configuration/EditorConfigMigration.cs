using System;
using GameDeveloperKit.LubanConfigEditor;
using UnityEngine;
using IOFile = System.IO.File;

namespace GameDeveloperKit.EditorConfiguration
{
    internal static class EditorConfigMigration
    {
        public const int CurrentMigrationVersion = 3;

        public static bool MigrateProject(EditorGlobalConfig config, int sourceVersion)
        {
            return false;
        }

        public static bool MigrateUser(EditorUserConfig config, int sourceVersion)
        {
            if (sourceVersion >= CurrentMigrationVersion ||
                string.Equals(
                    config.LubanDllPath,
                    EditorUserConfig.DefaultLubanDllPath,
                    StringComparison.Ordinal) is false)
            {
                return false;
            }

            var releasePath = TryLoadString(
                LubanEditorSettings.SettingsPath,
                "m_ReleasePath");
            if (string.IsNullOrWhiteSpace(releasePath))
            {
                return false;
            }

            config.LubanDllPath = releasePath.Trim();
            return true;
        }

        private static string TryLoadString(string path, string propertyName)
        {
            if (IOFile.Exists(path) is false)
            {
                return null;
            }

            try
            {
                var prefix = propertyName + ":";
                foreach (var line in IOFile.ReadLines(path))
                {
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith(prefix, StringComparison.Ordinal) is false)
                    {
                        continue;
                    }

                    return ParseYamlScalar(trimmed.Substring(prefix.Length).Trim());
                }

                Debug.LogWarning($"读取旧 Editor 配置失败，已保留新配置默认值：{path}。未找到字段 {propertyName}。");
                return null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"读取旧 Editor 配置失败，已保留新配置默认值：{path}。{exception.Message}");
                return null;
            }
        }

        private static string ParseYamlScalar(string value)
        {
            if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) || value == "~")
            {
                return string.Empty;
            }

            if (value.Length >= 2 && value[0] == '\'' && value[value.Length - 1] == '\'')
            {
                return value.Substring(1, value.Length - 2).Replace("''", "'");
            }

            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                return value.Substring(1, value.Length - 2)
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");
            }

            return value;
        }
    }
}
