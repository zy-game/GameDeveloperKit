using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源位置类，用于定位和匹配资源条目。
    /// </summary>
    public sealed class ResourceLocation
    {
        /// <summary>
        /// 获取或设置资源包名称。
        /// </summary>
        public string PackageName { get; set; }

        /// <summary>
        /// 获取或设置资源名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置资源类型。
        /// </summary>
        public Type AssetType { get; set; }

        /// <summary>
        /// 获取或设置资源标签列表。
        /// </summary>
        public IReadOnlyList<string> Labels { get; set; }

        /// <summary>
        /// 获取或设置资源完整路径。
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// 检查资源位置是否匹配指定的资源条目。
        /// </summary>
        /// <param name="entry">资源条目。</param>
        /// <param name="expectedKind">期望的资源条目类型。</param>
        /// <returns>如果匹配返回true，否则返回false。</returns>
        public bool Matches(ResourceEntry entry, ResourceEntryKind? expectedKind = null)
        {
            if (entry == null)
            {
                return false;
            }

            if (expectedKind.HasValue && entry.Kind != expectedKind.Value)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(Name) &&
                !string.Equals(entry.Name, Name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(FullPath) &&
                !string.Equals(NormalizePath(entry.FullPath), NormalizePath(FullPath), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (AssetType != null)
            {
                if (entry.AssetType == null)
                {
                    return false;
                }

                if (entry.AssetType != AssetType && !AssetType.IsAssignableFrom(entry.AssetType))
                {
                    return false;
                }
            }

            if (Labels != null && Labels.Count > 0)
            {
                if (entry.Labels == null || entry.Labels.Count == 0)
                {
                    return false;
                }

                for (var i = 0; i < Labels.Count; i++)
                {
                    if (!entry.Labels.Any(label => string.Equals(label, Labels[i], StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 克隆当前资源位置实例。
        /// </summary>
        /// <returns>新的资源位置实例。</returns>
        public ResourceLocation Clone()
        {
            return new ResourceLocation
            {
                PackageName = PackageName,
                Name = Name,
                AssetType = AssetType,
                Labels = Labels == null ? null : new List<string>(Labels),
                FullPath = FullPath
            };
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var normalized = path.Replace('\\', '/').Trim().TrimStart('/');
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring("Assets/".Length);
            }

            return normalized;
        }
    }
}
