using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace GameDeveloperKit.ResourceEditor.Build
{
    /// <summary>
    /// Provides the resource build entry point used by unattended player builds.
    /// </summary>
    public static class Automation
    {
        private static readonly Regex s_GeneratedBundleName = new Regex(
            "^[0-9a-f]{40}\\.bundle$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>
        /// Builds every resource package for the active player target and removes stale packaged bundles.
        /// </summary>
        public static Result BuildAllForActiveTarget(out int removedStaleBundleCount)
        {
            removedStaleBundleCount = 0;
            var target = EditorUserBuildSettings.activeBuildTarget;
            var settings = GameDeveloperKit.ResourceEditor.Authoring.Settings.LoadOrCreate();
            var registry = GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistryCache.Current ??
                           GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistryCache.Refresh();
            var source = settings.BuildSettings;
            var buildSettings = new Settings
            {
                OutputRoot = source.OutputRoot,
                Target = target.ToString(),
                Channel = source.Channel,
                CleanOutput = true,
                Compression = source.Compression,
                ManifestFileName = source.ManifestFileName,
                ManifestVersion = source.ManifestVersion,
                Scope = Scope.AllPackages
            };

            var result = new Workflow(settings, registry, buildSettings).Build(out _);
            if (result == null || result.Succeeded is false)
            {
                return result ?? Result.Failure("Resource build returned a null result.");
            }

            try
            {
                removedStaleBundleCount = RemoveStalePackagedBundles(result);
                ValidatePackagedManifest(settings);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                return result;
            }
            catch (Exception exception)
            {
                result.Succeeded = false;
                result.ErrorMessage = $"Resource package validation failed: {exception.Message}";
                return result;
            }
        }

        private static int RemoveStalePackagedBundles(Result result)
        {
            var streamingAssetsRoot = Path.GetFullPath("Assets/StreamingAssets");
            var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var artifact in result.Artifacts)
            {
                if (artifact == null || string.IsNullOrWhiteSpace(artifact.LocalPath))
                {
                    continue;
                }

                var artifactPath = Path.GetFullPath(artifact.LocalPath);
                if (string.Equals(Path.GetDirectoryName(artifactPath), streamingAssetsRoot,
                        StringComparison.OrdinalIgnoreCase) &&
                    artifactPath.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
                {
                    expected.Add(artifactPath);
                }
            }

            if (expected.Count == 0)
            {
                throw new InvalidDataException("The resource build produced no packaged AssetBundles.");
            }

            var removed = 0;
            foreach (var path in Directory.GetFiles(streamingAssetsRoot, "*.bundle", SearchOption.TopDirectoryOnly))
            {
                if (s_GeneratedBundleName.IsMatch(Path.GetFileName(path)) is false || expected.Contains(Path.GetFullPath(path)))
                {
                    continue;
                }

                var assetPath = path.Replace('\\', '/');
                var projectRoot = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/') + "/";
                if (assetPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    assetPath = assetPath.Substring(projectRoot.Length);
                }

                if (AssetDatabase.DeleteAsset(assetPath) is false)
                {
                    throw new IOException($"Failed to remove stale packaged bundle: {assetPath}");
                }

                removed++;
            }

            return removed;
        }

        private static void ValidatePackagedManifest(
            GameDeveloperKit.ResourceEditor.Authoring.Settings settings)
        {
            var manifestPath = Path.GetFullPath(settings.ManifestOutputPath);
            if (System.IO.File.Exists(manifestPath) is false)
            {
                throw new FileNotFoundException("The packaged resource manifest is missing.", manifestPath);
            }

            if (new FileInfo(manifestPath).Length == 0)
            {
                throw new InvalidDataException("The packaged resource manifest is empty.");
            }
        }
    }
}
