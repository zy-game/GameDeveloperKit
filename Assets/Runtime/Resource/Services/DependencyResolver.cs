using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 依赖解析器
    /// 管理 Bundle 的依赖关系
    /// </summary>
    public class DependencyResolver
    {
        private readonly Dictionary<string, string[]> _dependencyMap = new Dictionary<string, string[]>();
        
        /// <summary>
        /// 从 Manifest 构建依赖图
        /// </summary>
        public void BuildDependencyGraph(PackageManifest manifest)
        {
            _dependencyMap.Clear();
            
            if (manifest == null || manifest.bundles == null)
            {
                return;
            }
            
            foreach (var bundleManifest in manifest.bundles)
            {
                if (bundleManifest.dependencies != null && bundleManifest.dependencies.Length > 0)
                {
                    _dependencyMap[bundleManifest.name] = bundleManifest.dependencies;
                }
                else
                {
                    _dependencyMap[bundleManifest.name] = System.Array.Empty<string>();
                }
            }
            
            Game.Debug.Debug($"DependencyResolver: Built dependency graph for {_dependencyMap.Count} bundles");
        }
        
        /// <summary>
        /// 获取直接依赖
        /// </summary>
        public string[] GetDependencies(string bundleName)
        {
            if (_dependencyMap.TryGetValue(bundleName, out var dependencies))
            {
                return dependencies;
            }
            
            return System.Array.Empty<string>();
        }
        
        /// <summary>
        /// 获取所有依赖（递归）
        /// </summary>
        public List<string> GetAllDependencies(string bundleName)
        {
            var result = new List<string>();
            var visited = new HashSet<string>();
            
            CollectDependenciesRecursive(bundleName, result, visited);
            
            return result;
        }
        
        /// <summary>
        /// 递归收集依赖
        /// </summary>
        private void CollectDependenciesRecursive(string bundleName, List<string> result, HashSet<string> visited)
        {
            if (visited.Contains(bundleName))
            {
                return;
            }
            
            visited.Add(bundleName);
            
            var dependencies = GetDependencies(bundleName);
            foreach (var dep in dependencies)
            {
                if (!visited.Contains(dep))
                {
                    result.Add(dep);
                    CollectDependenciesRecursive(dep, result, visited);
                }
            }
        }
        
        /// <summary>
        /// 检查循环依赖
        /// </summary>
        public bool HasCircularDependency(string bundleName)
        {
            var visited = new HashSet<string>();
            var stack = new HashSet<string>();
            
            return HasCircularDependencyRecursive(bundleName, visited, stack);
        }
        
        private bool HasCircularDependencyRecursive(string bundleName, HashSet<string> visited, HashSet<string> stack)
        {
            if (stack.Contains(bundleName))
            {
                Game.Debug.Error($"Circular dependency detected for bundle: {bundleName}");
                return true;
            }
            
            if (visited.Contains(bundleName))
            {
                return false;
            }
            
            visited.Add(bundleName);
            stack.Add(bundleName);
            
            var dependencies = GetDependencies(bundleName);
            foreach (var dep in dependencies)
            {
                if (HasCircularDependencyRecursive(dep, visited, stack))
                {
                    return true;
                }
            }
            
            stack.Remove(bundleName);
            return false;
        }
        
        /// <summary>
        /// 清空依赖图
        /// </summary>
        public void Clear()
        {
            _dependencyMap.Clear();
        }
    }
}
