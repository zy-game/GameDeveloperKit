using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.CodeAnalysis
{
    [InitializeOnLoad]
    internal sealed class AnalyzerDeploymentAssetPostprocessor : AssetPostprocessor
    {
        internal const string RoslynAnalyzerLabel = "RoslynAnalyzer";

        private static bool s_Scheduled;

        static AnalyzerDeploymentAssetPostprocessor()
        {
            ScheduleSynchronization();
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsRelevantPath(importedAssets) ||
                ContainsRelevantPath(deletedAssets) ||
                ContainsRelevantPath(movedAssets) ||
                ContainsRelevantPath(movedFromAssetPaths))
            {
                ScheduleSynchronization();
            }
        }

        internal static void SynchronizeNow()
        {
            var manifest = AnalyzerDeploymentManifestAsset.Load(out var packageRoot, out var manifestAssetPath);
            var canMutateMetadata = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(manifestAssetPath) == null;
            foreach (var component in manifest.Components)
            {
                var unityAssetPath = AnalyzerDeploymentManifestAsset.ResolveUnityAssetPath(packageRoot, component);
                SynchronizeComponent(unityAssetPath, canMutateMetadata);
            }

            foreach (var component in manifest.ExternalComponents)
            {
                var unityAssetPath = AnalyzerDeploymentManifestAsset.ResolveUnityAssetPath(packageRoot, component);
                SynchronizeComponent(unityAssetPath, canMutateMetadata);
            }
        }

        private static void SynchronizeComponent(string unityAssetPath, bool canMutateMetadata)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(unityAssetPath);
            if (asset == null)
            {
                throw new InvalidOperationException($"Deployed analyzer asset is missing: {unityAssetPath}");
            }

            var labels = AssetDatabase.GetLabels(asset);
            if (!labels.Contains(RoslynAnalyzerLabel, StringComparer.Ordinal))
            {
                if (canMutateMetadata is false)
                {
                    throw new InvalidOperationException($"Deployed analyzer asset is missing the {RoslynAnalyzerLabel} label: {unityAssetPath}");
                }

                AssetDatabase.SetLabels(
                    asset,
                    labels.Append(RoslynAnalyzerLabel).Distinct(StringComparer.Ordinal).OrderBy(static label => label, StringComparer.Ordinal).ToArray());
            }

            var importer = AssetImporter.GetAtPath(unityAssetPath) as PluginImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Analyzer asset does not use PluginImporter: {unityAssetPath}");
            }

            var incompatibleTargets = Enum.GetValues(typeof(BuildTarget))
                .Cast<BuildTarget>()
                .Where(buildTarget => buildTarget != BuildTarget.NoTarget && importer.GetCompatibleWithPlatform(buildTarget))
                .ToArray();
            var importerChanged = importer.GetCompatibleWithAnyPlatform() ||
                                  importer.GetCompatibleWithEditor() ||
                                  incompatibleTargets.Length > 0;
            if (importerChanged && canMutateMetadata is false)
            {
                throw new InvalidOperationException($"Deployed analyzer asset is enabled as a normal Plugin: {unityAssetPath}");
            }

            if (importer.GetCompatibleWithAnyPlatform())
            {
                importer.SetCompatibleWithAnyPlatform(false);
            }

            if (importer.GetCompatibleWithEditor())
            {
                importer.SetCompatibleWithEditor(false);
            }

            foreach (var buildTarget in incompatibleTargets)
            {
                importer.SetCompatibleWithPlatform(buildTarget, false);
            }

            if (importerChanged)
            {
                importer.SaveAndReimport();
            }
        }

        private static bool ContainsRelevantPath(string[] paths)
        {
            return AnalyzerDeploymentManifestAsset.ContainsRelevantPath(paths);
        }

        private static void ScheduleSynchronization()
        {
            if (s_Scheduled)
            {
                return;
            }

            s_Scheduled = true;
            EditorApplication.delayCall += Drain;
        }

        private static void Drain()
        {
            s_Scheduled = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ScheduleSynchronization();
                return;
            }

            try
            {
                SynchronizeNow();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
