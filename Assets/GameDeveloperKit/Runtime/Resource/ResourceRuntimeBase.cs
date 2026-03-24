using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    public abstract class ResourceRuntimeBase : IResourceRuntime
    {
        public virtual UniTask InitializePackageAsync(ResourcePackageContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        public virtual UniTask EnsurePackageReadyAsync(ResourcePackageContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        public virtual UnityEngine.Object LoadAsset(ResourcePackageContext context, ResourceEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            var resourcePath = NormalizeResourcesPath(entry);
            if (!string.IsNullOrWhiteSpace(resourcePath))
            {
                if (entry.AssetType != null)
                {
                    var loaded = Resources.Load(resourcePath, entry.AssetType);
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
                else
                {
                    var loaded = Resources.Load(resourcePath);
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
            }

            var filePath = ResolveFilePath(context, entry);
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            if (entry.AssetType == typeof(TextAsset) || entry.AssetType == null)
            {
                return new TextAsset(File.ReadAllText(filePath));
            }

            return null;
        }

        public virtual UniTask<UnityEngine.Object> LoadAssetAsync(ResourcePackageContext context, ResourceEntry entry, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.FromResult(LoadAsset(context, entry));
        }

        public virtual string ResolveScenePath(ResourcePackageContext context, ResourceEntry entry)
        {
            return ResolveFilePath(context, entry);
        }

        public virtual string ResolveFilePath(ResourcePackageContext context, ResourceEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(entry.FullPath))
            {
                return ResolvePathByMode(context, entry.FullPath);
            }

            return ResolvePathByMode(context, entry.Name);
        }

        public virtual IReadOnlyList<ResourceEntry> BuildEntries(ResourcePackageContext context)
        {
            var manifestEntries = LoadManifestEntries(context);
            if (manifestEntries.Count > 0)
            {
                return manifestEntries;
            }

            var results = new List<ResourceEntry>();
            if (context?.Definition?.Entries == null)
            {
                return results;
            }

            for (var i = 0; i < context.Definition.Entries.Count; i++)
            {
                var entry = context.Definition.Entries[i];
                if (entry == null)
                {
                    continue;
                }

                results.Add(new ResourceEntry
                {
                    Name = entry.Name,
                    Version = entry.Version,
                    Hash = entry.Hash,
                    SizeBytes = entry.SizeBytes,
                    AssetType = entry.AssetType,
                    Labels = entry.Labels == null ? null : new List<string>(entry.Labels),
                    Dependencies = entry.Dependencies == null ? null : new List<string>(entry.Dependencies),
                    FullPath = entry.FullPath,
                    Kind = entry.Kind
                });
            }

            return results;
        }

        protected virtual List<ResourceEntry> LoadManifestEntries(ResourcePackageContext context)
        {
            var manifestPath = ResolveManifestPath(context);
            var manifest = ResourceManifestUtility.LoadFromFile(manifestPath);
            return ResourceManifestUtility.ToEntries(manifest);
        }

        protected virtual string ResolveManifestPath(ResourcePackageContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.ManifestRelativePath))
            {
                return string.Empty;
            }

            return context.ResolvePersistentPath(context.ManifestRelativePath);
        }

        protected virtual string ResolvePathByMode(ResourcePackageContext context, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return context.ResolvePersistentPath(path);
        }

        protected static string NormalizeResourcesPath(ResourceEntry entry)
        {
            var path = entry?.Name;
            if (string.IsNullOrWhiteSpace(path))
            {
                path = entry?.FullPath;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            path = path.Replace('\\', '/');
            var resourcesIndex = path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
            if (resourcesIndex >= 0)
            {
                path = path.Substring(resourcesIndex + "/Resources/".Length);
            }

            var extension = Path.GetExtension(path);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                path = Path.ChangeExtension(path, null);
            }

            return path.TrimStart('/');
        }
    }
}
