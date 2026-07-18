using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace GameDeveloperKit.ResourceEditor.Build
{
    /// <summary>
    /// 定义 Resource Build Workflow 类型。
    /// </summary>
    public sealed class Workflow
    {
        /// <summary>
        /// 存储 Settings。
        /// </summary>
        private readonly GameDeveloperKit.ResourceEditor.Authoring.Settings m_Settings;
        /// <summary>
        /// 存储 Registry。
        /// </summary>
        private readonly GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry m_Registry;
        /// <summary>
        /// 存储 Build Settings。
        /// </summary>
        private readonly Settings m_BuildSettings;

        /// <summary>
        /// 初始化 Resource Build Workflow。
        /// </summary>
        /// <param name="settings">settings 参数。</param>
        /// <param name="registry">registry 参数。</param>
        /// <param name="buildSettings">build Settings 参数。</param>
        public Workflow(
            GameDeveloperKit.ResourceEditor.Authoring.Settings settings,
            GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry registry,
            Settings buildSettings = null)
        {
            m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            m_Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            m_BuildSettings = buildSettings ?? settings.BuildSettings ??
                throw new ArgumentException("Resource build settings are missing.", nameof(settings));
        }

        /// <summary>
        /// 创建 Plan。
        /// </summary>
        /// <param name="error">error 参数。</param>
        /// <returns>执行结果。</returns>
        public Plan CreatePlan(out string error)
        {
            return TryCreatePlan(out var plan, out _, out _, out _, out _, out error)
                ? plan
                : null;
        }

        /// <summary>
        /// 构建 member。
        /// </summary>
        /// <param name="plan">plan 参数。</param>
        /// <returns>执行结果。</returns>
        public Result Build(out Plan plan)
        {
            if (TryCreatePlan(
                    out plan,
                    out var snapshot,
                    out var buildSettings,
                    out var target,
                    out var buildTime,
                    out var error) is false)
            {
                return Result.Failure(error);
            }

            var packages = GetManifestPackages(plan).ToArray();
            var context = new Context(
                m_Settings,
                m_Registry,
                packages,
                snapshot.Previews,
                buildSettings,
                buildTime,
                target);
            return Executor.Build(context, plan);
        }

        private bool TryCreatePlan(
            out Plan plan,
            out GameDeveloperKit.ResourceEditor.Authoring.Snapshot snapshot,
            out Settings buildSettings,
            out BuildTarget target,
            out DateTime buildTime,
            out string error)
        {
            plan = null;
            snapshot = null;
            target = default;
            buildTime = DateTime.UtcNow;
            buildSettings = m_BuildSettings.Copy();
            buildSettings.EnsureDefaults();
            if (ValidateBuildSettings(buildSettings, out target, out error) is false)
            {
                return false;
            }

            snapshot = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(m_Settings, m_Registry);
            var blockingIssues = snapshot.Issues
                .Where(issue => issue.Severity == GameDeveloperKit.ResourceEditor.Validation.Severity.Error)
                .ToArray();
            if (blockingIssues.Length > 0)
            {
                error = $"Resource authoring preflight failed: {GameDeveloperKit.ResourceEditor.Authoring.Service.FormatIssues(blockingIssues)}";
                return false;
            }

            var packages = GetBuildPackages(buildSettings.Scope).ToArray();
            if (packages.Length == 0)
            {
                error = "No package selected for build.";
                return false;
            }

            plan = new Plan();
            foreach (var package in packages)
            {
                var strategy = m_Registry.GetBuildStrategy(package.BuildStrategyId)?.Instance;
                if (strategy == null)
                {
                    error = $"Missing build strategy: {package.BuildStrategyId}";
                    plan = null;
                    return false;
                }

                var packageContext = new Context(
                    m_Settings,
                    m_Registry,
                    new[] { package },
                    snapshot.Previews,
                    buildSettings,
                    buildTime,
                    target);
                var packagePlan = strategy.CreatePlan(packageContext);
                foreach (var bundle in packagePlan.Bundles)
                {
                    plan.AddBundle(bundle);
                }
            }

            error = null;
            return true;
        }

        private static bool ValidateBuildSettings(
            Settings settings,
            out BuildTarget target,
            out string error)
        {
            target = default;
            error = null;
            settings.OutputRoot = settings.OutputRoot?.Trim();
            settings.Target = settings.Target?.Trim();
            settings.ManifestFileName = settings.ManifestFileName?.Trim();
            settings.ManifestVersion = settings.ManifestVersion?.Trim();

            if (string.IsNullOrWhiteSpace(settings.OutputRoot))
            {
                error = "Build output root cannot be empty.";
                return false;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(Utilities.ProjectRelativeOrAbsolutePath(settings.OutputRoot)))
                {
                    error = "Build output root cannot be empty.";
                    return false;
                }
            }
            catch (Exception exception)
            {
                error = $"Build output root is invalid: {exception.Message}";
                return false;
            }

            if (Enum.TryParse(settings.Target, false, out target) is false ||
                Enum.IsDefined(typeof(BuildTarget), target) is false)
            {
                error = $"Build target is invalid: {settings.Target}";
                return false;
            }

            var targetGroup = BuildPipeline.GetBuildTargetGroup(target);
            if (targetGroup == BuildTargetGroup.Unknown ||
                BuildPipeline.IsBuildTargetSupported(targetGroup, target) is false)
            {
                error = $"Build target is not supported by the current Unity installation: {target}";
                return false;
            }

            if (settings.Channels.Count == 0)
            {
                error = "Build channel cannot be empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.ManifestVersion))
            {
                error = "Build version cannot be empty.";
                return false;
            }

            if (IsSafeFileName(settings.ManifestFileName) is false)
            {
                error = $"Manifest file name is invalid: {settings.ManifestFileName}";
                return false;
            }

            if (Enum.IsDefined(typeof(Compression), settings.Compression) is false)
            {
                error = $"Build compression is invalid: {settings.Compression}";
                return false;
            }

            if (Enum.IsDefined(typeof(Scope), settings.Scope) is false)
            {
                error = $"Build scope is invalid: {settings.Scope}";
                return false;
            }

            return true;
        }

        private static bool IsSafeFileName(string value)
        {
            return string.IsNullOrWhiteSpace(value) is false &&
                   value != "." &&
                   value != ".." &&
                   Path.IsPathRooted(value) is false &&
                   value.IndexOf('/') < 0 &&
                   value.IndexOf('\\') < 0 &&
                   value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
                   string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal);
        }

        /// <summary>
        /// 获取 Build Packages。
        /// </summary>
        /// <returns>执行结果。</returns>
        private IEnumerable<GameDeveloperKit.ResourceEditor.Authoring.Package> GetBuildPackages(Scope scope)
        {
            switch (scope)
            {
                case Scope.AllPackages:
                    return m_Settings.Packages.Where(x => x != null);
                case Scope.HotUpdatePackages:
                    return m_Settings.Packages.Where(x => x != null && x.IsHotUpdate);
                default:
                    return GetSelectedBuildPackages();
            }
        }

        private IEnumerable<GameDeveloperKit.ResourceEditor.Authoring.Package> GetSelectedBuildPackages()
        {
            var packages = new HashSet<GameDeveloperKit.ResourceEditor.Authoring.Package>();
            foreach (var package in ManifestPartitioner.GetLocalBasePackages(m_Settings))
            {
                packages.Add(package);
            }

            var index = m_Settings.SelectedPackageIndex;
            if (index >= 0 && index < m_Settings.Packages.Count && m_Settings.Packages[index] != null)
            {
                packages.Add(m_Settings.Packages[index]);
            }

            return packages;
        }

        private IEnumerable<GameDeveloperKit.ResourceEditor.Authoring.Package> GetManifestPackages(Plan plan)
        {
            var packages = new HashSet<GameDeveloperKit.ResourceEditor.Authoring.Package>();
            foreach (var package in ManifestPartitioner.GetLocalBasePackages(m_Settings))
            {
                packages.Add(package);
            }

            foreach (var package in plan.Bundles.Select(x => x.Package).Where(x => x != null))
            {
                packages.Add(package);
            }

            return packages;
        }
    }
}
