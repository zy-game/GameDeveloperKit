using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Resource;

namespace GameDeveloperKit.ResourceEditor
{
    public static class ResourceEditorPlayModeManifestProvider
    {
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

            var previews = BuildPreviews(settings, registry);
            var issues = CheckManifest(settings, registry, previews);
            var errors = issues.Where(x => x.Severity == ResourceValidationSeverity.Error).ToList();
            if (errors.Count > 0)
            {
                throw new GameException($"Resource editor manifest has errors: {string.Join("; ", errors.Select(FormatIssue))}");
            }

            var manifest = ResourceManifestPreviewBuilder.Build(settings, previews);
            if (manifest == null || manifest.Packages == null || manifest.Packages.Count == 0)
            {
                throw new GameException("Resource editor manifest is empty.");
            }

            return manifest;
        }

        private static Dictionary<ResourceEditorBundle, List<ResourceGroupPreview>> BuildPreviews(ResourceEditorSettings settings, ResourceEditorRegistry registry)
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

                    var collector = registry.GetCollector(bundle.CollectorId) ?? registry.GetCollector(package.CollectorId);
                    if (collector == null)
                    {
                        throw new GameException($"Resource collector is missing. Package: {package.Name}, Bundle: {bundle.Name}");
                    }

                    try
                    {
                        previews[bundle] = collector.Instance.Collect(package, bundle)?.ToList() ?? new List<ResourceGroupPreview>();
                    }
                    catch (Exception exception)
                    {
                        throw new GameException($"Resource collector failed. Package: {package.Name}, Bundle: {bundle.Name}", exception);
                    }
                }
            }

            return previews;
        }

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

        private static string FormatIssue(ResourceValidationIssue issue)
        {
            var package = issue.Package == null ? string.Empty : $" Package: {issue.Package.Name}.";
            var bundle = issue.Bundle == null ? string.Empty : $" Bundle: {issue.Bundle.Name}.";
            var resource = issue.Resource == null ? string.Empty : $" Resource: {issue.Resource.Location}.";
            return $"{issue.Source}: {issue.Message}.{package}{bundle}{resource}";
        }
    }
}
