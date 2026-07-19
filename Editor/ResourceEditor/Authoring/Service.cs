using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GameDeveloperKit.Resource;
using Newtonsoft.Json;
using AssetDatabase = UnityEditor.AssetDatabase;

namespace GameDeveloperKit.ResourceEditor.Authoring
{
    internal static class Service
    {
        internal const string IssueSource = "ResourceAuthoring";

        public static Snapshot BuildSnapshot()
        {
            if (Settings.TryLoadExisting(out var settings) is false)
            {
                return CreateMissingSettingsSnapshot();
            }

            var registry = GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistryCache.Current ?? GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistryCache.Refresh();
            return BuildSnapshot(settings, registry);
        }

        internal static Snapshot BuildSnapshot(
            Settings settings,
            GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry registry)
        {
            return BuildSnapshot(settings, registry, null);
        }

        internal static Snapshot Reconcile(
            Settings settings,
            GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry registry,
            AssetChangeSet changes)
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

        internal static Snapshot Reconcile(AssetChangeSet changes)
        {
            if (changes == null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            if (Settings.TryLoadExisting(out var settings) is false)
            {
                return CreateMissingSettingsSnapshot();
            }

            var registry = GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistryCache.Current ?? GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistryCache.Refresh();
            var snapshot = ReconcileAndCommit(settings, registry, changes);
            UnityEngine.Debug.Log(
                $"[ResourceEditor] Reconciled resources. Imported={changes.ImportedAssets.Count}, " +
                $"Deleted={changes.DeletedAssets.Count}, Moved={changes.MovedAssets.Count}, " +
                $"Revision={snapshot.Revision}.");
            return snapshot;
        }

        internal static Snapshot ReconcileAndCommit(
            Settings settings,
            GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry registry,
            AssetChangeSet changes)
        {
            var snapshot = ReconcileCore(settings, registry, changes, out var mutationPlan);
            SnapshotStore.Commit(
                snapshot,
                mutationPlan,
                settings.SaveSettings);
            return snapshot;
        }

        internal static Snapshot MutateAndCommit(
            Settings settings,
            GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry registry,
            Action mutation)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (mutation == null)
            {
                throw new ArgumentNullException(nameof(mutation));
            }

            settings.EnsureDefaults();
            var mutationPlan = MutationPlan.Capture(settings);
            try
            {
                mutation();
                settings.EnsureDefaults();
                var issues = new List<GameDeveloperKit.ResourceEditor.Validation.Issue>();
                Reconciliation.Reconcile(settings, registry, new AssetChangeSet(fullReconcile: true), issues);
                var snapshot = BuildSnapshot(settings, registry, issues);
                SnapshotStore.Commit(snapshot, mutationPlan, settings.SaveSettings);
                return snapshot;
            }
            catch
            {
                mutationPlan.Rollback();
                throw;
            }
        }

        private static Snapshot ReconcileCore(
            Settings settings,
            GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry registry,
            AssetChangeSet changes,
            out MutationPlan mutationPlan)
        {
            settings.EnsureDefaults();
            mutationPlan = MutationPlan.Capture(settings);
            try
            {
                var issues = new List<GameDeveloperKit.ResourceEditor.Validation.Issue>();
                Reconciliation.Reconcile(settings, registry, changes, issues);
                return BuildSnapshot(settings, registry, issues);
            }
            catch
            {
                mutationPlan.Rollback();
                throw;
            }
        }

        private static Snapshot BuildSnapshot(
            Settings settings,
            GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry registry,
            IEnumerable<GameDeveloperKit.ResourceEditor.Validation.Issue> initialIssues)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            var issues = initialIssues?.ToList() ?? new List<GameDeveloperKit.ResourceEditor.Validation.Issue>();
            AddRegistryIssues(settings, registry, issues);
            var previews = AssetValidator.ResolvePreviews(settings, registry, issues);
            var manifest = GameDeveloperKit.ResourceEditor.Build.ManifestPreviewBuilder.Build(settings, previews);
            RunCheckers(settings, registry, previews, issues);
            DependencyOwnershipAnalyzer.Analyze(settings, previews, issues);
            return new Snapshot(CalculateRevision(settings, manifest, issues), manifest, issues, previews);
        }

        internal static string FormatIssues(IEnumerable<GameDeveloperKit.ResourceEditor.Validation.Issue> issues)
        {
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            return string.Join("; ", issues.Select(FormatIssue));
        }

        private static void AddRegistryIssues(
            Settings settings,
            GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry registry,
            List<GameDeveloperKit.ResourceEditor.Validation.Issue> issues)
        {
            foreach (var error in registry.Errors)
            {
                issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(
                    GameDeveloperKit.ResourceEditor.Validation.Severity.Error,
                    "Registry",
                    error));
            }

            foreach (var package in settings.Packages ?? Enumerable.Empty<Package>())
            {
                if (package == null)
                {
                    continue;
                }

                foreach (var bundle in package.Bundles.Where(bundle => bundle != null))
                {
                    if (registry.GetCollector(bundle.CollectorId) == null)
                    {
                        issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(
                            GameDeveloperKit.ResourceEditor.Validation.Severity.Error,
                            "Registry",
                            $"Missing collector: {bundle.CollectorId}",
                            package,
                            bundle));
                    }

                    if (registry.GetFilterRule(bundle.FilterRuleId) == null)
                    {
                        issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(
                            GameDeveloperKit.ResourceEditor.Validation.Severity.Error,
                            "Registry",
                            $"Missing filter rule: {bundle.FilterRuleId}",
                            package,
                            bundle));
                    }

                    if (registry.GetPackRule(bundle.PackRuleId) == null)
                    {
                        issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(
                            GameDeveloperKit.ResourceEditor.Validation.Severity.Error,
                            "Registry",
                            $"Missing pack rule: {bundle.PackRuleId}",
                            package,
                            bundle));
                    }
                }
            }
        }

        internal static void AddFilterRuleError(
            ICollection<GameDeveloperKit.ResourceEditor.Validation.Issue> issues,
            Package package,
            Bundle bundle,
            string ruleId,
            Exception exception)
        {
            issues.Add(new GameDeveloperKit.ResourceEditor.Validation.Issue(
                GameDeveloperKit.ResourceEditor.Validation.Severity.Error,
                "FilterRule",
                $"Filter rule '{ruleId}' failed: {exception.Message}",
                package,
                bundle));
        }

        private static void RunCheckers(
            Settings settings,
            GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry registry,
            IReadOnlyDictionary<Bundle, List<ResourceGroupPreview>> previews,
            List<GameDeveloperKit.ResourceEditor.Validation.Issue> issues)
        {
            foreach (var package in settings.Packages ?? Enumerable.Empty<Package>())
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
                    var context = new GameDeveloperKit.ResourceEditor.Validation.CheckContext(settings, package, bundle, resources, previews);
                    foreach (var checker in registry.Checkers)
                    {
                        checker.Instance.Check(context, issues);
                    }
                }
            }
        }

        private static Snapshot CreateMissingSettingsSnapshot()
        {
            var manifest = new ManifestInfo
            {
                Version = "preview",
                BuildTime = 0,
                Packages = new List<PackageInfo>()
            };
            var issues = new List<GameDeveloperKit.ResourceEditor.Validation.Issue>
            {
                new GameDeveloperKit.ResourceEditor.Validation.Issue(
                    GameDeveloperKit.ResourceEditor.Validation.Severity.Error,
                    IssueSource,
                    "Resource editor settings is missing.")
            };
            return new Snapshot(
                CalculateRevision(null, manifest, issues),
                manifest,
                issues,
                new Dictionary<Bundle, List<ResourceGroupPreview>>());
        }

        private static string CalculateRevision(
            Settings settings,
            ManifestInfo manifest,
            IReadOnlyList<GameDeveloperKit.ResourceEditor.Validation.Issue> issues)
        {
            var authoringProjection = (settings?.Packages ?? Enumerable.Empty<Package>())
                .Where(package => package != null)
                .Select(package => new
                {
                    Package = package.Name,
                    Bundles = (package.Bundles ?? new List<Bundle>())
                    .Where(bundle => bundle != null)
                    .Select(bundle => new
                    {
                        Bundle = bundle.Name,
                        bundle.Group,
                        bundle.CollectorId,
                        bundle.FilterRuleId,
                        bundle.PackRuleId,
                        bundle.SourceFolder,
                        Entries = (bundle.Entries ?? new List<AssetEntry>())
                            .Where(entry => entry != null)
                            .Select(entry => new
                        {
                            entry.Guid,
                            StoredPath = entry.AssetPath,
                            ResolvedPath = string.IsNullOrWhiteSpace(entry.Guid)
                                ? entry.AssetPath
                                : AssetDatabase.GUIDToAssetPath(entry.Guid),
                            entry.Location,
                            entry.TypeName,
                            Labels = entry.Labels,
                            entry.ExcludeKind
                        })
                    })
                });
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

        private static string FormatIssue(GameDeveloperKit.ResourceEditor.Validation.Issue issue)
        {
            var package = issue.Package == null ? string.Empty : $" Package: {issue.Package.Name}.";
            var bundle = issue.Bundle == null ? string.Empty : $" Bundle: {issue.Bundle.Name}.";
            var resource = issue.Resource == null ? string.Empty : $" Resource: {issue.Resource.Location}.";
            return $"{issue.Source}: {issue.Message}.{package}{bundle}{resource}";
        }
    }
}
