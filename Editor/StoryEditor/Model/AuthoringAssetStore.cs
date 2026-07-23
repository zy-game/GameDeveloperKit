using System.IO;
using UnityEditor;
using UnityEngine;
using GameDeveloperKit.Story.Publishing;

namespace GameDeveloperKit.StoryEditor.Model
{
    /// <summary>
    /// Story authoring asset 存取。
    /// </summary>
    internal static class AuthoringAssetStore
    {
        public static AuthoringAsset CreateProjectAtPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            var directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var fileName = Path.GetFileNameWithoutExtension(assetPath);
            var volumeFolder = $"{directory}/{fileName}.Volumes";
            var volumePath = $"{volumeFolder}/Volume01.asset";
            if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null ||
                AssetDatabase.LoadAssetAtPath<Object>(volumePath) != null)
            {
                return null;
            }

            EnsureFolder(directory);
            var volumeFolderExisted = AssetDatabase.IsValidFolder(volumeFolder);
            EnsureFolder(volumeFolder);
            var project = ScriptableObject.CreateInstance<AuthoringAsset>();
            var volume = AuthoringVolumeAsset.CreateDefault(IdentityId.New(), "第一卷");
            var createdProject = false;
            var createdVolume = false;
            try
            {
                AssetDatabase.CreateAsset(volume, volumePath);
                createdVolume = true;
                project.ReplaceVolumeAssets(new[] { volume });
                AssetDatabase.CreateAsset(project, assetPath);
                createdProject = true;
                EditorUtility.SetDirty(project);
                EditorUtility.SetDirty(volume);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return project;
            }
            catch
            {
                if (createdProject)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                if (createdVolume)
                {
                    AssetDatabase.DeleteAsset(volumePath);
                }

                if (volumeFolderExisted is false && AssetDatabase.IsValidFolder(volumeFolder))
                {
                    AssetDatabase.DeleteAsset(volumeFolder);
                }

                AssetDatabase.Refresh();
                throw;
            }
        }

        public static void Save(AuthoringAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void Save(AuthoringVolumeAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var name = Path.GetFileName(folder);
            EnsureFolder(parent);
            if (string.IsNullOrWhiteSpace(parent) is false && string.IsNullOrWhiteSpace(name) is false && AssetDatabase.IsValidFolder(folder) is false)
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

    }
}
