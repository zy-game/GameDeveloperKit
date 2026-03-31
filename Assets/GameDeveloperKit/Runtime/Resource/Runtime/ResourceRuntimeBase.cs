using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源运行时基类，提供资源加载的抽象实现。
    /// </summary>
    public abstract class ResourceRuntimeBase : IResourceRuntime
    {
        /// <summary>
        /// 异步初始化资源包。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public virtual UniTask InitializePackageAsync(ResourcePackageContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 异步确保资源包准备就绪。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public virtual UniTask EnsurePackageReadyAsync(ResourcePackageContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 同步加载资源。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="entry">资源条目。</param>
        /// <returns>加载的Unity对象。</returns>
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

        /// <summary>
        /// 异步加载资源。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="entry">资源条目。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>加载的Unity对象的异步任务。</returns>
        public virtual UniTask<UnityEngine.Object> LoadAssetAsync(ResourcePackageContext context, ResourceEntry entry, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.FromResult(LoadAsset(context, entry));
        }

        /// <summary>
        /// 解析场景路径。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="entry">资源条目。</param>
        /// <returns>场景路径。</returns>
        public virtual string ResolveScenePath(ResourcePackageContext context, ResourceEntry entry)
        {
            return ResolveFilePath(context, entry);
        }

        /// <summary>
        /// 解析文件路径。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="entry">资源条目。</param>
        /// <returns>文件路径。</returns>
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

        /// <summary>
        /// 构建资源条目列表。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <returns>资源条目列表。</returns>
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
                    BundleName = entry.BundleName
                });
            }

            return results;
        }

        /// <summary>
        /// 从清单加载资源条目。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <returns>资源条目列表。</returns>
        protected virtual List<ResourceEntry> LoadManifestEntries(ResourcePackageContext context)
        {
            var manifestPath = ResolveManifestPath(context);
            var manifest = ResourceManifestUtility.LoadFromFile(manifestPath);
            return ResourceManifestUtility.ToEntries(manifest, context?.PackageName);
        }

        /// <summary>
        /// 解析清单路径。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <returns>清单路径。</returns>
        protected virtual string ResolveManifestPath(ResourcePackageContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.ManifestRelativePath))
            {
                return string.Empty;
            }

            return context.ResolvePersistentPath(context.ManifestRelativePath);
        }

        /// <summary>
        /// 根据模式解析路径。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="path">要解析的路径。</param>
        /// <returns>解析后的路径。</returns>
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

        /// <summary>
        /// 规范化Resources目录路径。
        /// </summary>
        /// <param name="entry">资源条目。</param>
        /// <returns>规范化后的路径。</returns>
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
