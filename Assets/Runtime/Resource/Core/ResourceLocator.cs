using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源定位器
    /// 负责构建和查询资源索引
    /// </summary>
    public class ResourceLocator
    {
        private readonly Dictionary<string, ResourceLocation> _guidMap = new Dictionary<string, ResourceLocation>();
        private readonly Dictionary<string, ResourceLocation> _addressMap = new Dictionary<string, ResourceLocation>();
        private readonly Dictionary<string, ResourceLocation> _nameMap = new Dictionary<string, ResourceLocation>();
        private readonly Dictionary<string, ResourceLocation> _pathMap = new Dictionary<string, ResourceLocation>();
        private readonly Dictionary<string, List<ResourceLocation>> _labelMap = new Dictionary<string, List<ResourceLocation>>();
        private readonly Dictionary<string, List<ResourceLocation>> _bundleMap = new Dictionary<string, List<ResourceLocation>>();
        private readonly Dictionary<string, List<ResourceLocation>> _typeMap = new Dictionary<string, List<ResourceLocation>>();

        /// <summary>
        /// 从 PackageManifest 构建索引
        /// </summary>
        public void BuildIndex(PackageManifest manifest)
        {
            if (manifest == null || manifest.bundles == null)
            {
                Game.Debug.Warning("Manifest is null or empty, cannot build index");
                return;
            }

            _guidMap.Clear();
            _addressMap.Clear();
            _nameMap.Clear();
            _labelMap.Clear();
            _bundleMap.Clear();
            _pathMap.Clear();
            _typeMap.Clear();

            foreach (var bundleManifest in manifest.bundles)
            {
                if (bundleManifest.resources == null)
                    continue;

                foreach (var assetInfo in bundleManifest.resources)
                {
                    var location = new ResourceLocation
                    {
                        AssetInfo = assetInfo,
                        BundleName = bundleManifest.name,
                        AssetPath = assetInfo.name,
                        LocationType = ResourceLocationType.Bundle
                    };

                    if (!string.IsNullOrEmpty(assetInfo.path))
                        _pathMap[assetInfo.path] = location;
                        
                    if (!string.IsNullOrEmpty(assetInfo.guid))
                        _guidMap[assetInfo.guid] = location;

                    if (!string.IsNullOrEmpty(assetInfo.address))
                        _addressMap[assetInfo.address] = location;

                    if (!string.IsNullOrEmpty(assetInfo.name))
                        _nameMap[assetInfo.name] = location;

                    // 新增：Label 索引
                    if (assetInfo.labels != null)
                    {
                        foreach (var label in assetInfo.labels)
                        {
                            if (!_labelMap.ContainsKey(label))
                                _labelMap[label] = new List<ResourceLocation>();

                            _labelMap[label].Add(location);
                        }
                    }

                    // 新增：Bundle 索引
                    if (!_bundleMap.ContainsKey(bundleManifest.name))
                        _bundleMap[bundleManifest.name] = new List<ResourceLocation>();

                    _bundleMap[bundleManifest.name].Add(location);
                    
                    // 新增：Type 索引
                    if (!string.IsNullOrEmpty(assetInfo.type))
                    {
                        if (!_typeMap.ContainsKey(assetInfo.type))
                            _typeMap[assetInfo.type] = new List<ResourceLocation>();

                        _typeMap[assetInfo.type].Add(location);
                    }
                }
            }

            Game.Debug.Debug($"ResourceLocator: Indexed {_addressMap.Count} assets, {_labelMap.Count} labels, {_bundleMap.Count} bundles");
        }

        /// <summary>
        /// 定位资源
        /// </summary>
        public ResourceLocation Locate(string address)
        {
            // 优先按 address 查找
            if (_addressMap.TryGetValue(address, out var location))
                return location;

            // 按 name 查找
            if (_nameMap.TryGetValue(address, out location))
                return location;

            // 按 guid 查找
            if (_guidMap.TryGetValue(address, out location))
                return location;

            //按路径查找
            if (_pathMap.TryGetValue(address, out location))
                return location;

            // 判断是否是网络资源
            if (address.StartsWith("http://") || address.StartsWith("https://"))
            {
                return new ResourceLocation
                {
                    AssetInfo = new AssetInfo { name = address, address = address, guid = address },
                    LocationType = ResourceLocationType.Remote
                };
            }

            // 判断是否是 Resources 资源
            if (address.StartsWith("Resources/"))
            {
                return new ResourceLocation
                {
                    AssetInfo = new AssetInfo { name = address, address = address, guid = address },
                    LocationType = ResourceLocationType.Builtin
                };
            }

            return null;
        }

        /// <summary>
        /// 是否包含资源
        /// </summary>
        public bool Contains(string address)
        {
            return _addressMap.ContainsKey(address) || _nameMap.ContainsKey(address) || _guidMap.ContainsKey(address) || _pathMap.ContainsKey(address);
        }

        /// <summary>
        /// 通过 Label 查找资源列表
        /// </summary>
        public List<ResourceLocation> LocateByLabel(string label)
        {
            return _labelMap.TryGetValue(label, out var locations)
                ? locations
                : new List<ResourceLocation>();
        }

        /// <summary>
        /// 通过多个 Label 查找资源（交集 - 资源必须同时包含所有指定的 Label）
        /// </summary>
        public List<ResourceLocation> LocateByLabels(params string[] labels)
        {
            if (labels == null || labels.Length == 0)
                return new List<ResourceLocation>();

            if (labels.Length == 1)
                return LocateByLabel(labels[0]);

            // 获取第一个 Label 的资源列表
            var result = LocateByLabel(labels[0]);

            // 过滤出同时包含所有其他 Label 的资源
            for (int i = 1; i < labels.Length; i++)
            {
                var otherLocations = LocateByLabel(labels[i]);
                result = result.Intersect(otherLocations).ToList();

                if (result.Count == 0)
                    break;
            }

            return result;
        }

        /// <summary>
        /// 获取 Bundle 中的所有资源
        /// </summary>
        public List<ResourceLocation> LocateByBundle(string bundleName)
        {
            return _bundleMap.TryGetValue(bundleName, out var locations)
                ? locations
                : new List<ResourceLocation>();
        }

        /// <summary>
        /// 通过类型查找资源列表（支持继承类型匹配）
        /// </summary>
        public List<ResourceLocation> LocateByType(Type type)
        {
            var result = new List<ResourceLocation>();
            var typeName = type.FullName;
            
            // 精确匹配
            if (_typeMap.TryGetValue(typeName, out var locations))
            {
                result.AddRange(locations);
            }
            
            // 遍历所有类型，查找继承关系
            foreach (var kvp in _typeMap)
            {
                if (kvp.Key == typeName) continue;
                
                var assetType = Type.GetType(kvp.Key);
                if (assetType != null && type.IsAssignableFrom(assetType))
                {
                    result.AddRange(kvp.Value);
                }
            }
            
            return result;
        }

        /// <summary>
        /// 清空索引
        /// </summary>
        public void Clear()
        {
            _guidMap.Clear();
            _addressMap.Clear();
            _nameMap.Clear();
            _labelMap.Clear();
            _bundleMap.Clear();
            _pathMap.Clear();
            _typeMap.Clear();
        }
    }
}