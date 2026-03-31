using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.Runtime;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor
{
    internal sealed partial class ResourceBuildPipeline
    {
        private sealed class TaskCreateManifest : ISbpBuildTask
        {
            public string TaskName => "Create Manifest";

            public ResourceBuildTaskResult Run(ResourceBuildPipelineContext context)
            {
                if (context.BuiltBundles.Count == 0)
                {
                    return ResourceBuildTaskResult.Failed("No built bundles for manifest.");
                }

                var package = context.Request.Package;
                var sourceEntryLookup = BuildSourceEntryLookup(context.CollectedEntries);
                var entries = new List<ResourceManifestEntry>(context.CollectedEntries.Count);
                var entryPathDeduplicate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var packageDependencies = BuildPackageDependencies(context.Request.Settings, context.PackageName);
                var warnings = new List<string>();

                for (var i = 0; i < context.BuiltBundles.Count; i++)
                {
                    var bundle = context.BuiltBundles[i];
                    if (bundle.AssetPaths == null || bundle.AssetPaths.Count == 0)
                    {
                        warnings.Add($"Bundle '{bundle.BundleName}' has no mapped assets.");
                        continue;
                    }

                    for (var assetIndex = 0; assetIndex < bundle.AssetPaths.Count; assetIndex++)
                    {
                        var sourceAssetPath = NormalizeAssetPath(bundle.AssetPaths[assetIndex]);
                        if (string.IsNullOrWhiteSpace(sourceAssetPath))
                        {
                            continue;
                        }

                        if (!entryPathDeduplicate.Add(sourceAssetPath))
                        {
                            continue;
                        }

                        sourceEntryLookup.TryGetValue(sourceAssetPath, out var sourceEntry);
                        var sourceAssetType = sourceEntry?.AssetType ?? AssetDatabase.GetMainAssetTypeAtPath(sourceAssetPath);
                        var labels = sourceEntry?.Labels == null ? new List<string>() : new List<string>(sourceEntry.Labels);
                        var manifestPath = ToManifestEntryPath(sourceAssetPath);
                        var fileNameWithExtension = Path.GetFileName(manifestPath);
                        var sourceGuid = AssetDatabase.AssetPathToGUID(sourceAssetPath);
                        var version = package.BuildStrategy == ResourcePackageBuildStrategy.Dir
                            ? string.Empty
                            : context.PackageVersion;
                        var sizeBytes = ResolveFileSize(sourceAssetPath);

                        var bundleFileName = string.IsNullOrWhiteSpace(bundle.FileName)
                            ? bundle.BundleName
                            : bundle.FileName;

                        entries.Add(new ResourceManifestEntry
                        {
                            Name = fileNameWithExtension,
                            Version = version,
                            Hash = sourceGuid,
                            SizeBytes = sizeBytes,
                            AssetType = sourceAssetType?.AssemblyQualifiedName,
                            Labels = labels,
                            Dependencies = new List<string>(packageDependencies),
                            BundleName = bundleFileName,
                            FullPath = manifestPath
                        });
                    }
                }

                if (entries.Count == 0)
                {
                    return ResourceBuildTaskResult.Failed("No manifest entries were generated from built bundles.", warnings);
                }

                var manifest = new ResourceManifest
                {
                    AppVersion = Application.version,
                    BuildTimeUtc = DateTime.UtcNow.ToString("O"),
                    Version = context.PackageVersion
                };

                manifest.Packages.Add(new ResourceManifestPackage
                {
                    Name = context.PackageName,
                    Role = package.Role,
                    Version = context.PackageVersion,
                    BuildStrategy = package.BuildStrategy,
                    Entries = entries.Select(CloneManifestEntry).ToList()
                });

                ResourceManifestUtility.SaveToFile(manifest, context.PackageManifestPath);
                context.PackageManifest = manifest;
                context.Log($"Package manifest created: {context.PackageManifestPath}");
                return ResourceBuildTaskResult.Succeed(warnings);
            }

            private static string ToManifestEntryPath(string sourceAssetPath)
            {
                var normalized = NormalizeAssetPath(sourceAssetPath);
                return normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                    ? normalized.Substring("Assets/".Length)
                    : normalized;
            }

            private static Dictionary<string, ResourceEntry> BuildSourceEntryLookup(IReadOnlyList<ResourceEntry> sourceEntries)
            {
                var lookup = new Dictionary<string, ResourceEntry>(StringComparer.OrdinalIgnoreCase);
                if (sourceEntries == null)
                {
                    return lookup;
                }

                for (var i = 0; i < sourceEntries.Count; i++)
                {
                    var entry = sourceEntries[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.FullPath))
                    {
                        continue;
                    }

                    lookup[NormalizeAssetPath(entry.FullPath)] = entry;
                }

                return lookup;
            }

            private static long ResolveFileSize(string assetPath)
            {
                var fullPath = ResourceCollectionService.ToAbsolutePath(assetPath);
                if (!File.Exists(fullPath))
                {
                    return 0L;
                }

                return new FileInfo(fullPath).Length;
            }

            private static HashSet<string> BuildPackageDependencies(ResourceProjectSettingsData settings, string packageName)
            {
                var dependencies = new HashSet<string>(StringComparer.Ordinal);
                if (settings?.Packages == null)
                {
                    return dependencies;
                }

                var currentPackage = settings.Packages.FirstOrDefault(item =>
                    item != null &&
                    !string.IsNullOrWhiteSpace(item.PackageName) &&
                    string.Equals(item.PackageName, packageName, StringComparison.Ordinal));
                if (currentPackage?.Entries == null)
                {
                    return dependencies;
                }

                var pathToPackage = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < settings.Packages.Count; i++)
                {
                    var package = settings.Packages[i];
                    if (package?.Entries == null || string.IsNullOrWhiteSpace(package.PackageName))
                    {
                        continue;
                    }

                    for (var j = 0; j < package.Entries.Count; j++)
                    {
                        var entry = package.Entries[j];
                        if (entry == null || string.IsNullOrWhiteSpace(entry.FullPath))
                        {
                            continue;
                        }

                        pathToPackage[NormalizeAssetPath(entry.FullPath)] = package.PackageName;
                    }
                }

                for (var i = 0; i < currentPackage.Entries.Count; i++)
                {
                    var entry = currentPackage.Entries[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.FullPath))
                    {
                        continue;
                    }

                    var dependenciesPaths = AssetDatabase.GetDependencies(entry.FullPath, true);
                    for (var j = 0; j < dependenciesPaths.Length; j++)
                    {
                        var dependencyPath = NormalizeAssetPath(dependenciesPaths[j]);
                        if (string.IsNullOrWhiteSpace(dependencyPath))
                        {
                            continue;
                        }

                        if (!pathToPackage.TryGetValue(dependencyPath, out var ownerPackage))
                        {
                            continue;
                        }

                        if (string.Equals(ownerPackage, packageName, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        dependencies.Add(ownerPackage);
                    }
                }

                return dependencies;
            }
        }
    }
}
