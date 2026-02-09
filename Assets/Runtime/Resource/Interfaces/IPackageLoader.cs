using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源包加载器接口
    /// 负责资源包的初始化流程
    /// </summary>
    public interface IPackageLoader
    {
        /// <summary>
        /// 异步加载清单
        /// </summary>
        UniTask<PackageManifest> LoadManifestAsync();
        
        /// <summary>
        /// 准备资源（下载、校验等）
        /// </summary>
        UniTask<bool> PrepareResourcesAsync(PackageManifest manifest);
    }
}
