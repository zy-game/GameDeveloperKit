using System;
using System.Collections.Generic;
using GameDeveloperKit.Runtime;
using UnityEditor;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;

namespace GameDeveloperKit.Editor
{
    internal sealed class ResourceBuildPipelineContext
    {
        public ResourceBuildPipelineContext(ResourceBuildPipelineRequest request)
        {
            Request = request;
        }

        public ResourceBuildPipelineRequest Request { get; }

        public string PackageName { get; set; }

        public string PackageVersion { get; set; }

        public string BuildRoot { get; set; }

        public string BundleOutputRoot { get; set; }

        public string PackageManifestPath { get; set; }

        public string GlobalManifestPath { get; set; }

        public string HistoryRoot { get; set; }

        public string ReportPath { get; set; }

        public string ReportTextPath { get; set; }

        public DateTime BuildStartUtc { get; set; }

        public DateTime BuildEndUtc { get; set; }

        public List<ResourceEntry> CollectedEntries { get; set; } = new();

        public Dictionary<string, List<string>> BundleAssetMap { get; } = new(StringComparer.Ordinal);

        public List<UnityEditor.AssetBundleBuild> AssetBundleBuilds { get; } = new();

        public IBundleBuildResults SbpBuildResults { get; set; }

        public Dictionary<string, string> BundleOutputNameMap { get; } = new(StringComparer.Ordinal);

        public List<ResourceBuiltBundleRecord> BuiltBundles { get; } = new();

        public ResourceManifest PackageManifest { get; set; }

        public List<string> BuildLogs { get; } = new();

        public void Log(string message)
        {
            var content = $"[{DateTime.Now:HH:mm:ss}] {message}";
            BuildLogs.Add(content);
            Debug.Log($"[ResourceBuildPipeline] {message}");
        }

        public void LogWarning(string message)
        {
            var content = $"[{DateTime.Now:HH:mm:ss}] WARNING: {message}";
            BuildLogs.Add(content);
            Debug.LogWarning($"[ResourceBuildPipeline] {message}");
        }

        public void LogError(string message)
        {
            var content = $"[{DateTime.Now:HH:mm:ss}] ERROR: {message}";
            BuildLogs.Add(content);
            Debug.LogError($"[ResourceBuildPipeline] {message}");
        }
    }
}
