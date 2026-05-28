using System;
using System.Collections.Generic;

namespace GameDeveloperKit.ResourceEditor
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ColletionAttribute : Attribute
    {
        public ColletionAttribute(string id, string displayName = null, int order = 0)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Collector id cannot be empty.", nameof(id));
            }

            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            Order = order;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public string Description { get; set; }

        public int Order { get; }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class BuildedAttribute : Attribute
    {
        public BuildedAttribute(string id, string displayName = null, int order = 0)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Build strategy id cannot be empty.", nameof(id));
            }

            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            Order = order;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public string Description { get; set; }

        public int Order { get; }
    }

    public abstract class ResourceCollector
    {
        public abstract IReadOnlyList<ResourceGroupPreview> Collect(ResourceEditorPackage package, ResourceEditorBundle bundle);
    }

    public abstract class ResourceBuildStrategy
    {
    }

    public abstract class ResourceChecker
    {
        public abstract void Check(ResourceCheckContext context, List<ResourceValidationIssue> issues);
    }

    public sealed class ResourceCheckContext
    {
        public ResourceCheckContext(
            ResourceEditorSettings settings,
            ResourceEditorPackage package,
            ResourceEditorBundle bundle,
            IReadOnlyList<ResourceGroupPreview> resources,
            IReadOnlyDictionary<ResourceEditorBundle, List<ResourceGroupPreview>> previews)
        {
            Settings = settings;
            Package = package;
            Bundle = bundle;
            Resources = resources ?? Array.Empty<ResourceGroupPreview>();
            Previews = previews;
        }

        public ResourceEditorSettings Settings { get; }

        public ResourceEditorPackage Package { get; }

        public ResourceEditorBundle Bundle { get; }

        public IReadOnlyList<ResourceGroupPreview> Resources { get; }

        public IReadOnlyDictionary<ResourceEditorBundle, List<ResourceGroupPreview>> Previews { get; }
    }

    public sealed class ResourceGroupPreview
    {
        public ResourceGroupPreview(string assetPath, string location, string typeName, IReadOnlyList<string> labels, string bundleName, string group)
        {
            AssetPath = assetPath;
            Location = location;
            TypeName = typeName;
            Labels = labels ?? Array.Empty<string>();
            BundleName = bundleName;
            Group = group;
        }

        public string AssetPath { get; }

        public string Location { get; }

        public string TypeName { get; }

        public IReadOnlyList<string> Labels { get; }

        public string BundleName { get; }

        public string Group { get; }
    }

    public enum ResourceValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class ResourceValidationIssue
    {
        public ResourceValidationIssue(ResourceValidationSeverity severity, string source, string message, ResourceEditorPackage package = null, ResourceEditorBundle bundle = null, ResourceGroupPreview resource = null)
        {
            Severity = severity;
            Source = source;
            Message = message;
            Package = package;
            Bundle = bundle;
            Resource = resource;
        }

        public ResourceValidationSeverity Severity { get; }

        public string Source { get; }

        public string Message { get; }

        public ResourceEditorPackage Package { get; }

        public ResourceEditorBundle Bundle { get; }

        public ResourceGroupPreview Resource { get; }
    }
}
