using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源运行时接口，定义资源加载的核心功能。
    /// </summary>
    public interface IResourceRuntime
    {
        /// <summary>
        /// 异步初始化资源包。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        UniTask InitializePackageAsync(ResourcePackageContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步确保资源包准备就绪。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        UniTask EnsurePackageReadyAsync(ResourcePackageContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// 同步加载资源。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="entry">资源条目。</param>
        /// <returns>加载的Unity对象。</returns>
        Object LoadAsset(ResourcePackageContext context, ResourceEntry entry);

        /// <summary>
        /// 异步加载资源。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="entry">资源条目。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>加载的Unity对象的异步任务。</returns>
        UniTask<Object> LoadAssetAsync(ResourcePackageContext context, ResourceEntry entry, CancellationToken cancellationToken = default);

        /// <summary>
        /// 解析场景路径。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="entry">资源条目。</param>
        /// <returns>场景路径。</returns>
        string ResolveScenePath(ResourcePackageContext context, ResourceEntry entry);

        /// <summary>
        /// 解析文件路径。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="entry">资源条目。</param>
        /// <returns>文件路径。</returns>
        string ResolveFilePath(ResourcePackageContext context, ResourceEntry entry);

        /// <summary>
        /// 构建资源条目列表。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <returns>资源条目列表。</returns>
        IReadOnlyList<ResourceEntry> BuildEntries(ResourcePackageContext context);
    }
}
