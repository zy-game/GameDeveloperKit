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
        private static void WriteExecutionReport(ResourceBuildPipelineContext context, bool success, string errorMessage)
        {
            try
            {
                var report = new ResourceBuildExecutionReport
                {
                    PackageName = context.PackageName,
                    PackageVersion = context.PackageVersion,
                    Success = success,
                    ErrorMessage = errorMessage ?? string.Empty,
                    BuildStartUtc = context.BuildStartUtc.ToString("O"),
                    BuildEndUtc = context.BuildEndUtc.ToString("O"),
                    DurationSeconds = Math.Max(0d, (context.BuildEndUtc - context.BuildStartUtc).TotalSeconds),
                    OutputPath = context.BuildRoot,
                    BundleCount = context.BuiltBundles.Count,
                    TotalSizeBytes = context.BuiltBundles.Sum(static item => item.SizeBytes),
                    Bundles = context.BuiltBundles == null ? new List<ResourceBuiltBundleRecord>() : new List<ResourceBuiltBundleRecord>(context.BuiltBundles),
                    Logs = context.BuildLogs == null ? new List<string>() : new List<string>(context.BuildLogs)
                };

                var reportJson = JsonUtility.ToJson(report, true);
                var reportDir = Path.GetDirectoryName(context.ReportPath);
                if (!string.IsNullOrWhiteSpace(reportDir))
                {
                    Directory.CreateDirectory(reportDir);
                }

                File.WriteAllText(context.ReportPath, reportJson);

                var lines = new List<string>
                {
                    $"Success: {report.Success}",
                    $"Package: {report.PackageName}",
                    $"Version: {report.PackageVersion}",
                    $"DurationSeconds: {report.DurationSeconds:F2}",
                    $"BundleCount: {report.BundleCount}",
                    $"TotalSizeBytes: {report.TotalSizeBytes}",
                    $"OutputPath: {report.OutputPath}"
                };

                if (!string.IsNullOrWhiteSpace(report.ErrorMessage))
                {
                    lines.Add($"Error: {report.ErrorMessage}");
                }

                lines.Add("Logs:");
                lines.AddRange(report.Logs);
                File.WriteAllText(context.ReportTextPath, string.Join(Environment.NewLine, lines));

                var historyFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{NormalizeToken(context.PackageName)}_{NormalizeToken(context.PackageVersion)}.json";
                var historyPath = Path.Combine(context.HistoryRoot, historyFileName);
                File.WriteAllText(historyPath, reportJson);
            }
            catch (Exception ex)
            {
                context.LogError($"Failed to write build report: {ex.Message}");
            }
        }

        private static ResourceVersionManifest LoadGlobalManifest(string path)
        {
            if (!File.Exists(path))
            {
                return new ResourceVersionManifest
                {
                    Version = "1.0",
                    UpdateTimeUtc = DateTime.UtcNow.ToString("O"),
                    Packages = new List<ResourcePackageVersionInfo>()
                };
            }

            try
            {
                var json = File.ReadAllText(path);
                var manifest = JsonUtility.FromJson<ResourceVersionManifest>(json);
                if (manifest == null)
                {
                    return new ResourceVersionManifest
                    {
                        Version = "1.0",
                        UpdateTimeUtc = DateTime.UtcNow.ToString("O"),
                        Packages = new List<ResourcePackageVersionInfo>()
                    };
                }

                manifest.Packages ??= new List<ResourcePackageVersionInfo>();
                for (var i = 0; i < manifest.Packages.Count; i++)
                {
                    manifest.Packages[i].Versions ??= new List<ResourceVersionDetail>();
                }

                return manifest;
            }
            catch
            {
                return new ResourceVersionManifest
                {
                    Version = "1.0",
                    UpdateTimeUtc = DateTime.UtcNow.ToString("O"),
                    Packages = new List<ResourcePackageVersionInfo>()
                };
            }
        }

        private static void SaveGlobalManifest(string path, ResourceVersionManifest manifest)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonUtility.ToJson(manifest, true));
        }

        private static string ResolveAssetPath(ResourceEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            var candidate = string.IsNullOrWhiteSpace(entry.FullPath) ? entry.Name : entry.FullPath;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return null;
            }

            var assetPath = NormalizeAssetPath(candidate);
            if (!assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(assetPath, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null || asset is DefaultAsset)
            {
                return null;
            }

            return assetPath;
        }

        private static string ResolveBundleName(
            ResourcePackageDefinition package,
            IReadOnlyList<string> roots,
            ResourceEntry entry,
            string assetPath,
            int index)
        {
            var baseName = NormalizeToken(string.IsNullOrWhiteSpace(package.BundleNameOverride) ? package.PackageName : package.BundleNameOverride);
            switch (package.BuildStrategy)
            {
                case ResourcePackageBuildStrategy.Dir:
                    return $"{baseName}_{ResolveDirectoryGroup(roots, assetPath)}";
                case ResourcePackageBuildStrategy.Label:
                    return $"{baseName}_{ResolveLabelGroup(entry)}";
                case ResourcePackageBuildStrategy.OneFile:
                default:
                    return $"{baseName}_{ResolveOneFileGroup(assetPath, index)}";
            }
        }

        private static string ResolveDirectoryGroup(IReadOnlyList<string> roots, string assetPath)
        {
            for (var i = 0; i < roots.Count; i++)
            {
                var root = roots[i];
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                if (!assetPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var relative = assetPath.Substring(root.Length).TrimStart('/');
                if (string.IsNullOrWhiteSpace(relative))
                {
                    return "root";
                }

                var firstLevel = relative.Split('/')[0];
                return NormalizeToken(firstLevel);
            }

            var normalized = NormalizeAssetPath(assetPath);
            var segments = normalized.Split('/');
            return segments.Length > 1 ? NormalizeToken(segments[1]) : "root";
        }

        private static string ResolveLabelGroup(ResourceEntry entry)
        {
            if (entry?.Labels == null || entry.Labels.Count == 0)
            {
                return "unlabeled";
            }

            for (var i = 0; i < entry.Labels.Count; i++)
            {
                var label = entry.Labels[i];
                if (!string.IsNullOrWhiteSpace(label))
                {
                    return NormalizeToken(label);
                }
            }

            return "unlabeled";
        }

        private static string ResolveOneFileGroup(string assetPath, int index)
        {
            var normalized = NormalizeAssetPath(assetPath);
            normalized = Path.ChangeExtension(normalized, null)?.Replace('/', '_') ?? $"entry_{index}";
            normalized = NormalizeToken(normalized);
            return string.IsNullOrWhiteSpace(normalized) ? $"entry_{index}" : normalized;
        }

        private static string NormalizeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "default";
            }

            var source = value.Trim().ToLowerInvariant();
            var buffer = new char[source.Length];
            var write = 0;
            var previousIsSeparator = false;
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                if (char.IsLetterOrDigit(c))
                {
                    buffer[write++] = c;
                    previousIsSeparator = false;
                }
                else if (!previousIsSeparator)
                {
                    buffer[write++] = '_';
                    previousIsSeparator = true;
                }
            }

            if (write == 0)
            {
                return "default";
            }

            var result = new string(buffer, 0, write).Trim('_');
            return string.IsNullOrWhiteSpace(result) ? "default" : result;
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(sourceDir))
            {
                return;
            }

            Directory.CreateDirectory(targetDir);
            var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            for (var i = 0; i < files.Length; i++)
            {
                var relative = Path.GetRelativePath(sourceDir, files[i]);
                var target = Path.Combine(targetDir, relative);
                var targetFolder = Path.GetDirectoryName(target);
                if (!string.IsNullOrWhiteSpace(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                File.Copy(files[i], target, true);
            }
        }

        private static ResourceManifestEntry CloneManifestEntry(ResourceManifestEntry source)
        {
            if (source == null)
            {
                return null;
            }

            return new ResourceManifestEntry
            {
                Name = source.Name,
                Version = source.Version,
                Hash = source.Hash,
                SizeBytes = source.SizeBytes,
                AssetType = source.AssetType,
                Labels = source.Labels == null ? new List<string>() : new List<string>(source.Labels),
                Dependencies = source.Dependencies == null ? new List<string>() : new List<string>(source.Dependencies),
                BundleName = source.BundleName,
                FullPath = source.FullPath
            };
        }
    }
}
