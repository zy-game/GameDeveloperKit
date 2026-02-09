using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源提供者接口
    /// </summary>
    public interface IAssetProvider
    {
        /// <summary>
        /// 异步加载资源
        /// </summary>
        UniTask<AssetHandle<T>> LoadAsync<T>(ResourceLocation location) where T : UnityEngine.Object;

        /// <summary>
        /// 卸载资源
        /// </summary>
        void Unload(BaseHandle handle);
        
        /// <summary>
        /// 异步加载子资源（如 Atlas 中的 Sprite）
        /// </summary>
        UniTask<AssetHandle<T>> LoadSubAssetAsync<T>(ResourceLocation location, string subAssetName) 
            where T : UnityEngine.Object;
        
        /// <summary>
        /// 异步加载资源中的所有子对象（用于 Bundle 全加载）
        /// </summary>
        UniTask<List<AssetHandle<T>>> LoadAllAsync<T>(ResourceLocation location) 
            where T : UnityEngine.Object;
    }
}