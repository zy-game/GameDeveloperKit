using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源提供者系统
    /// 根据资源类型和定位信息分发给对应的 Provider
    /// </summary>
    public class ProviderSystem
    {
        private readonly Dictionary<ResourceLocationType, Dictionary<Type, IAssetProvider>> _providers = new Dictionary<ResourceLocationType, Dictionary<Type, IAssetProvider>>();

        private IAssetProvider _defaultBundleProvider;
        private IAssetProvider _defaultBuiltinProvider;
        private IAssetProvider _defaultRemoteProvider;

        private ISceneProvider _defaultBundleSceneProvider;
        private ISceneProvider _defaultBuiltinSceneProvider;
        private ISceneProvider _defaultRemoteSceneProvider;

        /// <summary>
        /// 注册 Provider
        /// </summary>
        public void RegisterProvider(ResourceLocationType locationType, Type assetType, IAssetProvider provider)
        {
            if (!_providers.ContainsKey(locationType))
            {
                _providers[locationType] = new Dictionary<Type, IAssetProvider>();
            }

            _providers[locationType][assetType] = provider;
        }

        /// <summary>
        /// 设置默认 Provider
        /// </summary>
        public void SetDefaultProvider(ResourceLocationType locationType, IAssetProvider provider)
        {
            switch (locationType)
            {
                case ResourceLocationType.Bundle:
                    _defaultBundleProvider = provider;
                    break;
                case ResourceLocationType.Builtin:
                    _defaultBuiltinProvider = provider;
                    break;
                case ResourceLocationType.Remote:
                    _defaultRemoteProvider = provider;
                    break;
            }
        }

        /// <summary>
        /// 设置场景 Provider
        /// </summary>
        public void SetSceneProvider(ResourceLocationType locationType, ISceneProvider provider)
        {
            switch (locationType)
            {
                case ResourceLocationType.Bundle:
                    _defaultBundleSceneProvider = provider;
                    break;
                case ResourceLocationType.Builtin:
                    _defaultBuiltinSceneProvider = provider;
                    break;
                case ResourceLocationType.Remote:
                    _defaultRemoteSceneProvider = provider;
                    break;
            }
        }

        /// <summary>
        /// 获取 Provider
        /// </summary>
        public IAssetProvider GetProvider<T>(ResourceLocation location) where T : UnityEngine.Object
        {
            if (location == null)
                return null;

            var assetType = typeof(T);

            // 查找特定类型的 Provider
            if (_providers.TryGetValue(location.LocationType, out var typeProviders))
            {
                if (typeProviders.TryGetValue(assetType, out var provider))
                    return provider;
            }

            // 返回默认 Provider
            return location.LocationType switch
            {
                ResourceLocationType.Bundle => _defaultBundleProvider,
                ResourceLocationType.Builtin => _defaultBuiltinProvider,
                ResourceLocationType.Remote => _defaultRemoteProvider,
                _ => null
            };
        }

        /// <summary>
        /// 获取场景 Provider
        /// </summary>
        public ISceneProvider GetSceneProvider(ResourceLocation location)
        {
            if (location == null)
                return null;

            // 查找场景 Provider（先查找注册的特定类型 Provider）
            if (_providers.TryGetValue(location.LocationType, out var typeProviders))
            {
                foreach (var provider in typeProviders.Values)
                {
                    if (provider is ISceneProvider sceneProvider)
                        return sceneProvider;
                }
            }

            // 返回默认场景 Provider
            return location.LocationType switch
            {
                ResourceLocationType.Bundle => _defaultBundleSceneProvider,
                ResourceLocationType.Builtin => _defaultBuiltinSceneProvider,
                ResourceLocationType.Remote => _defaultRemoteSceneProvider,
                _ => null
            };
        }

        /// <summary>
        /// 清理
        /// </summary>
        public void Clear()
        {
            _providers.Clear();
            _defaultBundleProvider = null;
            _defaultBuiltinProvider = null;
            _defaultRemoteProvider = null;
            _defaultBundleSceneProvider = null;
            _defaultBuiltinSceneProvider = null;
            _defaultRemoteSceneProvider = null;
        }
    }
}