using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDeveloperKit.ResourceEditor.Authoring
{
    internal static class EntryPreviewBuilder
    {
        public static List<ResourceGroupPreview> Build(Bundle bundle)
        {
            if (bundle?.Entries == null || bundle.Entries.Count == 0)
            {
                return new List<ResourceGroupPreview>();
            }

            return bundle.Entries
                .Where(entry => entry != null)
                .Where(entry => entry.Excluded is false)
                .Where(entry => string.IsNullOrWhiteSpace(entry.AssetPath) is false)
                .Select(entry => new ResourceGroupPreview(
                    entry.AssetPath,
                    GameDeveloperKit.ResourceEditor.Registry.ExplicitAssetCollector.ResolveLocation(bundle.ProviderId, entry.AssetPath),
                    entry.TypeName,
                    entry.Labels ?? (IReadOnlyList<string>)Array.Empty<string>(),
                    bundle.Name,
                    bundle.Group))
                .ToList();
        }

        public static bool HasEntries(Bundle bundle)
        {
            return bundle?.Entries != null && bundle.Entries.Any(entry => entry != null);
        }
    }
}
