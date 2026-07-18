using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GameDeveloperKit.Resource;
using Newtonsoft.Json;
using AssetDatabase = UnityEditor.AssetDatabase;

namespace GameDeveloperKit.ResourceEditor
{
    internal static class ResourceAuthoringService
    {
        internal const string IssueSource = "ResourceAuthoring";

        public static ResourceAuthoringSnapshot BuildSnapshot()
        {
            if (ResourceEditorSettings.TryLoadExisting(out var settings) is false)
            {
                return CreateMissingSettingsSnapshot();
            }

            var registry = ResourceEditorRegistryCache.Current ?? ResourceEditorRegistryCache.Refresh();
            return BuildSnapshot(settings, registry);
        }

        internal static ResourceAuthoringSnapshot BuildSnapshot(
            ResourceEditorSettings settings,
            ResourceEditorRegistry registry)
        {
            return BuildSnapshot(settings, registry, null);
        }

        internal static ResourceAuthoringSnapshot Reconcile(
            ResourceEditorSettings settings,
            ResourceEditorRegistry registry,
            ResourceAssetChangeSet changes)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (changes == null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            return ReconcileCore(settings, registry, changes, out _);
        }

        internal static ResourceAuthoringSnapshot Reconcile(ResourceAssetChangeSet changes)
        {
            if (changes == null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            if (ResourceEditorSettings.TryLoadExisting(out var settings) is false)
            {
                return CreateMissingSettingsSnapshot();
            }

            var registry = ResourceEditorRegistryCache.Current ?? ResourceEditorRegistryCache.Refresh();
            var snapshot = ReconcileAndCommit(settings, registry, changes);
            UnityEngine.Debug.Log(
                $"[ResourceEditor] Reconciled resources. Imported={changes.ImportedAssets.Count}, " +
                $"Deleted={changes.DeletedAssets.Count}, Moved={changes.MovedAssets.Count}, " +
                $"Revision={snapshot.Revision}.");
            return snapshot;
        }

        internal static ResourceAuthoringSnapshot ReconcileAndCommit(
            ResourceEditorSettings settings,
            ResourceEditorRegistry registry,
            ResourceAssetChangeSet changes)
        {
            var snapshot = ReconcileCore(settings, registry, changes, out var mutationPlan);
            ResourceAuthoringSnapshotStore.Commit(
                snapshot,
                mutationPlan,
                settings.SaveSettings);
            return snapshot;
        }

        private static ResourceAuthoringSnapshot ReconcileCore(
            ResourceEditorSettings settings,
            ResourceEditorRegistry registry,
            ResourceAssetChangeSet changes,
            out ResourceAuthoringMutationPlan mutationPlan)
        {
            settings.EnsureDefaults();
            mutationPlan = ResourceAuthoringMutationPlan.Capture(settings);
            try
            {
                ResourceAuthoringReconciliation.Reconcile(settings, registry, changes);
                return BuildSnapshot(settings, registry, null);
            }
            catch
            {
                mutationPlan.Rollback();
                throw;
            }
        }

        private static ResourceAuthoringSnapshot BuildSnapshot(
            ResourceEditorSettings settings,
            ResourceEditorRegistry registry,
            IEnumerable<ResourceValidationIssue> initialIssues)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            var issues = initialIssues?.ToList() ?? new List<ResourceValidationIssue>();
            AddRegistryIssues(settings, registry, issues);
            var previews = ResourceAuthoringAssetValidator.ResolvePreviews(settings, issues);
            var manifest = ResourceManifestPreviewBuilder.Build(settings, previews);
            RunCheckers(settings, registry, previews, issues);
            ResourceDependencyOwnershipAnalyzer.Analyze(settings, previews, issues);
            return new ResourceAuthoringSnapshot(CalculateRevision(settings, manifest, issues), manifest, issues, previews);
        }

        internal static string FormatIssues(IEnumerable<ResourceValidationIssue> issues)
        {
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            return string.Join("; ", issues.Select(FormatIssue));
        }

        private static void AddRegistryIssues(
            ResourceEditorSettings settings,
            ResourceEditorRegistry registry,
            List<ResourceValidationIssue> issues)
        {
            foreach (var error in registry.Errors)
            {
                issues.Add(new ResourceValidationIssue(
                    ResourceValidationSeverity.Error,
                    "Registry",
                    error));
            }

            foreach (var package in settings.Packages ?? Enumerable.Empty<ResourceEditorPackage>())
            {
                if (package == null ||
                    string.IsNullOrWhiteSpace(package.BuildStrategyId) ||
                    registry.GetBuildStrategy(package.BuildStrategyId) != null)
                {
                    continue;
                }

                issues.Add(new ResourceValidationIssue(
                    ResourceValidationSeverity.Error,
                    "Registry",
                    $"Missing: {package.BuildStrategyId}",
                    package));
            }
        }

        private static void RunCheckers(
            ResourceEditorSettings settings,
            ResourceEditorRegistry registry,
            IReadOnlyDictionary<ResourceEditorBundle, List<ResourceGroupPreview>> previews,
            List<ResourceValidationIssue> issues)
        {
            foreach (var package in settings.Packages ?? Enumerable.Empty<ResourceEditorPackage>())
            {
                if (package?.Bundles == null)
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
        }

        private static ResourceAuthoringSnapshot CreateMissingSettingsSnapshot()
        {
            var manifest = new ManifestInfo
            {
                Version = "preview",
                BuildTime = 0,
                Packages = new List<PackageInfo>()
            };
            var issues = new List<ResourceValidationIssue>
            {
                new ResourceValidationIssue(
                    ResourceValidationSeverity.Error,
                    IssueSource,
                    "Resource editor settings is missing.")
            };
            return new ResourceAuthoringSnapshot(
                CalculateRevision(null, manifest, issues),
                manifest,
                issues,
                new Dictionary<ResourceEditorBundle, List<ResourceGroupPreview>>());
        }

        private static string CalculateRevision(
            ResourceEditorSettings settings,
            ManifestInfo manifest,
            IReadOnlyList<ResourceValidationIssue> issues)
        {
            var authoringProjection = (settings?.Packages ?? Enumerable.Empty<ResourceEditorPackage>())
                .Where(package => package != null)
                .SelectMany(package => (package.Bundles ?? new List<ResourceEditorBundle>())
                    .Where(bundle => bundle != null)
                    .SelectMany(bundle => (bundle.Entries ?? new List<ResourceEditorAssetEntry>())
                        .Where(entry => entry != null)
                        .Select(entry => new
                        {
                            Package = package.Name,
                            Bundle = bundle.Name,
                            entry.Guid,
                            StoredPath = entry.AssetPath,
                            ResolvedPath = string.IsNullOrWhiteSpace(entry.Guid)
                                ? entry.AssetPath
                                : AssetDatabase.GUIDToAssetPath(entry.Guid),
                            entry.Location,
                            entry.TypeName,
                            Labels = entry.Labels,
                            entry.ExcludeKind
                        })));
            var issueProjection = issues.Select(issue => new
            {
                issue.Severity,
                issue.Source,
                issue.Message,
                Package = issue.Package?.Name,
                Bundle = issue.Bundle?.Name,
                AssetPath = issue.Resource?.AssetPath,
                Location = issue.Resource?.Location
            });
            var payload = JsonConvert.SerializeObject(new
            {
                Authoring = authoringProjection,
                Manifest = manifest,
                Issues = issueProjection
            });

            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var value in hash)
                {
                    builder.Append(value.ToString("x2"));
                }

                return builder.ToString();
            }
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
