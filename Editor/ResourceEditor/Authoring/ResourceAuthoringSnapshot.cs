using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GameDeveloperKit.Resource;

namespace GameDeveloperKit.ResourceEditor
{
    internal sealed class ResourceAuthoringSnapshot
    {
        public ResourceAuthoringSnapshot(
            string revision,
            ManifestInfo manifest,
            IReadOnlyList<ResourceValidationIssue> issues,
            IReadOnlyDictionary<ResourceEditorBundle, List<ResourceGroupPreview>> previews)
        {
            Revision = string.IsNullOrWhiteSpace(revision)
                ? throw new ArgumentException("Revision cannot be empty.", nameof(revision))
                : revision;
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            Issues = new List<ResourceValidationIssue>(issues ?? throw new ArgumentNullException(nameof(issues))).AsReadOnly();
            if (previews == null)
            {
                throw new ArgumentNullException(nameof(previews));
            }

            var previewSnapshot = new Dictionary<ResourceEditorBundle, IReadOnlyList<ResourceGroupPreview>>();
            foreach (var pair in previews)
            {
                previewSnapshot.Add(
                    pair.Key,
                    new List<ResourceGroupPreview>(pair.Value ?? new List<ResourceGroupPreview>()).AsReadOnly());
            }

            Previews = new ReadOnlyDictionary<ResourceEditorBundle, IReadOnlyList<ResourceGroupPreview>>(previewSnapshot);
        }

        public string Revision { get; }

        public ManifestInfo Manifest { get; }

        public IReadOnlyList<ResourceValidationIssue> Issues { get; }

        public IReadOnlyDictionary<ResourceEditorBundle, IReadOnlyList<ResourceGroupPreview>> Previews { get; }
    }
}
