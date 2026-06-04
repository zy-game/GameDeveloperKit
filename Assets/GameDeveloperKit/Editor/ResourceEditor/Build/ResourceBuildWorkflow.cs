using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDeveloperKit.ResourceEditor
{
    public sealed class ResourceBuildWorkflow
    {
        private readonly ResourceEditorSettings m_Settings;
        private readonly ResourceEditorRegistry m_Registry;
        private readonly Func<IReadOnlyDictionary<ResourceEditorBundle, List<ResourceGroupPreview>>> m_GetPreviews;
        private readonly ResourceBuildSettings m_BuildSettings;

        public ResourceBuildWorkflow(
            ResourceEditorSettings settings,
            ResourceEditorRegistry registry,
            Func<IReadOnlyDictionary<ResourceEditorBundle, List<ResourceGroupPreview>>> getPreviews,
            ResourceBuildSettings buildSettings = null)
        {
            m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            m_Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            m_GetPreviews = getPreviews ?? throw new ArgumentNullException(nameof(getPreviews));
            m_BuildSettings = buildSettings ?? settings.BuildSettings;
        }

        public ResourceBuildPlan CreatePlan(out string error)
        {
            error = null;
            var packages = GetBuildPackages().ToArray();
            if (packages.Length == 0)
            {
                error = "No package selected for build.";
                return null;
            }

            var plan = new ResourceBuildPlan();
            var context = new ResourceBuildContext(m_Settings, m_Registry, packages, m_GetPreviews(), m_BuildSettings, DateTime.UtcNow);
            foreach (var package in packages)
            {
                var strategy = m_Registry.GetBuildStrategy(package.BuildStrategyId)?.Instance;
                if (strategy == null)
                {
                    error = $"Missing build strategy: {package.BuildStrategyId}";
                    return null;
                }

                var packagePlan = strategy.CreatePlan(new ResourceBuildContext(m_Settings, m_Registry, new[] { package }, context.Previews, m_BuildSettings, context.BuildTime));
                foreach (var bundle in packagePlan.Bundles)
                {
                    plan.AddBundle(bundle);
                }
            }

            return plan;
        }

        public ResourceBuildResult Build(out ResourceBuildPlan plan)
        {
            plan = CreatePlan(out var error);
            if (plan == null)
            {
                return ResourceBuildResult.Failure(error);
            }

            var packages = plan.Bundles.Select(x => x.Package).Distinct().ToArray();
            var context = new ResourceBuildContext(m_Settings, m_Registry, packages, m_GetPreviews(), m_BuildSettings, DateTime.UtcNow);
            return ResourceBuildExecutor.Build(context, plan);
        }

        private IEnumerable<ResourceEditorPackage> GetBuildPackages()
        {
            switch (m_BuildSettings.Scope)
            {
                case ResourceBuildScope.AllPackages:
                    return m_Settings.Packages.Where(x => x != null);
                case ResourceBuildScope.HotUpdatePackages:
                    return m_Settings.Packages.Where(x => x != null && x.IsHotUpdate);
                default:
                    var index = m_Settings.SelectedPackageIndex;
                    return index >= 0 && index < m_Settings.Packages.Count && m_Settings.Packages[index] != null
                        ? new[] { m_Settings.Packages[index] }
                        : Array.Empty<ResourceEditorPackage>();
            }
        }
    }
}
