using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源更新服务接口，用于管理资源包的更新和准备。
    /// </summary>
    public interface IResourceUpdateService
    {
        /// <summary>
        /// 异步更新指定名称的资源包。
        /// </summary>
        /// <param name="packageName">资源包名称，如果为null则更新默认包。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源更新结果的异步任务。</returns>
        UniTask<ResourceUpdateResult> UpdatePackageAsync(string packageName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步更新所有资源包。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源更新结果列表的异步任务。</returns>
        UniTask<IReadOnlyList<ResourceUpdateResult>> UpdateAllPackagesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步准备指定名称的资源包。
        /// </summary>
        /// <param name="packageName">资源包名称，如果为null则准备默认包。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        UniTask PreparePackageAsync(string packageName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步准备所有资源包。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        UniTask PrepareAllPackagesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取指定名称资源包的状态。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <returns>资源包状态。</returns>
        ResourcePackageState GetPackageState(string packageName);

        /// <summary>
        /// 获取指定名称资源包的最后错误信息。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <returns>错误信息字符串。</returns>
        string GetPackageLastError(string packageName);

        /// <summary>
        /// 获取指定名称资源包的更新报告。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <returns>资源更新报告。</returns>
        ResourceUpdateReport GetPackageUpdateReport(string packageName);
    }
}
