using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 组合收集器 - 组合多个收集器（并集）
    /// </summary>
    [Serializable]
    public class CompositeCollector : IAssetCollector
    {
        [SerializeReference]
        public List<IAssetCollector> collectors = new List<IAssetCollector>();
        
        public string Name => "组合收集器";
        
        public IEnumerable<CollectedAsset> Collect(CollectorContext context)
        {
            if (collectors == null || collectors.Count == 0)
                yield break;
            
            var seen = new HashSet<string>();
            
            foreach (var collector in collectors)
            {
                if (collector == null)
                    continue;
                
                foreach (var asset in collector.Collect(context))
                {
                    if (seen.Add(asset.guid))
                    {
                        yield return asset;
                    }
                }
            }
        }
        
        public bool Validate(out string error)
        {
            if (collectors == null || collectors.Count == 0)
            {
                error = "组合收集器至少需要一个子收集器";
                return false;
            }
            
            for (int i = 0; i < collectors.Count; i++)
            {
                var collector = collectors[i];
                if (collector == null)
                {
                    error = $"子收集器 [{i}] 为空";
                    return false;
                }
                
                if (!collector.Validate(out var subError))
                {
                    error = $"子收集器 [{i}] ({collector.Name}): {subError}";
                    return false;
                }
            }
            
            error = null;
            return true;
        }
        
        /// <summary>
        /// 添加子收集器
        /// </summary>
        public void Add(IAssetCollector collector)
        {
            collectors ??= new List<IAssetCollector>();
            collectors.Add(collector);
        }
        
        /// <summary>
        /// 移除子收集器
        /// </summary>
        public void Remove(IAssetCollector collector)
        {
            collectors?.Remove(collector);
        }
        
        /// <summary>
        /// 移除指定索引的子收集器
        /// </summary>
        public void RemoveAt(int index)
        {
            if (collectors != null && index >= 0 && index < collectors.Count)
            {
                collectors.RemoveAt(index);
            }
        }
    }
    
    /// <summary>
    /// 过滤收集器 - 在收集结果上应用过滤器
    /// </summary>
    [Serializable]
    public class FilteredCollector : IAssetCollector
    {
        [SerializeReference]
        public IAssetCollector source;
        
        [SerializeReference]
        public List<IAssetFilter> filters = new List<IAssetFilter>();
        
        /// <summary>
        /// 过滤模式：All = 所有过滤器都匹配，Any = 任一过滤器匹配
        /// </summary>
        public FilterMode filterMode = FilterMode.All;
        
        public string Name => "过滤收集器";
        
        public IEnumerable<CollectedAsset> Collect(CollectorContext context)
        {
            if (source == null)
                yield break;
            
            foreach (var asset in source.Collect(context))
            {
                if (PassFilters(asset))
                {
                    yield return asset;
                }
            }
        }
        
        public bool Validate(out string error)
        {
            if (source == null)
            {
                error = "源收集器不能为空";
                return false;
            }
            
            if (!source.Validate(out var sourceError))
            {
                error = $"源收集器错误: {sourceError}";
                return false;
            }
            
            if (filters != null)
            {
                for (int i = 0; i < filters.Count; i++)
                {
                    var filter = filters[i];
                    if (filter == null)
                        continue;
                    
                    if (!filter.Validate(out var filterError))
                    {
                        error = $"过滤器 [{i}] ({filter.Name}): {filterError}";
                        return false;
                    }
                }
            }
            
            error = null;
            return true;
        }
        
        private bool PassFilters(CollectedAsset asset)
        {
            if (filters == null || filters.Count == 0)
                return true;
            
            if (filterMode == FilterMode.All)
            {
                return filters.All(f => f == null || f.Match(asset));
            }
            else
            {
                return filters.Any(f => f != null && f.Match(asset));
            }
        }
        
        /// <summary>
        /// 添加过滤器
        /// </summary>
        public void AddFilter(IAssetFilter filter)
        {
            filters ??= new List<IAssetFilter>();
            filters.Add(filter);
        }
    }
    
    /// <summary>
    /// 过滤模式
    /// </summary>
    public enum FilterMode
    {
        /// <summary>
        /// 所有过滤器都必须匹配
        /// </summary>
        All,
        
        /// <summary>
        /// 任一过滤器匹配即可
        /// </summary>
        Any
    }
}
