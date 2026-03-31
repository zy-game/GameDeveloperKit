using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.Runtime;
using UnityEditor;

namespace GameDeveloperKit.Editor
{
    internal enum ResourceValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    internal sealed class ResourceValidationIssue
    {
        public ResourceValidationSeverity Severity { get; set; }

        public string Scope { get; set; }

        public string Message { get; set; }
    }

    internal sealed class ResourceValidationReport
    {
        public List<ResourceValidationIssue> Issues { get; } = new();

        public int ErrorCount => Issues.Count(static issue => issue.Severity == ResourceValidationSeverity.Error);

        public int WarningCount => Issues.Count(static issue => issue.Severity == ResourceValidationSeverity.Warning);

        public int InfoCount => Issues.Count(static issue => issue.Severity == ResourceValidationSeverity.Info);

        public bool HasErrors => ErrorCount > 0;

        public string BuildSummary()
        {
            if (Issues.Count == 0)
            {
                return "Resource validation passed with no issues.";
            }

            var lines = new List<string>
            {
                $"Validation finished: {ErrorCount} error(s), {WarningCount} warning(s), {InfoCount} info item(s)."
            };

            for (var i = 0; i < Issues.Count; i++)
            {
                var issue = Issues[i];
                lines.Add($"[{issue.Severity}] {issue.Scope}: {issue.Message}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    internal enum ResourcePackageValidationStatus
    {
        Valid,
        Warning,
        Error
    }

    internal sealed class ResourcePackageValidationSummary
    {
        public ResourcePackageValidationStatus Status { get; set; }

        public string Message { get; set; }
    }

    internal static class ResourceValidationService
    {
        public static ResourceValidationReport Validate(ResourceProjectSettingsData settings)
        {
            var report = new ResourceValidationReport();
            if (settings == null)
            {
                report.Issues.Add(new ResourceValidationIssue
                {
                    Severity = ResourceValidationSeverity.Error,
                    Scope = "Settings",
                    Message = "Resource project settings are unavailable."
                });
                return report;
            }

            if (settings.Packages == null || settings.Packages.Count == 0)
            {
                report.Issues.Add(new ResourceValidationIssue
                {
                    Severity = ResourceValidationSeverity.Warning,
                    Scope = "Settings",
                    Message = "No resource packages configured."
                });
                return report;
            }

            var packageNames = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < settings.Packages.Count; i++)
            {
                var package = settings.Packages[i];
                ValidatePackage(report, package, i, packageNames);
            }

            return report;
        }

        public static ResourcePackageValidationSummary ValidatePackageSummary(ResourceProjectSettingsData settings, ResourcePackageDefinition package, int index)
        {
            var report = new ResourceValidationReport();
            var packageNames = new HashSet<string>(StringComparer.Ordinal);
            if (settings?.Packages != null)
            {
                for (var i = 0; i < settings.Packages.Count; i++)
                {
                    var current = settings.Packages[i];
                    if (current == null || string.IsNullOrWhiteSpace(current.PackageName))
                    {
                        continue;
                    }

                    if (ReferenceEquals(current, package))
                    {
                        break;
                    }

                    packageNames.Add(current.PackageName);
                }
            }

            ValidatePackage(report, package, index, packageNames);

            var status = report.HasErrors
                ? ResourcePackageValidationStatus.Error
                : report.WarningCount > 0
                    ? ResourcePackageValidationStatus.Warning
                    : ResourcePackageValidationStatus.Valid;

            return new ResourcePackageValidationSummary
            {
                Status = status,
                Message = report.Issues.Count == 0
                    ? "Validation passed."
                    : string.Join(Environment.NewLine, report.Issues.Select(static issue => $"[{issue.Severity}] {issue.Message}"))
            };
        }

        private static void ValidatePackage(
            ResourceValidationReport report,
            ResourcePackageDefinition package,
            int index,
            ISet<string> packageNames)
        {
            if (package == null)
            {
                report.Issues.Add(new ResourceValidationIssue
                {
                    Severity = ResourceValidationSeverity.Error,
                    Scope = $"Package[{index}]",
                    Message = "Package definition is null."
                });
                return;
            }

            ResourceCollectionService.NormalizePackage(package);

            var scope = $"Package[{index}]";
            if (string.IsNullOrWhiteSpace(package.PackageName))
            {
                report.Issues.Add(new ResourceValidationIssue
                {
                    Severity = ResourceValidationSeverity.Error,
                    Scope = scope,
                    Message = "Package name is empty."
                });
            }
            else if (!packageNames.Add(package.PackageName))
            {
                report.Issues.Add(new ResourceValidationIssue
                {
                    Severity = ResourceValidationSeverity.Error,
                    Scope = package.PackageName,
                    Message = "Package name is duplicated."
                });
            }
            else
            {
                scope = package.PackageName;
            }

            if (string.IsNullOrWhiteSpace(package.Version))
            {
                report.Issues.Add(new ResourceValidationIssue
                {
                    Severity = ResourceValidationSeverity.Warning,
                    Scope = scope,
                    Message = "Version is empty."
                });
            }

            switch (package.CollectionStrategy)
            {
                case ResourcePackageCollectionStrategy.ManualEntries:
                    if (package.Entries != null && package.Entries.Count > 0)
                    {
                        report.Issues.Add(new ResourceValidationIssue
                        {
                            Severity = ResourceValidationSeverity.Info,
                            Scope = scope,
                            Message = $"Collector is None and {package.Entries.Count} cached entries will be ignored."
                        });
                    }
                    return;
                case ResourcePackageCollectionStrategy.Directory:
                    ValidateCollectRoots(report, scope, package.CollectRoots);
                    break;
                case ResourcePackageCollectionStrategy.Label:
                    ValidateCollectRoots(report, scope, package.CollectRoots);
                    if (package.Labels == null || package.Labels.Count == 0)
                    {
                        report.Issues.Add(new ResourceValidationIssue
                        {
                            Severity = ResourceValidationSeverity.Warning,
                            Scope = scope,
                            Message = "Label collector requires at least one label."
                        });
                    }
                    break;
                case ResourcePackageCollectionStrategy.Type:
                    ValidateCollectRoots(report, scope, package.CollectRoots);
                    if (string.IsNullOrWhiteSpace(package.TypeName))
                    {
                        report.Issues.Add(new ResourceValidationIssue
                        {
                            Severity = ResourceValidationSeverity.Warning,
                            Scope = scope,
                            Message = "Type collector requires a type name."
                        });
                    }
                    break;
                case ResourcePackageCollectionStrategy.Dependency:
                    if (string.IsNullOrWhiteSpace(package.RootAssetPath))
                    {
                        report.Issues.Add(new ResourceValidationIssue
                        {
                            Severity = ResourceValidationSeverity.Warning,
                            Scope = scope,
                            Message = "Dependency collector requires a root asset path."
                        });
                    }
                    else if (AssetDatabase.LoadMainAssetAtPath(package.RootAssetPath) == null)
                    {
                        report.Issues.Add(new ResourceValidationIssue
                        {
                            Severity = ResourceValidationSeverity.Warning,
                            Scope = scope,
                            Message = $"Root asset path does not exist: {package.RootAssetPath}"
                        });
                    }
                    break;
                case ResourcePackageCollectionStrategy.Query:
                    ValidateCollectRoots(report, scope, package.CollectRoots);
                    if (string.IsNullOrWhiteSpace(package.Query))
                    {
                        report.Issues.Add(new ResourceValidationIssue
                        {
                            Severity = ResourceValidationSeverity.Warning,
                            Scope = scope,
                            Message = "Query collector requires an AssetDatabase query."
                        });
                    }
                    break;
            }

            var entries = ResourceCollectionService.BuildCollectedEntries(package);
            ValidateEntries(report, scope, entries);

            if (entries.Count == 0)
            {
                report.Issues.Add(new ResourceValidationIssue
                {
                    Severity = ResourceValidationSeverity.Warning,
                    Scope = scope,
                    Message = "Collector resolved zero entries."
                });
            }
            else
            {
                report.Issues.Add(new ResourceValidationIssue
                {
                    Severity = ResourceValidationSeverity.Info,
                    Scope = scope,
                    Message = $"Collector resolved {entries.Count} entries."
                });
            }
        }

        private static void ValidateCollectRoots(ResourceValidationReport report, string scope, IReadOnlyList<string> collectRoots)
        {
            if (collectRoots == null || collectRoots.Count == 0)
            {
                report.Issues.Add(new ResourceValidationIssue
                {
                    Severity = ResourceValidationSeverity.Info,
                    Scope = scope,
                    Message = "Using default Assets root."
                });
                return;
            }

            for (var i = 0; i < collectRoots.Count; i++)
            {
                var root = collectRoots[i];
                if (string.IsNullOrWhiteSpace(root))
                {
                    report.Issues.Add(new ResourceValidationIssue
                    {
                        Severity = ResourceValidationSeverity.Warning,
                        Scope = scope,
                        Message = $"Collect root at index {i} is empty."
                    });
                    continue;
                }

                var absolutePath = ResourceCollectionService.ToAbsolutePath(root);
                if (string.IsNullOrWhiteSpace(absolutePath) || !Directory.Exists(absolutePath))
                {
                    report.Issues.Add(new ResourceValidationIssue
                    {
                        Severity = ResourceValidationSeverity.Warning,
                        Scope = scope,
                        Message = $"Collect root does not exist: {root}"
                    });
                }
            }
        }

        private static void ValidateEntries(ResourceValidationReport report, string scope, IReadOnlyList<ResourceEntry> entries)
        {
            var nameLookup = new HashSet<string>(StringComparer.Ordinal);
            var pathLookup = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.Name) && string.IsNullOrWhiteSpace(entry.FullPath))
                {
                    report.Issues.Add(new ResourceValidationIssue
                    {
                        Severity = ResourceValidationSeverity.Error,
                        Scope = scope,
                        Message = $"Entry at index {i} is missing both Name and FullPath."
                    });
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(entry.Name) && !nameLookup.Add(entry.Name))
                {
                    report.Issues.Add(new ResourceValidationIssue
                    {
                        Severity = ResourceValidationSeverity.Error,
                        Scope = scope,
                        Message = $"Duplicate entry name: {entry.Name}"
                    });
                }

                if (!string.IsNullOrWhiteSpace(entry.FullPath) && !pathLookup.Add(entry.FullPath))
                {
                    report.Issues.Add(new ResourceValidationIssue
                    {
                        Severity = ResourceValidationSeverity.Error,
                        Scope = scope,
                        Message = $"Duplicate entry path: {entry.FullPath}"
                    });
                }
            }
        }
    }
}
