using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.CodeAnalysis
{
    [Serializable]
    internal sealed class AnalyzerDeploymentManifest
    {
        [SerializeField] private int schemaVersion;
        [SerializeField] private AnalyzerDeploymentComponent[] components;
        [SerializeField] private AnalyzerDeploymentComponent[] externalComponents;

        public int SchemaVersion => schemaVersion;
        public IReadOnlyList<AnalyzerDeploymentComponent> Components => components;
        public IReadOnlyList<AnalyzerDeploymentComponent> ExternalComponents => externalComponents;
    }

    [Serializable]
    internal sealed class AnalyzerDeploymentComponent
    {
        [SerializeField] private string name;
        [SerializeField] private string project;
        [SerializeField] private string artifact;
        [SerializeField] private string unityAsset;

        public string Name => name;
        public string Project => project;
        public string Artifact => artifact;
        public string UnityAsset => unityAsset;
    }

    internal static class AnalyzerDeploymentManifestAsset
    {
        private const string PackageIdentity = "com.gamedeveloperkit.framework";
        private const string ManifestRelativePath = "Editor/CodeAnalysis/AnalyzerDeploymentManifest.json";
        private const string DeploymentRelativeDirectory = "Analyzers";

        internal static string AssetPath
        {
            get
            {
                ResolvePackagePaths(out var assetPath, out _);
                return assetPath;
            }
        }

        internal static string DeploymentDirectory
        {
            get
            {
                ResolvePackagePaths(out _, out var packageRoot);
                return $"{packageRoot}/{DeploymentRelativeDirectory}";
            }
        }

        public static AnalyzerDeploymentManifest Load()
        {
            return Load(out _, out _);
        }

        internal static AnalyzerDeploymentManifest Load(out string packageRoot, out string manifestAssetPath)
        {
            ResolvePackagePaths(out manifestAssetPath, out packageRoot);
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(manifestAssetPath);
            if (textAsset == null)
            {
                throw new InvalidOperationException($"Analyzer deployment manifest is missing: {manifestAssetPath}");
            }

            var manifest = JsonUtility.FromJson<AnalyzerDeploymentManifest>(textAsset.text);
            Validate(manifest);
            return manifest;
        }

        internal static string ResolveUnityAssetPath(string packageRoot, AnalyzerDeploymentComponent component)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            return ResolveUnityAssetPath(packageRoot, component.UnityAsset);
        }

        internal static bool ContainsRelevantPath(string[] paths)
        {
            if (paths == null)
            {
                return false;
            }

            for (var i = 0; i < paths.Length; i++)
            {
                var path = NormalizePath(paths[i]);
                if (path.EndsWith("/" + ManifestRelativePath, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            ResolvePackagePaths(out var manifestAssetPath, out var packageRoot);
            var deploymentPrefix = $"{packageRoot}/{DeploymentRelativeDirectory}/";
            var manifest = Load(out _, out _);
            for (var i = 0; i < paths.Length; i++)
            {
                var path = NormalizePath(paths[i]);
                if (string.Equals(path, manifestAssetPath, StringComparison.Ordinal) ||
                    path.StartsWith(deploymentPrefix, StringComparison.Ordinal))
                {
                    return true;
                }

                for (var componentIndex = 0; componentIndex < manifest.ExternalComponents.Count; componentIndex++)
                {
                    if (string.Equals(path, ResolveUnityAssetPath(packageRoot, manifest.ExternalComponents[componentIndex]), StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void Validate(AnalyzerDeploymentManifest manifest)
        {
            if (manifest == null || manifest.SchemaVersion != 1)
            {
                throw new InvalidOperationException("Analyzer deployment manifest schema must be 1.");
            }

            if (manifest.Components == null || manifest.Components.Count == 0)
            {
                throw new InvalidOperationException("Analyzer deployment manifest contains no components.");
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            var destinations = new HashSet<string>(StringComparer.Ordinal);
            foreach (var component in manifest.Components)
            {
                if (component == null ||
                    string.IsNullOrWhiteSpace(component.Name) ||
                    string.IsNullOrWhiteSpace(component.Project) ||
                    string.IsNullOrWhiteSpace(component.Artifact) ||
                    string.IsNullOrWhiteSpace(component.UnityAsset))
                {
                    throw new InvalidOperationException("Analyzer deployment component contains an empty field.");
                }

                if (!names.Add(component.Name))
                {
                    throw new InvalidOperationException($"Duplicate analyzer component name: {component.Name}");
                }

                var unityAsset = NormalizeRelativePath(component.UnityAsset);
                if (!unityAsset.StartsWith(DeploymentRelativeDirectory + "/", StringComparison.Ordinal) ||
                    !component.UnityAsset.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                    !destinations.Add(unityAsset))
                {
                    throw new InvalidOperationException($"Invalid analyzer deployment destination: {component.UnityAsset}");
                }
            }

            if (manifest.ExternalComponents == null)
            {
                throw new InvalidOperationException("Analyzer deployment manifest external components are missing.");
            }

            foreach (var component in manifest.ExternalComponents)
            {
                if (component == null ||
                    string.IsNullOrWhiteSpace(component.Name) ||
                    string.IsNullOrWhiteSpace(component.UnityAsset))
                {
                    throw new InvalidOperationException("External analyzer component contains an empty field.");
                }

                var unityAsset = NormalizeRelativePath(component.UnityAsset);
                if (!names.Add(component.Name) ||
                    !unityAsset.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                    !destinations.Add(unityAsset))
                {
                    throw new InvalidOperationException($"Invalid external analyzer component: {component.Name} ({component.UnityAsset})");
                }
            }
        }

        private static string ResolveUnityAssetPath(string packageRoot, string relativePath)
        {
            var normalizedPath = NormalizeRelativePath(relativePath);
            return $"{packageRoot}/{normalizedPath}";
        }

        private static string NormalizeRelativePath(string path)
        {
            var normalizedPath = NormalizePath(path).Trim('/');
            if (string.IsNullOrWhiteSpace(normalizedPath) ||
                Path.IsPathRooted(path) ||
                normalizedPath.Equals("..", StringComparison.Ordinal) ||
                normalizedPath.StartsWith("../", StringComparison.Ordinal) ||
                normalizedPath.Contains("/../"))
            {
                throw new InvalidOperationException($"Analyzer deployment path must be package-relative: {path}");
            }

            return normalizedPath;
        }

        private static void ResolvePackagePaths(out string manifestAssetPath, out string packageRoot)
        {
            var matches = new List<string>();
            var guids = AssetDatabase.FindAssets("AnalyzerDeploymentManifest t:TextAsset");
            for (var i = 0; i < guids.Length; i++)
            {
                var candidate = NormalizePath(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (candidate.EndsWith("/" + ManifestRelativePath, StringComparison.Ordinal) is false)
                {
                    continue;
                }

                var candidateRoot = candidate.Substring(0, candidate.Length - ManifestRelativePath.Length - 1);
                var packageAsset = AssetDatabase.LoadAssetAtPath<TextAsset>($"{candidateRoot}/package.json");
                if (packageAsset == null)
                {
                    continue;
                }

                var identity = JsonUtility.FromJson<PackageIdentityInfo>(packageAsset.text);
                if (string.Equals(identity?.Name, PackageIdentity, StringComparison.Ordinal))
                {
                    matches.Add(candidate);
                }
            }

            if (matches.Count != 1)
            {
                throw new InvalidOperationException($"Expected one {PackageIdentity} analyzer deployment manifest, found {matches.Count}.");
            }

            manifestAssetPath = matches[0];
            packageRoot = manifestAssetPath.Substring(0, manifestAssetPath.Length - ManifestRelativePath.Length - 1);
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        [Serializable]
        private sealed class PackageIdentityInfo
        {
            [SerializeField] private string name;

            public string Name => name;
        }
    }
}
