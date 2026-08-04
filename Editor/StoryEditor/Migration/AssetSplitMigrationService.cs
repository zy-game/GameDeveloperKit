using System;
using System.Collections.Generic;
using System.IO;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Model;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Migration
{
    internal static class AssetSplitMigrationService
    {
        public static AssetSplitMigrationResult Analyze(AuthoringAsset project)
        {
            var result = new AssetSplitMigrationResult();
            if (project == null)
            {
                result.AddError("Story project is missing.");
                return result;
            }

            if (project.HasEmbeddedVolumes is false)
            {
                result.IsNoOp = project.VolumeAssets.Count > 0;
                if (result.IsNoOp is false)
                {
                    result.AddError("Story project does not contain embedded volumes or volume assets.");
                }

                return result;
            }

            var projectPath = AssetDatabase.GetAssetPath(project);
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                result.AddError("Story project must be saved before asset split migration.");
                return result;
            }

            var program = ProgramCompiler.Compile(project, out var report);
            if (program == null || report.HasErrors)
            {
                result.AddError(report.Issues.Count == 0
                    ? "Story project cannot be compiled before migration."
                    : report.Issues[0].Message);
                return result;
            }

            var directory = Path.GetDirectoryName(projectPath)?.Replace('\\', '/');
            var folder = $"{directory}/{Path.GetFileNameWithoutExtension(projectPath)}.Volumes";
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < project.EmbeddedVolumes.Count; i++)
            {
                var volume = project.EmbeddedVolumes[i];
                if (volume == null)
                {
                    result.AddError($"Embedded volume at index {i} is missing.");
                    continue;
                }

                var path = $"{folder}/{i + 1:00}_{SafeFileName(volume.VolumeId)}.asset";
                if (paths.Add(path) is false ||
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null ||
                    System.IO.File.Exists(Path.GetFullPath(path)))
                {
                    result.AddError($"Volume asset path is already occupied: {path}");
                    continue;
                }

                result.AddCandidate(volume, path);
            }

            return result;
        }

        public static AssetSplitMigrationResult Apply(AuthoringAsset project)
        {
            return Apply(project, null);
        }

        internal static AssetSplitMigrationResult Apply(
            AuthoringAsset project,
            Action<int> beforeCreateVolume)
        {
            var result = Analyze(project);
            if (result.HasErrors || result.IsNoOp)
            {
                return result;
            }

            var projectJson = EditorJsonUtility.ToJson(project);
            var createdPaths = new List<string>();
            var volumeFolder = Path.GetDirectoryName(result.Candidates[0].AssetPath)?.Replace('\\', '/');
            var volumeFolderExisted = AssetDatabase.IsValidFolder(volumeFolder);
            try
            {
                EnsureFolder(volumeFolder);
                var volumeAssets = new List<AuthoringVolumeAsset>();
                for (var i = 0; i < result.Candidates.Count; i++)
                {
                    beforeCreateVolume?.Invoke(i);
                    var candidate = result.Candidates[i];
                    var volumeAsset = ScriptableObject.CreateInstance<AuthoringVolumeAsset>();
                    var volume = new AuthoringVolume();
                    EditorUtility.CopySerializedManagedFieldsOnly(candidate.Volume, volume);
                    volumeAsset.SetVolume(volume);
                    AssetDatabase.CreateAsset(volumeAsset, candidate.AssetPath);
                    createdPaths.Add(candidate.AssetPath);
                    volumeAssets.Add(volumeAsset);
                }

                project.ReplaceVolumeAssets(volumeAssets);
                project.ClearEmbeddedVolumes();
                EditorUtility.SetDirty(project);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                result.CreatedAssetPaths.AddRange(createdPaths);
                return result;
            }
            catch (Exception exception)
            {
                EditorJsonUtility.FromJsonOverwrite(projectJson, project);
                EditorUtility.SetDirty(project);
                for (var i = 0; i < createdPaths.Count; i++)
                {
                    AssetDatabase.DeleteAsset(createdPaths[i]);
                }

                if (volumeFolderExisted is false && AssetDatabase.IsValidFolder(volumeFolder))
                {
                    AssetDatabase.DeleteAsset(volumeFolder);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                result.AddError(exception.Message);
                return result;
            }
        }

        private static string SafeFileName(string value)
        {
            var result = string.IsNullOrWhiteSpace(value) ? "volume" : value.Trim();
            var invalid = Path.GetInvalidFileNameChars();
            for (var i = 0; i < invalid.Length; i++)
            {
                result = result.Replace(invalid[i], '_');
            }

            return result;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }

    internal sealed class AssetSplitMigrationResult
    {
        private readonly List<string> m_Errors = new List<string>();
        private readonly List<AssetSplitMigrationCandidate> m_Candidates = new List<AssetSplitMigrationCandidate>();

        public IReadOnlyList<string> Errors => m_Errors;

        public IReadOnlyList<AssetSplitMigrationCandidate> Candidates => m_Candidates;

        public List<string> CreatedAssetPaths { get; } = new List<string>();

        public bool HasErrors => m_Errors.Count > 0;

        public bool IsNoOp { get; set; }

        public void AddError(string error)
        {
            m_Errors.Add(error);
        }

        public void AddCandidate(AuthoringVolume volume, string assetPath)
        {
            m_Candidates.Add(new AssetSplitMigrationCandidate(volume, assetPath));
        }
    }

    internal readonly struct AssetSplitMigrationCandidate
    {
        public AssetSplitMigrationCandidate(AuthoringVolume volume, string assetPath)
        {
            Volume = volume;
            AssetPath = assetPath;
        }

        public AuthoringVolume Volume { get; }

        public string AssetPath { get; }
    }
}
