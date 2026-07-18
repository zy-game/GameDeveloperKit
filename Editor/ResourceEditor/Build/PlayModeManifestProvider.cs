using System;
using System.Linq;
using GameDeveloperKit.Resource;

namespace GameDeveloperKit.ResourceEditor.Build
{
    /// <summary>
    /// 定义 Resource Editor Play Mode Manifest Provider 类型。
    /// </summary>
    public static class PlayModeManifestProvider
    {
        /// <summary>
        /// 构建 Editor Simulator Manifest。
        /// </summary>
        /// <returns>执行结果。</returns>
        public static ManifestInfo BuildEditorSimulatorManifest()
        {
            var settings = GameDeveloperKit.ResourceEditor.Authoring.Settings.LoadOrCreate();
            if (settings == null)
            {
                throw new GameException("Resource editor settings is missing.");
            }

            settings.EnsureDefaults();
            var registry = GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistryCache.Current ?? GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistryCache.Refresh();
            if (registry == null)
            {
                throw new GameException("Resource editor registry is missing.");
            }

            var snapshot = GameDeveloperKit.ResourceEditor.Authoring.Service.ReconcileAndCommit(
                settings,
                registry,
                new GameDeveloperKit.ResourceEditor.Authoring.AssetChangeSet(fullReconcile: true));
            return GetManifest(snapshot);
        }

        internal static ManifestInfo BuildEditorSimulatorManifest(
            GameDeveloperKit.ResourceEditor.Authoring.Settings settings,
            GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry registry)
        {
            var snapshot = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, registry);
            return GetManifest(snapshot);
        }

        private static ManifestInfo GetManifest(GameDeveloperKit.ResourceEditor.Authoring.Snapshot snapshot)
        {
            var errors = snapshot.Issues.Where(x => x.Severity == GameDeveloperKit.ResourceEditor.Validation.Severity.Error).ToList();
            if (errors.Count > 0)
            {
                throw new GameException($"Resource editor manifest has errors: {GameDeveloperKit.ResourceEditor.Authoring.Service.FormatIssues(errors)}");
            }

            if (snapshot.Manifest.Packages == null)
            {
                throw new GameException("Resource editor manifest is missing.");
            }

            return snapshot.Manifest;
        }

    }
}
