using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Config
{
    /// <summary>
    /// 配置加载器（基于 ResourceModule）
    /// </summary>
    public static class ConfigLoader
    {
        /// <summary>
        /// 从 JSON TextAsset 加载配置
        /// </summary>
        public static async UniTask<T[]> LoadFromJsonAsync<T>(string address)
        {
            var handle = await Game.Resource.LoadAssetAsync<TextAsset>(address);
            if (handle.Asset == null)
            {
                Game.Debug.Error($"Failed to load config asset: {address}");
                return null;
            }

            try
            {
                var json = handle.Asset.text;
                var wrapper = JsonUtility.FromJson<ConfigWrapper<T>>(json);
                return wrapper?.Datas;
            }
            catch (Exception ex)
            {
                Game.Debug.Error($"Failed to parse config JSON: {address}, Error: {ex.Message}");
                return null;
            }
            finally
            {
                handle.Release();
            }
        }

        /// <summary>
        /// 从 ScriptableObject 加载配置
        /// </summary>
        public static async UniTask<T[]> LoadFromScriptableObjectAsync<T>(string address) where T : IConfigData
        {
            var handle = await Game.Resource.LoadAssetAsync<ScriptableObject>(address);
            if (handle.Asset == null)
            {
                Game.Debug.Error($"Failed to load config asset: {address}");
                return null;
            }

            try
            {
                var configAsset = handle.Asset as IConfigAsset<T>;
                if (configAsset == null)
                {
                    Game.Debug.Error($"Asset {address} does not implement IConfigAsset<{typeof(T).Name}>");
                    return null;
                }
                return configAsset.Datas;
            }
            finally
            {
                handle.Release();
            }
        }

        [Serializable]
        private class ConfigWrapper<T>
        {
            public T[] Datas;
        }
    }

    /// <summary>
    /// ScriptableObject 配置资源接口
    /// </summary>
    public interface IConfigAsset<T> where T : IConfigData
    {
        T[] Datas { get; }
    }
}
