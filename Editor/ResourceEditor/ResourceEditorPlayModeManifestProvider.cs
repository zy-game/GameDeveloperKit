using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Resource;
using UnityEditor;

namespace GameDeveloperKit.ResourceEditor
{
    /// <summary>
    /// 定义 Resource Editor Play Mode Manifest Provider 类型。
    /// </summary>
    public static class ResourceEditorPlayModeManifestProvider
    {
        /// <summary>
        /// 构建 Editor Simulator Manifest。
        /// </summary>
        /// <returns>执行结果。</returns>
        public static ManifestInfo BuildEditorSimulatorManifest()
        {
            var settings = ResourceEditorSettings.LoadOrCreate();
            if (settings == null)
            {
                throw new GameException("Resource editor settings is missing.");
            }

            settings.EnsureDefaults();
            var registry = ResourceEditorRegistryCache.Current ?? ResourceEditorRegistryCache.Refresh();
            if (registry == null)
            {
                throw new GameException("Resource editor registry is missing.");
            }

            if (registry.Errors.Count > 0)
            {
                throw new GameException($"Resource editor registry has errors: {string.Join("; ", registry.Errors)}");
            }

            var previews = BuildPreviews(settings);
            var issues = CheckManifest(settings, registry, previews);
            var errors = issues.Where(x => x.Severity == ResourceValidationSeverity.Error).ToList();
            if (errors.Count > 0)
            {
                throw new GameException($"Resource editor manifest has errors: {string.Join("; ", errors.Select(FormatIssue))}");
            }

            WriteLocalBaseManifest(settings, previews);

            var manifest = ResourceManifestPreviewBuilder.Build(
                settings,
                previews,
                package => package != null && package.IsHotUpdate);
            if (manifest == null || manifest.Packages == null)
            {
                throw new GameException("Resource editor manifest is missing.");
            }

            return manifest;
        }

        private static void WriteLocalBaseManifest(ResourceEditorSettings settings, IReadOnlyDictionary<ResourceEditorBundle, List<ResourceGroupPreview>> previews)
        {
            var localManifest = ResourceManifestPreviewBuilder.Build(settings, previews, ResourceManifestPartitioner.IsLocalBasePackage);
            var localManifestPath = ResourceManifestPartitioner.ResolveLocalManifestPath(settings);
            ResourceManifestPartitioner.WriteManifest(localManifestPath, localManifest);

            var projectPath = ResourceBuildUtilities.ProjectRelativePath(localManifestPath);
            if (string.IsNullOrWhiteSpace(projectPath) is false)
            {
                AssetDatabase.ImportAsset(projectPath);
            }
        }

        /// <summary>
        /// 构建 Previews。
        /// </summary>
        /// <param name="settings">settings 参数。</param>
        /// <param name="registry">registry 参数。</param>
        /// <returns>执行结果。</returns>
        private static Dictionary<ResourceEditorBundle, List<ResourceGroupPreview>> BuildPreviews(ResourceEditorSettings settings)
        {
            var previews = new Dictionary<ResourceEditorBundle, List<ResourceGroupPreview>>();
            foreach (var package in settings.Packages)
            {
                if (package == null)
                {
                    continue;
                }

                foreach (var bundle in package.Bundles)
                {
                    if (bundle == null)
                    {
                        continue;
                    }

                    previews[bundle] = ResourceEditorEntryPreviewBuilder.Build(bundle);
                }
            }

            return previews;
        }

        /// <summary>
        /// 执行 Check Manifest。
        /// </summary>
        /// <param name="settings">settings 参数。</param>
        /// <param name="registry">registry 参数。</param>
        /// <param name="previews">previews 参数。</param>
        /// <returns>执行结果。</returns>
        private static List<ResourceValidationIssue> CheckManifest(ResourceEditorSettings settings, ResourceEditorRegistry registry, IReadOnlyDictionary<ResourceEditorBundle, List<ResourceGroupPreview>> previews)
        {
            var issues = new List<ResourceValidationIssue>();
            foreach (var package in settings.Packages)
            {
                if (package == null)
                {
                    continue;
                }

                foreach (var bundle in package.Bundles)
                {
                    if (bundle == null)
                    {
                        continue;
                    }

                    var resources = previews.TryGetValue(bundle, out var preview)
                        ? preview
                        : new List<ResourceGroupPreview>();
                    var context = new ResourceCheckContext(settings, package, bundle, resources, previews);
                    foreach (var checker in registry.Checkers)
                    {
                        checker.Instance.Check(context, issues);
                    }
                }
            }

            return issues;
        }

        /// <summary>
        /// 执行 Format Issue。
        /// </summary>
        /// <param name="issue">issue 参数。</param>
        /// <returns>执行结果。</returns>
        private static string FormatIssue(ResourceValidationIssue issue)
        {
            var package = issue.Package == null ? string.Empty : $" Package: {issue.Package.Name}.";
            var bundle = issue.Bundle == null ? string.Empty : $" Bundle: {issue.Bundle.Name}.";
            var resource = issue.Resource == null ? string.Empty : $" Resource: {issue.Resource.Location}.";
            return $"{issue.Source}: {issue.Message}.{package}{bundle}{resource}";
        }
    }
}
