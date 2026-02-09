using System;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 场景提供者接口
    /// </summary>
    public interface ISceneProvider
    {
        /// <summary>
        /// 异步加载场景
        /// </summary>
        UniTask<SceneHandle> LoadSceneAsync(ResourceLocation location, LoadSceneMode mode, Action<float> progressHandler = null);
    }
}