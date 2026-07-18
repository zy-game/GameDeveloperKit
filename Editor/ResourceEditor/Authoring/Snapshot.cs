using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GameDeveloperKit.Resource;

namespace GameDeveloperKit.ResourceEditor.Authoring
{
    internal sealed class Snapshot
    {
        public Snapshot(
            string revision,
            ManifestInfo manifest,
            IReadOnlyList<GameDeveloperKit.ResourceEditor.Validation.Issue> issues,
            IReadOnlyDictionary<Bundle, List<ResourceGroupPreview>> previews)
        {
            Revision = string.IsNullOrWhiteSpace(revision)
                ? throw new ArgumentException("Revision cannot be empty.", nameof(revision))
                : revision;
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            Issues = new List<GameDeveloperKit.ResourceEditor.Validation.Issue>(issues ?? throw new ArgumentNullException(nameof(issues))).AsReadOnly();
            if (previews == null)
            {
                throw new ArgumentNullException(nameof(previews));
            }

            var previewSnapshot = new Dictionary<Bundle, IReadOnlyList<ResourceGroupPreview>>();
            foreach (var pair in previews)
            {
                previewSnapshot.Add(
                    pair.Key,
                    new List<ResourceGroupPreview>(pair.Value ?? new List<ResourceGroupPreview>()).AsReadOnly());
            }

            Previews = new ReadOnlyDictionary<Bundle, IReadOnlyList<ResourceGroupPreview>>(previewSnapshot);
        }

        public string Revision { get; }

        public ManifestInfo Manifest { get; }

        public IReadOnlyList<GameDeveloperKit.ResourceEditor.Validation.Issue> Issues { get; }

        public IReadOnlyDictionary<Bundle, IReadOnlyList<ResourceGroupPreview>> Previews { get; }
    }
}
