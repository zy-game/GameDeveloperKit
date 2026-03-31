using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor
{
    internal interface IResourceEntryCollector
    {
        ResourcePackageCollectionStrategy Strategy { get; }

        void Collect(ResourcePackageDefinition package, ICollection<ResourceEntry> results);
    }

    internal static class ResourceCollectionService
    {
        private static readonly ResourcePackageCollectionStrategy[] SupportedStrategies =
        {
            ResourcePackageCollectionStrategy.ManualEntries,
            ResourcePackageCollectionStrategy.Directory,
            ResourcePackageCollectionStrategy.Label,
            ResourcePackageCollectionStrategy.Type,
            ResourcePackageCollectionStrategy.Dependency,
            ResourcePackageCollectionStrategy.Query
        };

        private static readonly IReadOnlyDictionary<ResourcePackageCollectionStrategy, IResourceEntryCollector> Collectors =
            new Dictionary<ResourcePackageCollectionStrategy, IResourceEntryCollector>
            {
                { ResourcePackageCollectionStrategy.Directory, new DirectoryCollector() },
                { ResourcePackageCollectionStrategy.Label, new LabelCollector() },
                { ResourcePackageCollectionStrategy.Type, new TypeCollector() },
                { ResourcePackageCollectionStrategy.Dependency, new DependencyCollector() },
                { ResourcePackageCollectionStrategy.Query, new QueryCollector() }
            };

        public static IReadOnlyList<ResourcePackageCollectionStrategy> GetSupportedStrategies()
        {
            return SupportedStrategies;
        }

        public static ResourcePackageCollectionStrategy NormalizeCollectionStrategy(ResourcePackageCollectionStrategy strategy)
        {
            return strategy;
        }

        public static bool NormalizePackage(ResourcePackageDefinition package)
        {
            if (package == null)
            {
                return false;
            }

            var changed = false;
            var normalizedStrategy = NormalizeCollectionStrategy(package.CollectionStrategy);
            if (package.CollectionStrategy != normalizedStrategy)
            {
                package.CollectionStrategy = normalizedStrategy;
                changed = true;
            }

            package.CollectionStrategy = NormalizeCollectionStrategy(package.CollectionStrategy);
            if (package.CollectRoots == null)
            {
                package.CollectRoots = new List<string>();
                changed = true;
            }

            if (RequiresCollectRoots(package.CollectionStrategy) && package.CollectRoots.Count == 0)
            {
                package.CollectRoots.Add("Assets");
                changed = true;
            }

            if (package.SearchExtensions == null)
            {
                package.SearchExtensions = new List<string>();
                changed = true;
            }

            if (package.Labels == null)
            {
                package.Labels = new List<string>();
                changed = true;
            }

            if (package.ExcludePatterns == null)
            {
                package.ExcludePatterns = new List<string>();
                changed = true;
            }

            if (package.Entries == null)
            {
                package.Entries = new List<ResourceEntry>();
                changed = true;
            }

            if (package.SimulateSearchRoots == null)
            {
                package.SimulateSearchRoots = new List<string>();
                changed = true;
            }

            SanitizeExclusiveExtensions(package);
            return changed;
        }

        public static bool ValidateSinglePackage(ResourcePackageDefinition package)
        {
            if (package == null || string.IsNullOrWhiteSpace(package.PackageName) || string.IsNullOrWhiteSpace(package.Version))
            {
                return false;
            }

            return SupportedStrategies.Contains(NormalizeCollectionStrategy(package.CollectionStrategy));
        }

        public static (string Message, HelpBoxMessageType MessageType) ValidateSettings(ResourceProjectSettingsData settings)
        {
            if (settings == null)
            {
                return ("Resource project settings are unavailable.", HelpBoxMessageType.Error);
            }

            if (settings.Packages == null || settings.Packages.Count == 0)
            {
                return ("No resource packages configured.", HelpBoxMessageType.Warning);
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            var warnings = new List<string>();

            for (var i = 0; i < settings.Packages.Count; i++)
            {
                var package = settings.Packages[i];
                NormalizePackage(package);

                if (package == null)
                {
                    return ($"Package at index {i} is null.", HelpBoxMessageType.Error);
                }

                if (string.IsNullOrWhiteSpace(package.PackageName))
                {
                    return ($"Package at index {i} has an empty name.", HelpBoxMessageType.Error);
                }

                if (!names.Add(package.PackageName))
                {
                    return ($"Package '{package.PackageName}' is duplicated.", HelpBoxMessageType.Error);
                }

                if (string.IsNullOrWhiteSpace(package.Version))
                {
                    warnings.Add($"Package '{package.PackageName}' has no version.");
                }

                switch (package.CollectionStrategy)
                {
                    case ResourcePackageCollectionStrategy.Directory:
                        if (package.CollectRoots == null || package.CollectRoots.Count == 0)
                        {
                            warnings.Add($"Package '{package.PackageName}' is using the default Assets root.");
                        }
                        break;
                    case ResourcePackageCollectionStrategy.Label:
                        if (package.Labels == null || package.Labels.Count == 0)
                        {
                            warnings.Add($"Package '{package.PackageName}' requires at least one label.");
                        }
                        break;
                    case ResourcePackageCollectionStrategy.Type:
                        if (string.IsNullOrWhiteSpace(package.TypeName))
                        {
                            warnings.Add($"Package '{package.PackageName}' requires a type name.");
                        }
                        break;
                    case ResourcePackageCollectionStrategy.Dependency:
                        if (string.IsNullOrWhiteSpace(package.RootAssetPath))
                        {
                            warnings.Add($"Package '{package.PackageName}' requires a root asset path.");
                        }
                        break;
                    case ResourcePackageCollectionStrategy.Query:
                        if (string.IsNullOrWhiteSpace(package.Query))
                        {
                            warnings.Add($"Package '{package.PackageName}' requires an AssetDatabase query.");
                        }
                        break;
                }
            }

            return warnings.Count > 0
                ? (string.Join(" ", warnings.Take(3)), HelpBoxMessageType.Warning)
                : ("Resource settings validation passed.", HelpBoxMessageType.Info);
        }

        public static List<ResourceEntry> BuildCollectedEntries(ResourcePackageDefinition package)
        {
            var results = new List<ResourceEntry>();
            if (package == null)
            {
                return results;
            }

            NormalizePackage(package);
            if (Collectors.TryGetValue(package.CollectionStrategy, out var collector))
            {
                collector.Collect(package, results);
            }

            return results;
        }

        public static List<string> CollectAvailableExtensions(ResourcePackageDefinition package)
        {
            NormalizePackage(package);

            var extensions = new HashSet<string>(StringComparer.Ordinal);
            var roots = package.CollectRoots.Count == 0 ? new[] { "Assets" } : package.CollectRoots.ToArray();

            for (var i = 0; i < roots.Length; i++)
            {
                var absoluteRoot = ToAbsolutePath(roots[i]);
                if (string.IsNullOrWhiteSpace(absoluteRoot) || !Directory.Exists(absoluteRoot))
                {
                    continue;
                }

                try
                {
                    var files = Directory.GetFiles(
                        absoluteRoot,
                        "*",
                        package.IncludeSubDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

                    for (var j = 0; j < files.Length; j++)
                    {
                        var assetPath = ToAssetPath(files[j]);
                        if (string.IsNullOrWhiteSpace(assetPath) || AssetDatabase.IsValidFolder(assetPath))
                        {
                            continue;
                        }

                        var ext = Path.GetExtension(assetPath).ToLowerInvariant();
                        if (!string.IsNullOrWhiteSpace(ext))
                        {
                            extensions.Add(ext);
                        }
                    }
                }
                catch (DirectoryNotFoundException)
                {
                }
            }

            return extensions.OrderBy(static item => item, StringComparer.Ordinal).ToList();
        }

        public static void SanitizeExclusiveExtensions(ResourcePackageDefinition package)
        {
            if (package == null)
            {
                return;
            }

            package.SearchExtensions ??= new List<string>();
            package.ExcludePatterns ??= new List<string>();

            package.SearchExtensions = package.SearchExtensions
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Select(static item => item.Trim().ToLowerInvariant())
                .Distinct()
                .OrderBy(static item => item, StringComparer.Ordinal)
                .ToList();

            package.ExcludePatterns = package.ExcludePatterns
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Select(static item => item.Trim().ToLowerInvariant())
                .Distinct()
                .OrderBy(static item => item, StringComparer.Ordinal)
                .ToList();

            var excludeSet = new HashSet<string>(package.ExcludePatterns, StringComparer.Ordinal);
            package.SearchExtensions = package.SearchExtensions
                .Where(item => !excludeSet.Contains(item))
                .ToList();
        }

        public static bool IsExcluded(string assetPath, IReadOnlyList<string> excludePatterns)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || excludePatterns == null || excludePatterns.Count == 0)
            {
                return false;
            }

            var fileName = Path.GetFileName(assetPath);
            for (var i = 0; i < excludePatterns.Count; i++)
            {
                var pattern = excludePatterns[i];
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;
                }

                if (pattern.StartsWith("*.", StringComparison.Ordinal) &&
                    fileName.EndsWith(pattern.Substring(1), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (assetPath.StartsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RequiresCollectRoots(ResourcePackageCollectionStrategy strategy)
        {
            return strategy == ResourcePackageCollectionStrategy.Directory ||
                   strategy == ResourcePackageCollectionStrategy.Label ||
                   strategy == ResourcePackageCollectionStrategy.Type ||
                   strategy == ResourcePackageCollectionStrategy.Query;
        }

        public static string GetCollectionStrategyDisplayName(ResourcePackageCollectionStrategy strategy)
        {
            return strategy == ResourcePackageCollectionStrategy.ManualEntries ? "None" : strategy.ToString();
        }

        public static string ToAbsolutePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (Path.IsPathRooted(path))
            {
                return path;
            }

            if (path.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            }

            return Path.GetFullPath(path);
        }

        public static string ToAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var normalized = path.Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/');
            if (!normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return "Assets" + normalized.Substring(dataPath.Length);
        }

        private static void AddUniqueEntry(ICollection<ResourceEntry> results, ResourceEntry entry)
        {
            foreach (var existing in results)
            {
                if (existing != null &&
                    string.Equals(existing.FullPath, entry.FullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            results.Add(entry);
        }

        private static ResourceEntry CreateEntryFromAssetPath(string assetPath, string version)
        {
            var mainType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var labels = asset == null ? Array.Empty<string>() : AssetDatabase.GetLabels(asset);
            long sizeBytes = 0;
            var absolutePath = ToAbsolutePath(assetPath);
            if (!string.IsNullOrWhiteSpace(absolutePath) && File.Exists(absolutePath))
            {
                sizeBytes = new FileInfo(absolutePath).Length;
            }

            return new ResourceEntry
            {
                Name = Path.GetFileNameWithoutExtension(assetPath),
                Version = string.IsNullOrWhiteSpace(version) ? "1.0.0" : version,
                SizeBytes = sizeBytes,
                FullPath = assetPath.Replace('\\', '/'),
                AssetType = mainType,
                Labels = labels,
                Dependencies = Array.Empty<string>(),
                Kind = ResolveEntryKind(assetPath, mainType)
            };
        }

        private static ResourceEntryKind ResolveEntryKind(string assetPath, Type assetType)
        {
            if (string.Equals(Path.GetExtension(assetPath), ".unity", StringComparison.OrdinalIgnoreCase))
            {
                return ResourceEntryKind.Scene;
            }

            if (assetType != null && typeof(UnityEngine.Object).IsAssignableFrom(assetType))
            {
                return ResourceEntryKind.Asset;
            }

            return ResourceEntryKind.RawFile;
        }

        private sealed class DirectoryCollector : IResourceEntryCollector
        {
            public ResourcePackageCollectionStrategy Strategy => ResourcePackageCollectionStrategy.Directory;

            public void Collect(ResourcePackageDefinition package, ICollection<ResourceEntry> results)
            {
                var roots = package.CollectRoots.Count == 0 ? new[] { "Assets" } : package.CollectRoots.ToArray();
                var hasSearchFilter = package.SearchExtensions.Count > 0;
                var hasExcludeFilter = package.ExcludePatterns.Count > 0;

                for (var i = 0; i < roots.Length; i++)
                {
                    var absoluteRoot = ToAbsolutePath(roots[i]);
                    if (string.IsNullOrWhiteSpace(absoluteRoot) || !Directory.Exists(absoluteRoot))
                    {
                        continue;
                    }

                    var files = Directory.GetFiles(
                        absoluteRoot,
                        "*",
                        package.IncludeSubDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

                    for (var j = 0; j < files.Length; j++)
                    {
                        var assetPath = ToAssetPath(files[j]);
                        if (string.IsNullOrWhiteSpace(assetPath) || AssetDatabase.IsValidFolder(assetPath))
                        {
                            continue;
                        }

                        var ext = Path.GetExtension(assetPath).ToLowerInvariant();
                        if (hasSearchFilter && !package.SearchExtensions.Contains(ext))
                        {
                            continue;
                        }

                        if (hasExcludeFilter && package.ExcludePatterns.Contains(ext))
                        {
                            continue;
                        }

                        AddUniqueEntry(results, CreateEntryFromAssetPath(assetPath, package.Version));
                    }
                }
            }
        }

        private sealed class LabelCollector : IResourceEntryCollector
        {
            public ResourcePackageCollectionStrategy Strategy => ResourcePackageCollectionStrategy.Label;

            public void Collect(ResourcePackageDefinition package, ICollection<ResourceEntry> results)
            {
                if (package.Labels.Count == 0)
                {
                    return;
                }

                var roots = package.CollectRoots.Count == 0 ? new[] { "Assets" } : package.CollectRoots.ToArray();
                for (var i = 0; i < package.Labels.Count; i++)
                {
                    var guids = AssetDatabase.FindAssets($"l:{package.Labels[i]}", roots);
                    for (var j = 0; j < guids.Length; j++)
                    {
                        var assetPath = AssetDatabase.GUIDToAssetPath(guids[j]);
                        if (string.IsNullOrWhiteSpace(assetPath) ||
                            AssetDatabase.IsValidFolder(assetPath) ||
                            IsExcluded(assetPath, package.ExcludePatterns))
                        {
                            continue;
                        }

                        AddUniqueEntry(results, CreateEntryFromAssetPath(assetPath, package.Version));
                    }
                }
            }
        }

        private sealed class TypeCollector : IResourceEntryCollector
        {
            public ResourcePackageCollectionStrategy Strategy => ResourcePackageCollectionStrategy.Type;

            public void Collect(ResourcePackageDefinition package, ICollection<ResourceEntry> results)
            {
                if (string.IsNullOrWhiteSpace(package.TypeName))
                {
                    return;
                }

                var roots = package.CollectRoots.Count == 0 ? new[] { "Assets" } : package.CollectRoots.ToArray();
                var guids = AssetDatabase.FindAssets($"t:{package.TypeName}", roots);
                for (var i = 0; i < guids.Length; i++)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrWhiteSpace(assetPath) ||
                        AssetDatabase.IsValidFolder(assetPath) ||
                        IsExcluded(assetPath, package.ExcludePatterns))
                    {
                        continue;
                    }

                    AddUniqueEntry(results, CreateEntryFromAssetPath(assetPath, package.Version));
                }
            }
        }

        private sealed class DependencyCollector : IResourceEntryCollector
        {
            public ResourcePackageCollectionStrategy Strategy => ResourcePackageCollectionStrategy.Dependency;

            public void Collect(ResourcePackageDefinition package, ICollection<ResourceEntry> results)
            {
                if (string.IsNullOrWhiteSpace(package.RootAssetPath))
                {
                    return;
                }

                var dependencies = AssetDatabase.GetDependencies(package.RootAssetPath, true);
                for (var i = 0; i < dependencies.Length; i++)
                {
                    var assetPath = dependencies[i];
                    if (string.IsNullOrWhiteSpace(assetPath) ||
                        AssetDatabase.IsValidFolder(assetPath) ||
                        IsExcluded(assetPath, package.ExcludePatterns))
                    {
                        continue;
                    }

                    var mainType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                    if (mainType == typeof(MonoScript))
                    {
                        continue;
                    }

                    AddUniqueEntry(results, CreateEntryFromAssetPath(assetPath, package.Version));
                }
            }
        }

        private sealed class QueryCollector : IResourceEntryCollector
        {
            public ResourcePackageCollectionStrategy Strategy => ResourcePackageCollectionStrategy.Query;

            public void Collect(ResourcePackageDefinition package, ICollection<ResourceEntry> results)
            {
                if (string.IsNullOrWhiteSpace(package.Query))
                {
                    return;
                }

                var roots = package.CollectRoots.Count == 0 ? new[] { "Assets" } : package.CollectRoots.ToArray();
                var guids = AssetDatabase.FindAssets(package.Query, roots);
                for (var i = 0; i < guids.Length; i++)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrWhiteSpace(assetPath) ||
                        AssetDatabase.IsValidFolder(assetPath) ||
                        IsExcluded(assetPath, package.ExcludePatterns))
                    {
                        continue;
                    }

                    AddUniqueEntry(results, CreateEntryFromAssetPath(assetPath, package.Version));
                }
            }
        }
    }
}
