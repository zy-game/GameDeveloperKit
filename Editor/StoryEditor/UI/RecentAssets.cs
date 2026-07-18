using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GameDeveloperKit.StoryEditor.Model;

namespace GameDeveloperKit.StoryEditor.UI
{
    internal static class RecentAssets
    {
        private const string RecentKey = "StoryEditor.RecentAssets";
        private const int MaxCount = 10;

        public static IReadOnlyList<string> GetRecentPaths()
        {
            var json = EditorPrefs.GetString(RecentKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return System.Array.Empty<string>();
            }

            try
            {
                var paths = JsonUtility.FromJson<RecentList>(json);
                if (paths?.Items == null || paths.Items.Count == 0)
                {
                    return System.Array.Empty<string>();
                }

                var filtered = new List<string>(paths.Items.Count);
                for (var i = 0; i < paths.Items.Count; i++)
                {
                    var path = paths.Items[i];
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }

                    if (path.StartsWith("Assets/", System.StringComparison.Ordinal) is false)
                    {
                        continue;
                    }

                    filtered.Add(path);
                }

                return filtered;
            }
            catch
            {
                EditorPrefs.DeleteKey(RecentKey);
                return System.Array.Empty<string>();
            }
        }

        public static void RecordOpen(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            if (assetPath.StartsWith("Assets/", System.StringComparison.Ordinal) is false)
            {
                return;
            }

            var paths = new List<string>(GetRecentPaths());
            paths.RemoveAll(x => string.Equals(x, assetPath, System.StringComparison.Ordinal));
            paths.Insert(0, assetPath);

            while (paths.Count > MaxCount)
            {
                paths.RemoveAt(paths.Count - 1);
            }

            Save(paths);
        }

        public static bool IsValidAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            var asset = AssetDatabase.LoadAssetAtPath<AuthoringAsset>(assetPath);
            return asset != null;
        }

        private static void Save(List<string> paths)
        {
            var list = new RecentList { Items = paths };
            var json = JsonUtility.ToJson(list);
            EditorPrefs.SetString(RecentKey, json);
        }

        [System.Serializable]
        private sealed class RecentList
        {
            public List<string> Items;
        }
    }
}
