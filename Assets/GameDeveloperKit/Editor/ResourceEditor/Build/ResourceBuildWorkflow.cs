using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDeveloperKit.ResourceEditor
{
    /// <summary>
    /// 定义 Resource Build Workflow 类型。
    /// </summary>
    public sealed class ResourceBuildWorkflow
    {
        /// <summary>
        /// 存储 Settings。
        /// </summary>
        private readonly ResourceEditorSettings m_Settings;
        /// <summary>
        /// 存储 Registry。
        /// </summary>
        private readonly ResourceEditorRegistry m_Registry;
        /// <summary>
        /// 存储 Get Previews。
        /// </summary>
        private readonly Func<IReadOnlyDictionary<ResourceEditorBundle, List<ResourceGroupPreview>>> m_GetPreviews;
        /// <summary>
        /// 存储 Build Settings。
        /// </summary>
        private readonly ResourceBuildSettings m_BuildSettings;

        /// <summary>
        /// 初始化 Resource Build Workflow。
        /// </summary>
        /// <param name="settings">settings 参数。</param>
        /// <param name="registry">registry 参数。</param>
        /// <param name="getPreviews">get Previews 参数。</param>
        /// <param name="buildSettings">build Settings 参数。</param>
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

        /// <summary>
        /// 创建 Plan。
        /// </summary>
        /// <param name="error">error 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 构建 member。
        /// </summary>
        /// <param name="plan">plan 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 获取 Build Packages。
        /// </summary>
        /// <returns>执行结果。</returns>
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
