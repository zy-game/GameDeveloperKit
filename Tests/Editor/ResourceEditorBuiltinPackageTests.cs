using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Resource;
using GameDeveloperKit.ResourceEditor;
using NUnit.Framework;
using UnityEditor;

namespace GameDeveloperKit.Tests
{
    public sealed class ResourceEditorBuiltinPackageTests
    {
        [Test]
        public void EnsureDefaults_WhenSettingsEmpty_CreatesSingleBuiltinPackage()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<ResourceEditorSettings>();

            settings.EnsureDefaults();
            var initialEntryCount = settings.Packages
                .First(package => package.Name == ResourceConstants.BUILTIN_PACKAGE_NAME)
                .Bundles
                .First(bundle => bundle.ProviderId == ResourceProviderIds.Resources)
                .Entries
                .Count;
            settings.EnsureDefaults();

            var builtinPackages = settings.Packages
                .Where(package => package != null && package.Name == ResourceConstants.BUILTIN_PACKAGE_NAME)
                .ToList();
            Assert.AreEqual(1, builtinPackages.Count);
            Assert.AreEqual($"Assets/StreamingAssets/{ResourceSettings.MANIFEST_NAME}", settings.ManifestOutputPath);

            var builtinPackage = builtinPackages[0];
            Assert.IsFalse(builtinPackage.IsHotUpdate);
            Assert.IsTrue(string.IsNullOrWhiteSpace(builtinPackage.CollectorId));
            Assert.AreEqual("single-bundle", builtinPackage.BuildStrategyId);

            var resourcesGroups = builtinPackage.Bundles
                .Where(bundle => bundle != null && bundle.ProviderId == ResourceProviderIds.Resources)
                .ToList();
            Assert.AreEqual(1, resourcesGroups.Count);
            Assert.AreEqual(ResourceEditorBuiltinConstants.ResourcesGroupName, resourcesGroups[0].Name);
            Assert.IsTrue(string.IsNullOrWhiteSpace(resourcesGroups[0].CollectorId));
            Assert.AreEqual(initialEntryCount, resourcesGroups[0].Entries.Count);
        }

        [Test]
        public void UnityResourcesCollector_ToResourcesLocation_UsesNoExtensionResourcesLocation()
        {
            Assert.AreEqual(
                "Resources/GameDeveloperKit/TagCatalog",
                UnityResourcesCollector.ToResourcesLocation("Assets/Resources/GameDeveloperKit/TagCatalog.asset"));
            Assert.AreEqual(
                "Resources/Fx/Explosion",
                UnityResourcesCollector.ToResourcesLocation("Assets/Game/Resources/Fx/Explosion.prefab"));
        }

        [Test]
        public void UnityResourcesCollector_IsRuntimeResourceAsset_RejectsFoldersAndEditorOnlyResources()
        {
            Assert.IsFalse(UnityResourcesCollector.IsRuntimeResourceAsset("Assets/Resources"));
            Assert.IsFalse(UnityResourcesCollector.IsRuntimeResourceAsset("Assets/Resources/Editor/EditorOnly.asset"));
            Assert.IsTrue(UnityResourcesCollector.IsRuntimeResourceAsset("Assets/Resources/GameDeveloperKit/TagCatalog.asset"));
        }

        [Test]
        public void ManifestPartitioner_WhenPackagesMixed_SplitsLocalAndHotManifests()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<ResourceEditorSettings>();
            settings.EnsureDefaults();
            var builtinPackage = settings.Packages.First(package => package.Name == ResourceConstants.BUILTIN_PACKAGE_NAME);
            var builtinBundle = builtinPackage.Bundles.First();

            var localPackage = CreatePackage("Base", false, "base-ui");
            var hotPackage = CreatePackage("Hot", true, "hot-ui");
            settings.Packages.Add(localPackage);
            settings.Packages.Add(hotPackage);

            var plan = new ResourceBuildPlan();
            plan.AddBundle(new ResourceBuildPlanBundle(
                builtinPackage,
                builtinBundle,
                ResourceEditorBuiltinConstants.ResourcesGroupName,
                new[]
                {
                    new ResourceGroupPreview(
                        "Assets/Resources/DefaultGUISkin.guiskin",
                        "Resources/DefaultGUISkin",
                        nameof(UnityEngine.GUISkin),
                        Array.Empty<string>(),
                        ResourceEditorBuiltinConstants.ResourcesGroupName,
                        ResourceEditorBuiltinConstants.ResourcesGroupName)
                }));
            plan.AddBundle(new ResourceBuildPlanBundle(
                localPackage,
                localPackage.Bundles[0],
                "base-built.bundle",
                new[] { CreatePreview("Assets/Game/UI/Loading.prefab", "Assets/Game/UI/Loading.prefab", "base-ui") }));
            plan.AddBundle(new ResourceBuildPlanBundle(
                hotPackage,
                hotPackage.Bundles[0],
                "hot-built.bundle",
                new[] { CreatePreview("Assets/Game/UI/Event.prefab", "Assets/Game/UI/Event.prefab", "hot-ui") }));

            var result = new ResourceBuildResult
            {
                Succeeded = true,
                BuildTime = 1,
                Artifacts = new List<ResourceBuildArtifact>
                {
                    new ResourceBuildArtifact { PackageName = "Base", BundleName = "base-built.bundle", Hash = "base-hash", Size = 10 },
                    new ResourceBuildArtifact { PackageName = "Hot", BundleName = "hot-built.bundle", Hash = "hot-hash", Size = 20 },
                }
            };
            var context = new ResourceBuildContext(
                settings,
                ResourceEditorRegistry.Scan(),
                settings.Packages,
                new Dictionary<ResourceEditorBundle, IReadOnlyList<ResourceGroupPreview>>(),
                settings.BuildSettings,
                DateTime.UtcNow,
                EditorUserBuildSettings.activeBuildTarget);

            var localManifest = ResourceManifestPartitioner.BuildLocalBaseManifest(context, plan, result);
            var hotManifest = ResourceManifestPartitioner.BuildHotUpdateManifest(context, plan, result);

            CollectionAssert.AreEquivalent(
                new[] { ResourceConstants.BUILTIN_PACKAGE_NAME, ResourceEditorBuiltinConstants.LocalPackageName, "Base" },
                localManifest.Packages.Select(package => package.Name).ToArray());
            CollectionAssert.AreEquivalent(
                new[] { "Hot" },
                hotManifest.Packages.Select(package => package.Name).ToArray());
            Assert.AreEqual(ResourceEditorBuiltinConstants.ResourcesGroupName, localManifest.GetBundle(ResourceEditorBuiltinConstants.ResourcesGroupName).Name);
            Assert.IsNull(hotManifest.Packages.FirstOrDefault(package => package.Name == ResourceConstants.BUILTIN_PACKAGE_NAME));
            Assert.IsTrue(localManifest.Packages.All(package => package.Name != "Hot"));
            Assert.AreEqual(ResourceProviderIds.Resources, localManifest.GetBundle(ResourceEditorBuiltinConstants.ResourcesGroupName).ProviderId);
            Assert.AreEqual("base-hash", localManifest.GetBundle("base-built.bundle").Hash);
            Assert.AreEqual("hot-hash", hotManifest.GetBundle("hot-built.bundle").Hash);
        }

        [Test]
        public void CreateSbpPlan_WhenBuiltinHasAssetBundleGroup_IncludesOnlyAssetBundleProviders()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<ResourceEditorSettings>();
            settings.EnsureDefaults();
            var builtinPackage = settings.Packages.First(package => package.Name == ResourceConstants.BUILTIN_PACKAGE_NAME);
            var resourcesGroup = builtinPackage.Bundles.First(bundle => bundle.ProviderId == ResourceProviderIds.Resources);
            var builtinAssetBundle = new ResourceEditorBundle
            {
                Name = "BuiltinHats",
                Group = "Hats",
                ProviderId = ResourceProviderIds.AssetBundle,
            };
            builtinAssetBundle.EnsureDefaults();
            builtinPackage.Bundles.Add(builtinAssetBundle);

            var plan = new ResourceBuildPlan();
            plan.AddBundle(new ResourceBuildPlanBundle(
                builtinPackage,
                resourcesGroup,
                "resources.bundle",
                new[] { CreatePreview("Assets/Resources/Foo.prefab", "Resources/Foo", resourcesGroup.Name) }));
            plan.AddBundle(new ResourceBuildPlanBundle(
                builtinPackage,
                builtinAssetBundle,
                "builtin-hats.bundle",
                new[] { CreatePreview("Assets/Game/Hats/Hat00.prefab", "Assets/Game/Hats/Hat00.prefab", builtinAssetBundle.Name) }));

            var sbpPlan = ResourceManifestPartitioner.CreateSbpPlan(plan);

            Assert.AreEqual(1, sbpPlan.Bundles.Count);
            Assert.AreSame(builtinAssetBundle, sbpPlan.Bundles[0].Bundle);
        }

        [Test]
        public void ResourceBuildWorkflow_WhenBundleHasEntries_CreatesPlanAndManifestFromEntries()
        {
            var assetPath = GameDeveloperKitEditorPaths.PackageAssetPath("Tests/Editor/Fixtures/Loading.prefab");
            var settings = UnityEngine.ScriptableObject.CreateInstance<ResourceEditorSettings>();
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var originalLabels = AssetDatabase.GetLabels(asset);
            try
            {
                AssetDatabase.SetLabels(asset, originalLabels.Concat(new[] { "hat" }).Distinct().ToArray());
                settings.EnsureDefaults();
                var package = CreatePackage("Base", false, "base-hats");
                var bundle = package.Bundles[0];
                var entry = new ResourceEditorAssetEntry
                {
                    Guid = AssetDatabase.AssetPathToGUID(assetPath),
                    AssetPath = assetPath,
                    Location = "hats/hat00",
                    TypeName = "GameObject",
                    ProviderId = ResourceProviderIds.AssetBundle
                };
                entry.EnsureDefaults(ResourceProviderIds.AssetBundle);
                bundle.Entries.Add(entry);
                settings.Packages.Add(package);

                var workflow = new ResourceBuildWorkflow(
                    settings,
                    ResourceEditorRegistry.Scan(),
                    settings.BuildSettings);
                var plan = workflow.CreatePlan(out var error);

                Assert.IsNull(error);
                Assert.IsNotNull(plan);
                var planBundle = plan.Bundles.FirstOrDefault(candidate => candidate.Bundle == bundle);
                Assert.IsNotNull(planBundle);
                Assert.AreEqual(1, planBundle.Resources.Count);
                Assert.AreEqual(assetPath, planBundle.Resources[0].AssetPath);
                Assert.AreEqual("hats/hat00", planBundle.Resources[0].Location);

                var context = new ResourceBuildContext(
                    settings,
                    ResourceEditorRegistry.Scan(),
                    settings.Packages,
                    new Dictionary<ResourceEditorBundle, IReadOnlyList<ResourceGroupPreview>>(),
                    settings.BuildSettings,
                    DateTime.UtcNow,
                    EditorUserBuildSettings.activeBuildTarget);
                var manifest = ResourceManifestPartitioner.BuildLocalBaseManifest(context, plan, new ResourceBuildResult { BuildTime = 1 });
                var manifestBundle = manifest.Packages.First(manifestPackage => manifestPackage.Name == "Base").Bundles.First();

                Assert.AreEqual(ResourceProviderIds.AssetBundle, manifestBundle.ProviderId);
                Assert.AreEqual("hats/hat00", manifestBundle.Assets[0].Location);
                Assert.AreEqual(assetPath, manifestBundle.Assets[0].AssetPath);
                CollectionAssert.Contains(manifestBundle.Assets[0].Labels, "hat");
            }
            finally
            {
                AssetDatabase.SetLabels(asset, originalLabels);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void BuiltinResourceChecker_WhenNonResourcesAssetUsesResourcesProvider_AddsError()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<ResourceEditorSettings>();
            settings.EnsureDefaults();
            var package = settings.Packages.First(package => package.Name == ResourceConstants.BUILTIN_PACKAGE_NAME);
            var bundle = package.Bundles.First(bundle => bundle.ProviderId == ResourceProviderIds.Resources);
            var resource = CreatePreview("Assets/Game/UI/Loading.prefab", "Assets/Game/UI/Loading.prefab", bundle.Name);
            var issues = new List<ResourceValidationIssue>();

            new BuiltinResourceChecker().Check(
                new ResourceCheckContext(
                    settings,
                    package,
                    bundle,
                    new[] { resource },
                    new Dictionary<ResourceEditorBundle, List<ResourceGroupPreview>> { [bundle] = new List<ResourceGroupPreview> { resource } }),
                issues);

            Assert.IsTrue(issues.Any(issue => issue.Severity == ResourceValidationSeverity.Error && issue.Message.Contains("Resources provider asset")));
        }

        [Test]
        public void BuiltinResourceChecker_WhenResourcesAssetUsesAssetBundleProvider_AddsWarning()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<ResourceEditorSettings>();
            settings.EnsureDefaults();
            var package = CreatePackage("Hot", true, "hot-ui");
            var bundle = package.Bundles[0];
            bundle.ProviderId = ResourceProviderIds.AssetBundle;
            var resource = CreatePreview("Assets/Resources/Foo.prefab", "Resources/Foo", bundle.Name);
            var issues = new List<ResourceValidationIssue>();

            new BuiltinResourceChecker().Check(
                new ResourceCheckContext(
                    settings,
                    package,
                    bundle,
                    new[] { resource },
                    new Dictionary<ResourceEditorBundle, List<ResourceGroupPreview>> { [bundle] = new List<ResourceGroupPreview> { resource } }),
                issues);

            Assert.IsTrue(issues.Any(issue => issue.Severity == ResourceValidationSeverity.Warning && issue.Message.Contains("asset-bundle group")));
            Assert.IsFalse(issues.Any(issue => issue.Severity == ResourceValidationSeverity.Error));
        }

        [Test]
        public void DuplicateResourceChecker_WhenAssetPathOrLocationDuplicated_AddsErrors()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<ResourceEditorSettings>();
            settings.EnsureDefaults();
            var package = CreatePackage("Base", false, "base-ui");
            settings.Packages.Add(package);
            var bundleA = package.Bundles[0];
            var bundleB = new ResourceEditorBundle
            {
                Name = "base-ui-copy",
                Group = "Default",
                ProviderId = ResourceProviderIds.AssetBundle,
            };
            bundleB.EnsureDefaults();
            package.Bundles.Add(bundleB);
            var previewA = CreatePreview("Assets/Game/UI/Loading.prefab", "ui/loading", bundleA.Name);
            var previewB = CreatePreview("Assets/Game/UI/Loading.prefab", "ui/loading", bundleB.Name);
            var previews = new Dictionary<ResourceEditorBundle, List<ResourceGroupPreview>>
            {
                [bundleA] = new List<ResourceGroupPreview> { previewA },
                [bundleB] = new List<ResourceGroupPreview> { previewB }
            };
            var issues = new List<ResourceValidationIssue>();

            new DuplicateResourceChecker().Check(new ResourceCheckContext(settings, package, bundleA, previews[bundleA], previews), issues);

            Assert.IsTrue(issues.Any(issue => issue.Message.Contains("Duplicate asset path")));
            Assert.IsTrue(issues.Any(issue => issue.Message.Contains("Duplicate asset location")));
        }

        [Test]
        public void EntryPreviewBuilder_WhenEntryExcluded_SkipsExcludedEntry()
        {
            var bundle = new ResourceEditorBundle
            {
                Name = "base-ui",
                Group = "Default",
                ProviderId = ResourceProviderIds.AssetBundle,
            };
            bundle.EnsureDefaults();
            bundle.Entries.Add(CreateEntry("Assets/Game/UI/Keep.prefab", "ui/keep", ResourceEntryExcludeKind.None));
            bundle.Entries.Add(CreateEntry("Assets/Game/UI/Drop.prefab", "ui/drop", ResourceEntryExcludeKind.Excluded));
            bundle.Entries.Add(CreateEntry("Assets/Game/UI/Gone.prefab", "ui/gone", ResourceEntryExcludeKind.Deleted));

            var previews = ResourceEditorEntryPreviewBuilder.Build(bundle);

            Assert.AreEqual(1, previews.Count);
            Assert.AreEqual("Assets/Game/UI/Keep.prefab", previews[0].AssetPath);
        }

        [Test]
        public void DuplicateResourceChecker_WhenOneDuplicateExcluded_NoDuplicateReported()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<ResourceEditorSettings>();
            settings.EnsureDefaults();
            var package = CreatePackage("Base", false, "base-ui");
            settings.Packages.Add(package);
            var bundleA = package.Bundles[0];
            bundleA.ProviderId = ResourceProviderIds.AssetBundle;
            bundleA.Entries.Add(CreateEntry("Assets/Game/UI/Loading.prefab", "ui/loading", ResourceEntryExcludeKind.None));
            var bundleB = new ResourceEditorBundle
            {
                Name = "base-ui-copy",
                Group = "Default",
                ProviderId = ResourceProviderIds.AssetBundle,
            };
            bundleB.EnsureDefaults();
            bundleB.Entries.Add(CreateEntry("Assets/Game/UI/Loading.prefab", "ui/loading", ResourceEntryExcludeKind.Excluded));
            package.Bundles.Add(bundleB);
            var previews = new Dictionary<ResourceEditorBundle, List<ResourceGroupPreview>>
            {
                [bundleA] = ResourceEditorEntryPreviewBuilder.Build(bundleA),
                [bundleB] = ResourceEditorEntryPreviewBuilder.Build(bundleB)
            };
            var issues = new List<ResourceValidationIssue>();

            new DuplicateResourceChecker().Check(new ResourceCheckContext(settings, package, bundleA, previews[bundleA], previews), issues);

            Assert.IsFalse(issues.Any(issue => issue.Message.Contains("Duplicate asset path")));
            Assert.IsFalse(issues.Any(issue => issue.Message.Contains("Duplicate asset location")));
        }

        [Test]
        public void DependencyOwnership_WhenImplicitDependencyHasMultipleBundles_AddsWarningWithSize()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<ResourceEditorSettings>();
            settings.EnsureDefaults();
            var package = CreatePackage("Base", false, "bundle-a");
            var bundleA = package.Bundles[0];
            var bundleB = new ResourceEditorBundle { Name = "bundle-b" };
            bundleB.EnsureDefaults();
            package.Bundles.Add(bundleB);
            settings.Packages.Add(package);
            var previews = new Dictionary<ResourceEditorBundle, List<ResourceGroupPreview>>
            {
                [bundleA] = new List<ResourceGroupPreview> { CreatePreview("Assets/A.prefab", "a", bundleA.Name) },
                [bundleB] = new List<ResourceGroupPreview> { CreatePreview("Assets/B.prefab", "b", bundleB.Name) }
            };
            var issues = new List<ResourceValidationIssue>();

            ResourceDependencyOwnershipAnalyzer.Analyze(
                settings,
                previews,
                _ => new[] { "Assets/Shared.png" },
                _ => 128L,
                issues);

            var issue = issues.Single();
            Assert.AreEqual(ResourceValidationSeverity.Warning, issue.Severity);
            StringAssert.Contains("Assets/Shared.png", issue.Message);
            StringAssert.Contains("128 bytes", issue.Message);
            StringAssert.Contains("Base/bundle-a", issue.Message);
            StringAssert.Contains("Base/bundle-b", issue.Message);
        }

        [Test]
        public void DependencyOwnership_WhenDependencyIsExplicitlyOwned_DoesNotReport()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<ResourceEditorSettings>();
            settings.EnsureDefaults();
            var package = CreatePackage("Base", false, "bundle-a");
            var bundleA = package.Bundles[0];
            var bundleB = new ResourceEditorBundle { Name = "bundle-b" };
            bundleB.EnsureDefaults();
            package.Bundles.Add(bundleB);
            settings.Packages.Add(package);
            var previews = new Dictionary<ResourceEditorBundle, List<ResourceGroupPreview>>
            {
                [bundleA] = new List<ResourceGroupPreview> { CreatePreview("Assets/A.prefab", "a", bundleA.Name) },
                [bundleB] = new List<ResourceGroupPreview>
                {
                    CreatePreview("Assets/B.prefab", "b", bundleB.Name),
                    CreatePreview("Assets/Shared.png", "shared", bundleB.Name)
                }
            };
            var issues = new List<ResourceValidationIssue>();

            ResourceDependencyOwnershipAnalyzer.Analyze(
                settings,
                previews,
                _ => new[] { "Assets/Shared.png" },
                _ => 128L,
                issues);

            Assert.IsEmpty(issues);
        }

        [Test]
        public void DependencyOwnership_WhenOnlyScriptsAreShared_DoesNotReport()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<ResourceEditorSettings>();
            settings.EnsureDefaults();
            var package = CreatePackage("Base", false, "bundle-a");
            var bundleA = package.Bundles[0];
            var bundleB = new ResourceEditorBundle { Name = "bundle-b" };
            bundleB.EnsureDefaults();
            package.Bundles.Add(bundleB);
            settings.Packages.Add(package);
            var previews = new Dictionary<ResourceEditorBundle, List<ResourceGroupPreview>>
            {
                [bundleA] = new List<ResourceGroupPreview> { CreatePreview("Assets/A.prefab", "a", bundleA.Name) },
                [bundleB] = new List<ResourceGroupPreview> { CreatePreview("Assets/B.prefab", "b", bundleB.Name) }
            };
            var issues = new List<ResourceValidationIssue>();

            ResourceDependencyOwnershipAnalyzer.Analyze(
                settings,
                previews,
                _ => new[] { "Assets/Shared.cs" },
                _ => 128L,
                issues);

            Assert.IsEmpty(issues);
        }

        [Test]
        public void BuildLocalBaseManifest_WhenLocalDefaultBundleEmpty_SkipsEmptyAssetBundle()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<ResourceEditorSettings>();
            settings.EnsureDefaults();
            var localPackage = settings.Packages.First(package => package.Name == ResourceEditorBuiltinConstants.LocalPackageName);
            var defaultBundle = localPackage.Bundles.First(bundle => bundle.Name == ResourceEditorBuiltinConstants.LocalBundleName);
            var plan = new ResourceBuildPlan();
            plan.AddBundle(new ResourceBuildPlanBundle(
                localPackage,
                defaultBundle,
                $"{ResourceEditorBuiltinConstants.LocalBundleName}.bundle",
                Array.Empty<ResourceGroupPreview>()));
            var context = new ResourceBuildContext(
                settings,
                ResourceEditorRegistry.Scan(),
                settings.Packages,
                new Dictionary<ResourceEditorBundle, IReadOnlyList<ResourceGroupPreview>>(),
                settings.BuildSettings,
                DateTime.UtcNow,
                EditorUserBuildSettings.activeBuildTarget);

            var manifest = ResourceManifestPartitioner.BuildLocalBaseManifest(context, plan, new ResourceBuildResult { BuildTime = 1 });

            var manifestLocalPackage = manifest.Packages.FirstOrDefault(package => package.Name == ResourceEditorBuiltinConstants.LocalPackageName);
            Assert.IsNotNull(manifestLocalPackage);
            Assert.IsFalse(manifestLocalPackage.Bundles.Any(bundle => bundle.Name == ResourceEditorBuiltinConstants.LocalBundleName));
        }

        [Test]
        public void BuildPreviewManifest_WhenAssetBundleEmpty_SkipsEmptyAssetBundle()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<ResourceEditorSettings>();
            settings.EnsureDefaults();
            var localPackage = settings.Packages.First(package => package.Name == ResourceEditorBuiltinConstants.LocalPackageName);

            var manifest = ResourceManifestPreviewBuilder.Build(
                settings,
                new Dictionary<ResourceEditorBundle, List<ResourceGroupPreview>>(),
                ResourceManifestPartitioner.IsLocalBasePackage);

            var manifestLocalPackage = manifest.Packages.FirstOrDefault(package => package.Name == ResourceEditorBuiltinConstants.LocalPackageName);
            Assert.IsNotNull(manifestLocalPackage);
            Assert.IsFalse(manifestLocalPackage.Bundles.Any(bundle => bundle.Name == ResourceEditorBuiltinConstants.LocalBundleName));
        }

        [Test]
        public void ResolveLocalManifestPath_WhenUnset_UsesStreamingAssetsManifest()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<ResourceEditorSettings>();
            settings.EnsureDefaults();

            var path = ResourceManifestPartitioner.ResolveLocalManifestPath(settings).Replace('\\', '/');

            StringAssert.EndsWith("Assets/StreamingAssets/manifest.json", path);
        }

        [Test]
        public void ResolveLocalBundlePath_WhenHashExists_UsesBundleNameOnly()
        {
            var artifact = new ResourceBuildArtifact
            {
                BundleName = "ui.bundle",
                Hash = "0123456789abcdef"
            };

            var path = ResourceManifestPartitioner.ResolveLocalBundlePath(artifact).Replace('\\', '/');

            StringAssert.EndsWith("Assets/StreamingAssets/ui.bundle", path);
        }

        [Test]
        public void BuildEditorSimulatorManifest_ReturnsCompleteSnapshotWithoutWritingPlayerManifest()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<ResourceEditorSettings>();
            settings.EnsureDefaults();
            settings.ManifestOutputPath = $"Temp/ResourceEditorBuiltinPackageTests/{Guid.NewGuid():N}/manifest.json";
            var manifestPath = ResourceManifestPartitioner.ResolveLocalManifestPath(settings);
            var localPackage = CreatePackage("EditorLocalTest", false, "editor-local-test");
            var hotPackage = CreatePackage("EditorHotTest", true, "editor-hot-test");
            settings.Packages.Add(localPackage);
            settings.Packages.Add(hotPackage);
            try
            {
                var manifest = ResourceEditorPlayModeManifestProvider.BuildEditorSimulatorManifest(
                    settings,
                    ResourceEditorRegistry.Scan());

                Assert.IsFalse(System.IO.File.Exists(manifestPath));
                Assert.IsNotNull(manifest.Packages.FirstOrDefault(package => package.Name == ResourceConstants.BUILTIN_PACKAGE_NAME));
                Assert.IsNotNull(manifest.Packages.FirstOrDefault(package => package.Name == "EditorLocalTest"));
                Assert.IsNotNull(manifest.Packages.FirstOrDefault(package => package.Name == "EditorHotTest"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        private static ResourceEditorPackage CreatePackage(string name, bool isHotUpdate, string bundleName)
        {
            var package = new ResourceEditorPackage
            {
                Name = name,
                IsHotUpdate = isHotUpdate,
                BuildStrategyId = "single-bundle",
                CollectorId = "explicit-assets",
            };
            package.EnsureDefaults();
            package.Bundles.Add(new ResourceEditorBundle
            {
                Name = bundleName,
                Group = "Default",
                CollectorId = "explicit-assets",
            });
            package.Bundles[0].EnsureDefaults();
            return package;
        }

        private static ResourceEditorAssetEntry CreateEntry(string assetPath, string location, ResourceEntryExcludeKind excludeKind)
        {
            var entry = new ResourceEditorAssetEntry
            {
                AssetPath = assetPath,
                Location = location,
                TypeName = "GameObject",
                ProviderId = ResourceProviderIds.AssetBundle,
                ExcludeKind = excludeKind,
            };
            entry.EnsureDefaults(ResourceProviderIds.AssetBundle);
            return entry;
        }

        private static ResourceGroupPreview CreatePreview(string assetPath, string location, string bundleName)
        {
            return new ResourceGroupPreview(
                assetPath,
                location,
                "GameObject",
                Array.Empty<string>(),
                bundleName,
                "Default");
        }
    }
}
