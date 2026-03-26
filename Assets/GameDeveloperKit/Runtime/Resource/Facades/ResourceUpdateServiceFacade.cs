using System.Threading;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源更新服务门面，提供对资源模块的资源更新功能的访问。
    /// </summary>
    public sealed class ResourceUpdateServiceFacade : IResourceUpdateService
    {
        private readonly ResourceModule _module;

        /// <summary>
        /// 初始化资源更新服务门面的新实例。
        /// </summary>
        /// <param name="module">资源模块。</param>
        public ResourceUpdateServiceFacade(ResourceModule module)
        {
            _module = module;
        }

        /// <summary>
        /// 异步准备资源包。
        /// </summary>
        /// <param name="packageName">资源包名称，如果为null则准备所有包。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public UniTask PreparePackageAsync(string packageName = null, CancellationToken cancellationToken = default)
        {
            return _module.PreparePackageAsync(packageName, cancellationToken);
        }

        /// <summary>
        /// 异步更新资源包。
        /// </summary>
        /// <param name="packageName">资源包名称，如果为null则更新所有包。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>更新结果。</returns>
        public UniTask<ResourceUpdateResult> UpdatePackageAsync(string packageName = null, CancellationToken cancellationToken = default)
        {
            return _module.UpdatePackageAsync(packageName, cancellationToken);
        }

        /// <summary>
        /// 异步更新所有资源包。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>所有包的更新结果列表。</returns>
        public UniTask<IReadOnlyList<ResourceUpdateResult>> UpdateAllPackagesAsync(CancellationToken cancellationToken = default)
        {
            return _module.UpdateAllPackagesAsync(cancellationToken);
        }

        /// <summary>
        /// 异步准备所有资源包。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public UniTask PrepareAllPackagesAsync(CancellationToken cancellationToken = default)
        {
            return _module.PrepareAllPackagesAsync(cancellationToken);
        }

        /// <summary>
        /// 获取资源包的状态。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <returns>资源包状态。</returns>
        public ResourcePackageState GetPackageState(string packageName)
        {
            return _module.GetPackageState(packageName);
        }

        /// <summary>
        /// 获取资源包的最后错误信息。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <returns>错误信息字符串。</returns>
        public string GetPackageLastError(string packageName)
        {
            return _module.GetPackageLastError(packageName);
        }

        /// <summary>
        /// 获取资源包的更新报告。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <returns>资源更新报告。</returns>
        public ResourceUpdateReport GetPackageUpdateReport(string packageName)
        {
            return _module.GetPackageUpdateReport(packageName);
        }
    }
}
