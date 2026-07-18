using System;
using System.Linq;
using GameDeveloperKit.Resource;

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

            var snapshot = ResourceAuthoringService.ReconcileAndCommit(
                settings,
                registry,
                new ResourceAssetChangeSet(fullReconcile: true));
            return GetManifest(snapshot);
        }

        internal static ManifestInfo BuildEditorSimulatorManifest(
            ResourceEditorSettings settings,
            ResourceEditorRegistry registry)
        {
            var snapshot = ResourceAuthoringService.BuildSnapshot(settings, registry);
            return GetManifest(snapshot);
        }

        private static ManifestInfo GetManifest(ResourceAuthoringSnapshot snapshot)
        {
            var errors = snapshot.Issues.Where(x => x.Severity == ResourceValidationSeverity.Error).ToList();
            if (errors.Count > 0)
            {
                throw new GameException($"Resource editor manifest has errors: {ResourceAuthoringService.FormatIssues(errors)}");
            }

            if (snapshot.Manifest.Packages == null)
            {
                throw new GameException("Resource editor manifest is missing.");
            }

            return snapshot.Manifest;
        }

    }
}
