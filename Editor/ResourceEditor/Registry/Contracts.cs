using System;
using System.Collections.Generic;

namespace GameDeveloperKit.ResourceEditor.Registry
{
    /// <summary>
    /// 定义 Colletion Attribute 类型。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class CollectorAttribute : Attribute
    {
        /// <summary>
        /// 初始化 Colletion Attribute。
        /// </summary>
        /// <param name="id">id 参数。</param>
        /// <param name="displayName">display Name 参数。</param>
        /// <param name="order">order 参数。</param>
        public CollectorAttribute(string id, string displayName = null, int order = 0)
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

    /// <summary>
    /// 定义 Builded Attribute 类型。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class BuildStrategyAttribute : Attribute
    {
        /// <summary>
        /// 初始化 Builded Attribute。
        /// </summary>
        /// <param name="id">id 参数。</param>
        /// <param name="displayName">display Name 参数。</param>
        /// <param name="order">order 参数。</param>
        public BuildStrategyAttribute(string id, string displayName = null, int order = 0)
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

    /// <summary>
    /// 定义 Resource Collector 类型。
    /// </summary>
    public abstract class Collector
    {
        /// <summary>
        /// 执行 Collect。
        /// </summary>
        /// <param name="package">package 参数。</param>
        /// <param name="bundle">bundle 参数。</param>
        /// <returns>执行结果。</returns>
        public abstract IReadOnlyList<ResourceGroupPreview> Collect(GameDeveloperKit.ResourceEditor.Authoring.Package package, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle);
    }

    /// <summary>
    /// 定义 Resource Build Strategy 类型。
    /// </summary>
    public abstract class BuildStrategy
    {
        /// <summary>
        /// 创建 Plan。
        /// </summary>
        /// <param name="context">context 参数。</param>
        /// <returns>执行结果。</returns>
        public abstract GameDeveloperKit.ResourceEditor.Build.Plan CreatePlan(GameDeveloperKit.ResourceEditor.Build.Context context);
    }

    /// <summary>
    /// 定义 Resource Checker 类型。
    /// </summary>
}

namespace GameDeveloperKit.ResourceEditor.Validation
{
    public abstract class Checker
    {
        /// <summary>
        /// 执行 Check。
        /// </summary>
        /// <param name="context">context 参数。</param>
        /// <param name="issues">issues 参数。</param>
        public abstract void Check(GameDeveloperKit.ResourceEditor.Validation.CheckContext context, List<GameDeveloperKit.ResourceEditor.Validation.Issue> issues);
    }

    /// <summary>
    /// 定义 Resource Check Context 类型。
    /// </summary>
    public sealed class CheckContext
    {
        /// <summary>
        /// 初始化 Resource Check Context。
        /// </summary>
        /// <param name="settings">settings 参数。</param>
        /// <param name="package">package 参数。</param>
        /// <param name="bundle">bundle 参数。</param>
        /// <param name="resources">resources 参数。</param>
        /// <param name="previews">previews 参数。</param>
        public CheckContext(
            GameDeveloperKit.ResourceEditor.Authoring.Settings settings,
            GameDeveloperKit.ResourceEditor.Authoring.Package package,
            GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle,
            IReadOnlyList<ResourceGroupPreview> resources,
            IReadOnlyDictionary<GameDeveloperKit.ResourceEditor.Authoring.Bundle, List<ResourceGroupPreview>> previews)
        {
            Settings = settings;
            Package = package;
            Bundle = bundle;
            Resources = resources ?? Array.Empty<ResourceGroupPreview>();
            Previews = previews;
        }

        public GameDeveloperKit.ResourceEditor.Authoring.Settings Settings { get; }

        public GameDeveloperKit.ResourceEditor.Authoring.Package Package { get; }

        public GameDeveloperKit.ResourceEditor.Authoring.Bundle Bundle { get; }

        public IReadOnlyList<ResourceGroupPreview> Resources { get; }

        public IReadOnlyDictionary<GameDeveloperKit.ResourceEditor.Authoring.Bundle, List<ResourceGroupPreview>> Previews { get; }
    }

    /// <summary>
    /// 定义 Resource Group Preview 类型。
    /// </summary>
}

namespace GameDeveloperKit.ResourceEditor
{
    public sealed class ResourceGroupPreview
    {
        /// <summary>
        /// 初始化 Resource Group Preview。
        /// </summary>
        /// <param name="assetPath">asset Path 参数。</param>
        /// <param name="location">location 参数。</param>
        /// <param name="typeName">type Name 参数。</param>
        /// <param name="labels">labels 参数。</param>
        /// <param name="bundleName">bundle Name 参数。</param>
        /// <param name="group">group 参数。</param>
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

    /// <summary>
    /// 定义 Resource Validation Severity 枚举。
    /// </summary>
}

namespace GameDeveloperKit.ResourceEditor.Validation
{
    public enum Severity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// 定义 Resource Validation Issue 类型。
    /// </summary>
    public sealed class Issue
    {
        /// <summary>
        /// 初始化 Resource Validation Issue。
        /// </summary>
        /// <param name="severity">severity 参数。</param>
        /// <param name="source">source 参数。</param>
        /// <param name="message">message 参数。</param>
        /// <param name="package">package 参数。</param>
        /// <param name="bundle">bundle 参数。</param>
        /// <param name="resource">resource 参数。</param>
        public Issue(Severity severity, string source, string message, GameDeveloperKit.ResourceEditor.Authoring.Package package = null, GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle = null, ResourceGroupPreview resource = null)
        {
            Severity = severity;
            Source = source;
            Message = message;
            Package = package;
            Bundle = bundle;
            Resource = resource;
        }

        public Severity Severity { get; }

        public string Source { get; }

        public string Message { get; }

        public GameDeveloperKit.ResourceEditor.Authoring.Package Package { get; }

        public GameDeveloperKit.ResourceEditor.Authoring.Bundle Bundle { get; }

        public ResourceGroupPreview Resource { get; }
    }
}
