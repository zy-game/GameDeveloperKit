using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDeveloperKit.Runtime
{
    public sealed class ResourceLocation
    {
        public string Name { get; set; }

        public Type AssetType { get; set; }

        public IReadOnlyList<string> Labels { get; set; }

        public string FullPath { get; set; }

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

        public ResourceLocation Clone()
        {
            return new ResourceLocation
            {
                Name = Name,
                AssetType = AssetType,
                Labels = Labels == null ? null : new List<string>(Labels),
                FullPath = FullPath
            };
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/').Trim();
        }
    }
}
