using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using IOFile = System.IO.File;

namespace GameDeveloperKit.LocalizationEditor
{
    internal static class LocalizationKeyUsageScanner
    {
        private static readonly HashSet<string> s_SerializedExtensions = new HashSet<string>(
            new[] { ".asset", ".prefab", ".unity" },
            StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<string> Find(string key)
        {
            key = key?.Trim() ?? string.Empty;
            if (key.Length == 0)
            {
                return Array.Empty<string>();
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) &&
                               s_SerializedExtensions.Contains(Path.GetExtension(path)))
                .Where(path => ContainsKey(Path.Combine(projectRoot, path), key))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool ContainsKey(string path, string key)
        {
            try
            {
                return IOFile.ReadAllText(path).IndexOf(key, StringComparison.Ordinal) >= 0;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
