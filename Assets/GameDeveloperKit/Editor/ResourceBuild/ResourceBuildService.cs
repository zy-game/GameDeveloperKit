using System;
using System.Collections.Generic;
using GameDeveloperKit.Runtime;

namespace GameDeveloperKit.Editor
{
    internal sealed class ResourceBuildService
    {
        private readonly ISBP_Build _sbpBuild;

        public ResourceBuildService()
            : this(new ResourceBuildPipeline())
        {
        }

        public ResourceBuildService(ISBP_Build sbpBuild)
        {
            _sbpBuild = sbpBuild ?? throw new ArgumentNullException(nameof(sbpBuild));
        }

        public ResourceBuildResult BuildPackage(ResourceProjectSettingsData settings, ResourcePackageDefinition package)
        {
            if (settings == null)
            {
                return Fail("Resource settings are unavailable.");
            }

            var request = new ResourceBuildPipelineRequest
            {
                Settings = settings,
                Package = package,
                ForceRebuild = false
            };

            var report = _sbpBuild.Build(request);
            if (!report.Success)
            {
                return Fail(report.ErrorMessage);
            }

            var configuration = GameFrameworkConfigurationBridge.ResolveSelectedOrFirstConfiguration();
            if (configuration != null)
            {
                GameFrameworkConfigurationBridge.ApplyResourceSettings(configuration, settings);
                GameFrameworkConfigurationBridge.SaveConfiguration(configuration);
            }
            GameFrameworkConfigurationBridge.SaveResourceSettingsData(settings);
            return new ResourceBuildResult
            {
                Success = true,
                Message = $"Package '{package.PackageName}' build succeeded.",
                PackageCount = 1,
                BundleCount = report.BundleCount,
                EntryCount = report.EntryCount,
                OutputRoot = report.OutputRoot
            };
        }

        public ResourceBuildResult BuildAll(ResourceProjectSettingsData settings)
        {
            if (settings?.Packages == null || settings.Packages.Count == 0)
            {
                return Fail("No packages to build.");
            }

            var succeeded = 0;
            var totalBundles = 0;
            var totalEntries = 0;
            var outputs = new List<string>();
            var failures = new List<string>();

            for (var i = 0; i < settings.Packages.Count; i++)
            {
                var package = settings.Packages[i];
                var result = BuildPackage(settings, package);
                if (!result.Success)
                {
                    failures.Add(result.Message);
                    continue;
                }

                succeeded++;
                totalBundles += result.BundleCount;
                totalEntries += result.EntryCount;
                if (!string.IsNullOrWhiteSpace(result.OutputRoot))
                {
                    outputs.Add(result.OutputRoot);
                }
            }

            if (failures.Count > 0)
            {
                return new ResourceBuildResult
                {
                    Success = false,
                    Message = $"Build all completed with failures. Success={succeeded}/{settings.Packages.Count}. {string.Join(" | ", failures)}",
                    PackageCount = succeeded,
                    BundleCount = totalBundles,
                    EntryCount = totalEntries,
                    OutputRoot = string.Join(";", outputs)
                };
            }

            return new ResourceBuildResult
            {
                Success = true,
                Message = $"Build all succeeded. Packages={succeeded}, Bundles={totalBundles}, Entries={totalEntries}.",
                PackageCount = succeeded,
                BundleCount = totalBundles,
                EntryCount = totalEntries,
                OutputRoot = string.Join(";", outputs)
            };
        }

        private static ResourceBuildResult Fail(string message)
        {
            return new ResourceBuildResult
            {
                Success = false,
                Message = message ?? "Build failed."
            };
        }
    }
}
