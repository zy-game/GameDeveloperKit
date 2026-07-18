using System;
using System.IO;
using System.Linq;
using GameDeveloperKit.Resource;
using GameDeveloperKit.ResourceEditor;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;

namespace GameDeveloperKit.Tests
{
    public sealed class ResourceAuthoringServiceTests
    {
        private const string UnresolvedGuid = "11111111111111111111111111111111";

        [Test]
        public void AddEntry_WhenAssetExists_CapturesStableGuid()
        {
            var assetPath = GameDeveloperKitEditorPaths.PackageAssetPath("Tests/Editor/Fixtures/Loading.prefab");
            var bundle = new ResourceEditorBundle
            {
                Name = "identity-assets",
                ProviderId = ResourceProviderIds.AssetBundle,
                CollectorId = "explicit-assets"
            };
            bundle.EnsureDefaults();

            var changed = ResourceEditorEntryTable.AddEntry(bundle, assetPath);

            Assert.IsTrue(changed);
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(assetPath), bundle.Entries[0].Guid);
            Assert.AreEqual(assetPath, AssetDatabase.GUIDToAssetPath(bundle.Entries[0].Guid));
        }

        [Test]
        public void EnsureDefaults_WhenEntryHasNoGuid_PreservesEntryForPreflight()
        {
            var bundle = new ResourceEditorBundle
            {
                Name = "invalid-assets",
                ProviderId = ResourceProviderIds.AssetBundle,
                CollectorId = "explicit-assets"
            };
            bundle.EnsureDefaults();
            var entry = CreateEntry("Assets/Missing/Unresolved.prefab");
            bundle.Entries.Add(entry);

            bundle.EnsureDefaults();

            Assert.AreEqual(string.Empty, entry.Guid);
            Assert.AreEqual("Assets/Missing/Unresolved.prefab", entry.AssetPath);
        }

        [Test]
        public void BuildSnapshotAndPlan_WhenGuidAssetMoves_UseCurrentPathAndKeepIdentity()
        {
            const string folderPath = "Assets/ResourceAuthoringGuidMoveTests";
            const string originalPath = folderPath + "/Original.asset";
            const string movedPath = folderPath + "/Moved.asset";
            EnsureFolder(folderPath);
            var asset = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            AssetDatabase.CreateAsset(asset, originalPath);
            AssetDatabase.SaveAssets();
            var guid = AssetDatabase.AssetPathToGUID(originalPath);
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            try
            {
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "Move", "move-assets");
                var entry = CreateEntry(originalPath);
                entry.Guid = guid;
                package.Bundles[0].Entries.Add(entry);
                var registry = ResourceEditorRegistry.Scan();
                var before = ResourceAuthoringService.BuildSnapshot(settings, registry);

                Assert.AreEqual(string.Empty, AssetDatabase.MoveAsset(originalPath, movedPath));
                var after = ResourceAuthoringService.BuildSnapshot(settings, registry);
                settings.BuildSettings.Scope = ResourceBuildScope.AllPackages;
                var workflow = new ResourceBuildWorkflow(settings, registry, settings.BuildSettings);
                var plan = workflow.CreatePlan(out var error);

                Assert.AreEqual(guid, entry.Guid);
                Assert.AreEqual(originalPath, entry.AssetPath);
                Assert.AreNotEqual(before.Revision, after.Revision);
                Assert.IsFalse(after.Manifest.Packages
                    .SelectMany(manifestPackage => manifestPackage.Bundles)
                    .SelectMany(bundle => bundle.Assets)
                    .Any(manifestAsset => manifestAsset.AssetPath == originalPath));
                Assert.IsTrue(after.Manifest.Packages
                    .SelectMany(manifestPackage => manifestPackage.Bundles)
                    .SelectMany(bundle => bundle.Assets)
                    .Any(manifestAsset => manifestAsset.AssetPath == movedPath));
                Assert.IsNull(error);
                Assert.IsNotNull(plan);
                var planBundle = plan.Bundles.Single(candidate => candidate.Bundle == package.Bundles[0]);
                Assert.AreEqual(movedPath, planBundle.Resources.Single().AssetPath);
                Assert.IsFalse(planBundle.Resources.Any(resource => resource.AssetPath == originalPath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                AssetDatabase.DeleteAsset(folderPath);
            }
        }

        [Test]
        public void BuildSnapshot_WhenCurrentGuidInvalidOrUnresolved_ExcludesEntries()
        {
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            try
            {
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "Invalid", "invalid-assets");
                var invalid = CreateEntry("Assets/Invalid.asset");
                invalid.Guid = "not-a-guid";
                var unresolved = CreateEntry("Assets/Unresolved.asset");
                unresolved.Guid = System.Guid.NewGuid().ToString("N");
                package.Bundles[0].Entries.Add(invalid);
                package.Bundles[0].Entries.Add(unresolved);

                var snapshot = ResourceAuthoringService.BuildSnapshot(settings, ResourceEditorRegistry.Scan());

                Assert.IsTrue(snapshot.Issues.Any(issue =>
                    issue.Message == "Configured resource GUID is invalid: not-a-guid"));
                Assert.IsTrue(snapshot.Issues.Any(issue =>
                    issue.Message == $"Configured resource GUID cannot be resolved: {unresolved.Guid}"));
                Assert.IsFalse(snapshot.Manifest.Packages
                    .SelectMany(manifestPackage => manifestPackage.Bundles)
                    .SelectMany(bundle => bundle.Assets)
                    .Any(asset => asset.Location == invalid.Location || asset.Location == unresolved.Location));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void BuildSnapshot_WhenActiveGuidDuplicated_ExcludesEveryMembership()
        {
            var assetPath = GameDeveloperKitEditorPaths.PackageAssetPath("Tests/Editor/Fixtures/Loading.prefab");
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            try
            {
                settings.EnsureDefaults();
                var guid = AssetDatabase.AssetPathToGUID(assetPath);
                var firstPackage = CreatePackage(settings, "DuplicateA", "duplicate-a");
                var secondPackage = CreatePackage(settings, "DuplicateB", "duplicate-b");
                var first = CreateEntry(assetPath);
                first.Guid = guid;
                first.Location = "duplicate/a";
                var second = CreateEntry(assetPath);
                second.Guid = guid;
                second.Location = "duplicate/b";
                firstPackage.Bundles[0].Entries.Add(first);
                secondPackage.Bundles[0].Entries.Add(second);

                var snapshot = ResourceAuthoringService.BuildSnapshot(settings, ResourceEditorRegistry.Scan());

                Assert.AreEqual(2, snapshot.Issues.Count(issue =>
                    issue.Message == $"Configured resource GUID has multiple active memberships: {guid}"));
                Assert.IsFalse(snapshot.Manifest.Packages
                    .SelectMany(manifestPackage => manifestPackage.Bundles)
                    .SelectMany(bundle => bundle.Assets)
                    .Any(asset => asset.Location == first.Location || asset.Location == second.Location));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Reconcile_WhenExplicitAssetMovesThenDeletes_PreservesAddressThenRemovesMembership()
        {
            const string folderPath = "Assets/ResourceAuthoringExplicitTests";
            const string originalPath = folderPath + "/Original.asset";
            const string movedPath = folderPath + "/Moved.asset";
            EnsureFolder(folderPath);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ResourceEditorSettings>(), originalPath);
            AssetDatabase.SaveAssets();
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            try
            {
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "Explicit", "explicit-assets-test");
                var entry = CreateEntry(originalPath);
                entry.Location = "stable/address";
                package.Bundles[0].Entries.Add(entry);

                Assert.AreEqual(string.Empty, AssetDatabase.MoveAsset(originalPath, movedPath));
                ResourceAuthoringService.Reconcile(
                    settings,
                    ResourceEditorRegistry.Scan(),
                    new ResourceAssetChangeSet(
                        movedAssets: new[] { new ResourceAssetMove(originalPath, movedPath) }));

                Assert.AreEqual(movedPath, entry.AssetPath);
                Assert.AreEqual("stable/address", entry.Location);

                AssetDatabase.DeleteAsset(movedPath);
                ResourceAuthoringService.Reconcile(
                    settings,
                    ResourceEditorRegistry.Scan(),
                    new ResourceAssetChangeSet(deletedAssets: new[] { movedPath }));

                Assert.IsFalse(package.Bundles[0].Entries.Contains(entry));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                AssetDatabase.DeleteAsset(folderPath);
            }
        }

        [Test]
        public void Reconcile_WhenFullScanFindsUnresolvedExplicitGuid_RemovesMembership()
        {
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            try
            {
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "Explicit", "explicit-full-scan-test");
                var entry = CreateEntry("Assets/Missing/Deleted.asset");
                entry.Guid = UnresolvedGuid;
                package.Bundles[0].Entries.Add(entry);

                ResourceAuthoringService.Reconcile(
                    settings,
                    ResourceEditorRegistry.Scan(),
                    new ResourceAssetChangeSet(fullReconcile: true));

                Assert.IsFalse(package.Bundles[0].Entries.Contains(entry));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Reconcile_WhenFullScanFindsUnresolvedRuleGuid_RemovesMembership()
        {
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            try
            {
                settings.EnsureDefaults();
                var package = new ResourceEditorPackage
                {
                    Name = "Folder",
                    BuildStrategyId = "single-bundle",
                    CollectorId = "folder-assets"
                };
                package.EnsureDefaults();
                var bundle = new ResourceEditorBundle
                {
                    Name = "folder-full-scan-test",
                    ProviderId = ResourceProviderIds.AssetBundle,
                    CollectorId = "folder-assets",
                    SourceFolder = "Assets/ResourceAuthoringMissingRuleTests"
                };
                bundle.EnsureDefaults();
                var entry = CreateEntry("Assets/Missing/Deleted.asset");
                entry.Guid = UnresolvedGuid;
                bundle.Entries.Add(entry);
                package.Bundles.Add(bundle);
                settings.Packages.Add(package);

                ResourceAuthoringService.Reconcile(
                    settings,
                    ResourceEditorRegistry.Scan(),
                    new ResourceAssetChangeSet(fullReconcile: true));

                Assert.IsFalse(bundle.Entries.Contains(entry));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Reconcile_WhenFolderRuleAssetMovesOut_RemovesRuleMembership()
        {
            const string rootPath = "Assets/ResourceAuthoringFolderTests";
            const string sourcePath = rootPath + "/Source";
            const string outsidePath = rootPath + "/Outside";
            const string originalPath = sourcePath + "/Original.asset";
            const string movedPath = outsidePath + "/Moved.asset";
            EnsureFolder(rootPath);
            EnsureFolder(sourcePath);
            EnsureFolder(outsidePath);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ResourceEditorSettings>(), originalPath);
            AssetDatabase.SaveAssets();
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            try
            {
                settings.EnsureDefaults();
                var package = new ResourceEditorPackage
                {
                    Name = "Folder",
                    BuildStrategyId = "single-bundle",
                    CollectorId = "folder-assets"
                };
                package.EnsureDefaults();
                var bundle = new ResourceEditorBundle
                {
                    Name = "folder-assets-test",
                    ProviderId = ResourceProviderIds.AssetBundle,
                    CollectorId = "folder-assets",
                    SourceFolder = sourcePath
                };
                bundle.EnsureDefaults();
                package.Bundles.Add(bundle);
                settings.Packages.Add(package);

                ResourceAuthoringService.Reconcile(
                    settings,
                    ResourceEditorRegistry.Scan(),
                    new ResourceAssetChangeSet(importedAssets: new[] { originalPath }));
                var guid = AssetDatabase.AssetPathToGUID(originalPath);
                Assert.IsTrue(bundle.Entries.Any(entry => entry.Guid == guid));

                Assert.AreEqual(string.Empty, AssetDatabase.MoveAsset(originalPath, movedPath));
                ResourceAuthoringService.Reconcile(
                    settings,
                    ResourceEditorRegistry.Scan(),
                    new ResourceAssetChangeSet(
                        movedAssets: new[] { new ResourceAssetMove(originalPath, movedPath) }));

                Assert.IsFalse(bundle.Entries.Any(entry => entry.Guid == guid));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                AssetDatabase.DeleteAsset(rootPath);
            }
        }

        [Test]
        public void Reconcile_WhenResourcesAssetMoves_RecalculatesLocationAndKeepsGuid()
        {
            const string rootPath = "Assets/ResourceAuthoringResourcesTests";
            const string resourcesPath = rootPath + "/Resources";
            const string nestedPath = resourcesPath + "/Nested";
            const string originalPath = resourcesPath + "/Original.asset";
            const string movedPath = nestedPath + "/Moved.asset";
            EnsureFolder(rootPath);
            EnsureFolder(resourcesPath);
            EnsureFolder(nestedPath);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ResourceEditorSettings>(), originalPath);
            AssetDatabase.SaveAssets();
            var guid = AssetDatabase.AssetPathToGUID(originalPath);
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            try
            {
                settings.EnsureDefaults();
                var resourcesBundle = settings.Packages
                    .First(ResourceEditorBuiltinConstants.IsBuiltinPackage)
                    .Bundles.First(ResourceEditorBuiltinConstants.IsResourcesGroup);
                ResourceAuthoringService.Reconcile(
                    settings,
                    ResourceEditorRegistry.Scan(),
                    new ResourceAssetChangeSet(importedAssets: new[] { originalPath }));
                var entry = resourcesBundle.Entries.Single(candidate => candidate.Guid == guid);

                Assert.AreEqual(string.Empty, AssetDatabase.MoveAsset(originalPath, movedPath));
                ResourceAuthoringService.Reconcile(
                    settings,
                    ResourceEditorRegistry.Scan(),
                    new ResourceAssetChangeSet(
                        movedAssets: new[] { new ResourceAssetMove(originalPath, movedPath) }));

                Assert.AreEqual(guid, entry.Guid);
                Assert.AreEqual(movedPath, entry.AssetPath);
                Assert.AreEqual("Resources/Nested/Moved", entry.Location);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                AssetDatabase.DeleteAsset(rootPath);
            }
        }

        [Test]
        public void SnapshotStore_WhenMutationExists_SavesOnceAndReplacesManifest()
        {
            var assetPath = GameDeveloperKitEditorPaths.PackageAssetPath("Tests/Editor/Fixtures/Loading.prefab");
            var rootPath = Path.GetFullPath("Temp/ResourceAuthoringSnapshotStoreSuccess");
            var manifestPath = Path.Combine(rootPath, "manifest.json");
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            try
            {
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "Store", "store-assets");
                var entry = CreateEntry(assetPath);
                package.Bundles[0].Entries.Add(entry);
                var plan = ResourceAuthoringMutationPlan.Capture(settings);
                entry.Location = "changed/location";
                var snapshot = ResourceAuthoringService.BuildSnapshot(settings, ResourceEditorRegistry.Scan());
                var saveCount = 0;

                ResourceAuthoringSnapshotStore.Commit(
                    snapshot,
                    plan,
                    () => saveCount++,
                    manifestPath);

                Assert.AreEqual(1, saveCount);
                Assert.IsTrue(IOFile.Exists(manifestPath));
                Assert.IsFalse(IOFile.Exists(manifestPath + ".tmp"));
                var manifest = JsonConvert.DeserializeObject<ManifestInfo>(IOFile.ReadAllText(manifestPath));
                Assert.IsTrue(manifest.Packages
                    .SelectMany(manifestPackage => manifestPackage.Bundles)
                    .SelectMany(bundle => bundle.Assets)
                    .Any(asset => asset.Location == "changed/location"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                if (IODirectory.Exists(rootPath))
                {
                    IODirectory.Delete(rootPath, true);
                }
            }
        }

        [Test]
        public void SnapshotStore_WhenSettingsSaveFails_RollsBackAndPreservesManifest()
        {
            var assetPath = GameDeveloperKitEditorPaths.PackageAssetPath("Tests/Editor/Fixtures/Loading.prefab");
            var rootPath = Path.GetFullPath("Temp/ResourceAuthoringSnapshotStoreSaveFailure");
            var manifestPath = Path.Combine(rootPath, "manifest.json");
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            try
            {
                IODirectory.CreateDirectory(rootPath);
                IOFile.WriteAllText(manifestPath, "sentinel");
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "Rollback", "rollback-assets");
                var entry = CreateEntry(assetPath);
                entry.Location = "before/location";
                package.Bundles[0].Entries.Add(entry);
                var plan = ResourceAuthoringMutationPlan.Capture(settings);
                entry.Location = "after/location";
                var snapshot = ResourceAuthoringService.BuildSnapshot(settings, ResourceEditorRegistry.Scan());

                Assert.Throws<InvalidOperationException>(() =>
                    ResourceAuthoringSnapshotStore.Commit(
                        snapshot,
                        plan,
                        () => throw new InvalidOperationException("save failed"),
                        manifestPath));

                Assert.AreEqual("before/location", entry.Location);
                Assert.AreEqual("sentinel", IOFile.ReadAllText(manifestPath));
                Assert.IsFalse(IOFile.Exists(manifestPath + ".tmp"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                if (IODirectory.Exists(rootPath))
                {
                    IODirectory.Delete(rootPath, true);
                }
            }
        }

        [Test]
        public void SnapshotStore_WhenTempCannotBeCreated_DoesNotSaveAndRollsBack()
        {
            var assetPath = GameDeveloperKitEditorPaths.PackageAssetPath("Tests/Editor/Fixtures/Loading.prefab");
            var rootPath = Path.GetFullPath("Temp/ResourceAuthoringSnapshotStoreTempFailure");
            var blockerPath = Path.Combine(rootPath, "blocker");
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            try
            {
                IODirectory.CreateDirectory(rootPath);
                IOFile.WriteAllText(blockerPath, "blocker");
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "TempFailure", "temp-failure-assets");
                var entry = CreateEntry(assetPath);
                entry.Location = "before/location";
                package.Bundles[0].Entries.Add(entry);
                var plan = ResourceAuthoringMutationPlan.Capture(settings);
                entry.Location = "after/location";
                var snapshot = ResourceAuthoringService.BuildSnapshot(settings, ResourceEditorRegistry.Scan());
                var saveCount = 0;

                Assert.Throws<IOException>(() =>
                    ResourceAuthoringSnapshotStore.Commit(
                        snapshot,
                        plan,
                        () => saveCount++,
                        Path.Combine(blockerPath, "manifest.json")));

                Assert.AreEqual(0, saveCount);
                Assert.AreEqual("before/location", entry.Location);
                Assert.AreEqual("blocker", IOFile.ReadAllText(blockerPath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                if (IODirectory.Exists(rootPath))
                {
                    IODirectory.Delete(rootPath, true);
                }
            }
        }

        [Test]
        public void ReconciliationIntegration_SourceGuardsKeepSingleGuidPipeline()
        {
            var watcherSource = IOFile.ReadAllText(
                "Assets/GameDeveloperKit/Editor/ResourceEditor/ResourceEditorAssetWatcher.cs");
            var reconciliationSource = IOFile.ReadAllText(
                "Assets/GameDeveloperKit/Editor/ResourceEditor/Authoring/ResourceAuthoringReconciliation.cs");
            var snapshotStoreSource = IOFile.ReadAllText(
                "Assets/GameDeveloperKit/Editor/ResourceEditor/Authoring/ResourceAuthoringSnapshotStore.cs");
            var validatorSource = IOFile.ReadAllText(
                "Assets/GameDeveloperKit/Editor/ResourceEditor/Authoring/ResourceAuthoringAssetValidator.cs");

            StringAssert.Contains("EditorApplication.delayCall += Drain", watcherSource);
            StringAssert.Contains("ResourceAuthoringService.Reconcile(changes)", watcherSource);
            StringAssert.DoesNotContain("LoadOrCreate(", watcherSource);
            StringAssert.DoesNotContain("WriteLocalBaseManifest", watcherSource);
            StringAssert.DoesNotContain("StreamingAssets", watcherSource);
            StringAssert.DoesNotContain("LoadOrCreate(", reconciliationSource);
            StringAssert.DoesNotContain("StreamingAssets", reconciliationSource);
            StringAssert.Contains("Library/GameDeveloperKit/ResourceEditor/manifest.json", snapshotStoreSource);
            StringAssert.DoesNotContain("AssetPathToGUID(entry.AssetPath)", validatorSource);
        }

        [Test]
        public void BuildSnapshot_WhenInputUnchanged_ReturnsStableRevisionAndManifest()
        {
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            try
            {
                settings.EnsureDefaults();
                var registry = ResourceEditorRegistry.Scan();

                var first = ResourceAuthoringService.BuildSnapshot(settings, registry);
                var second = ResourceAuthoringService.BuildSnapshot(settings, registry);

                Assert.AreEqual(first.Revision, second.Revision);
                Assert.AreEqual(
                    JsonConvert.SerializeObject(first.Manifest),
                    JsonConvert.SerializeObject(second.Manifest));
                Assert.IsFalse(first.Issues.Any(issue => issue.Severity == ResourceValidationSeverity.Error));
                CollectionAssert.AreEqual(
                    first.Issues.Select(FormatIssue).ToArray(),
                    second.Issues.Select(FormatIssue).ToArray());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void BuildSnapshot_WhenCheckerInputChanges_ReturnsStableIssuesAndNewRevision()
        {
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            try
            {
                settings.EnsureDefaults();
                var package = settings.Packages.First();
                var bundle = package.Bundles.First();
                var registry = ResourceEditorRegistry.Scan();
                var baseline = ResourceAuthoringService.BuildSnapshot(settings, registry);

                bundle.Group = string.Empty;
                var first = ResourceAuthoringService.BuildSnapshot(settings, registry);
                var second = ResourceAuthoringService.BuildSnapshot(settings, registry);

                Assert.AreNotEqual(baseline.Revision, first.Revision);
                Assert.AreEqual(first.Revision, second.Revision);
                Assert.IsTrue(first.Issues.Any(issue =>
                    issue.Source == nameof(BasicResourceChecker) &&
                    issue.Message == "Bundle group cannot be empty." &&
                    ReferenceEquals(issue.Package, package) &&
                    ReferenceEquals(issue.Bundle, bundle)));
                CollectionAssert.AreEqual(
                    first.Issues.Select(FormatIssue).ToArray(),
                    second.Issues.Select(FormatIssue).ToArray());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void BuildSnapshot_WhenBuildStrategyMissing_ReturnsRegistryError()
        {
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            try
            {
                settings.EnsureDefaults();
                var package = settings.Packages.First();
                package.BuildStrategyId = "missing-strategy";

                var snapshot = ResourceAuthoringService.BuildSnapshot(settings, ResourceEditorRegistry.Scan());

                Assert.IsTrue(snapshot.Issues.Any(issue =>
                    issue.Severity == ResourceValidationSeverity.Error &&
                    issue.Source == "Registry" &&
                    issue.Message == "Missing: missing-strategy" &&
                    ReferenceEquals(issue.Package, package)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void BuildSnapshot_WhenActiveEntriesInvalid_ReturnsStructuredErrorsInEntryOrder()
        {
            const string folderPath = "Assets/ResourceAuthoringServiceTests";
            EnsureFolder(folderPath);
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            try
            {
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "Validation", "validation-assets");
                var bundle = package.Bundles[0];
                bundle.Entries.Add(CreateEntry(string.Empty));
                bundle.Entries.Add(CreateEntry(folderPath));
                var unresolved = CreateEntry(folderPath + "/Missing.asset");
                unresolved.Guid = UnresolvedGuid;
                bundle.Entries.Add(unresolved);

                var snapshot = ResourceAuthoringService.BuildSnapshot(settings, ResourceEditorRegistry.Scan());
                var authoringErrors = snapshot.Issues
                    .Where(issue => issue.Source == ResourceAuthoringService.IssueSource)
                    .ToArray();

                Assert.AreEqual(3, authoringErrors.Length);
                Assert.AreEqual("Configured resource GUID cannot be empty.", authoringErrors[0].Message);
                Assert.AreEqual($"Configured resource is a folder, not an asset: {folderPath}", authoringErrors[1].Message);
                Assert.AreEqual($"Configured resource GUID cannot be resolved: {UnresolvedGuid}", authoringErrors[2].Message);
                Assert.IsTrue(authoringErrors.All(issue =>
                    ReferenceEquals(issue.Package, package) &&
                    ReferenceEquals(issue.Bundle, bundle) &&
                    issue.Resource != null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                AssetDatabase.DeleteAsset(folderPath);
            }
        }

        [Test]
        public void BuildSnapshot_WhenMissingEntriesExcluded_DoesNotBlockOrEnterManifest()
        {
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            try
            {
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "Excluded", "excluded-assets");
                var bundle = package.Bundles[0];
                var excluded = CreateEntry("Assets/MissingExcluded.asset");
                excluded.ExcludeKind = ResourceEntryExcludeKind.Excluded;
                var deleted = CreateEntry("Assets/MissingDeleted.asset");
                deleted.ExcludeKind = ResourceEntryExcludeKind.Deleted;
                bundle.Entries.Add(excluded);
                bundle.Entries.Add(deleted);

                var snapshot = ResourceAuthoringService.BuildSnapshot(settings, ResourceEditorRegistry.Scan());

                Assert.IsFalse(snapshot.Issues.Any(issue =>
                    issue.Source == ResourceAuthoringService.IssueSource &&
                    (issue.Resource?.AssetPath == excluded.AssetPath || issue.Resource?.AssetPath == deleted.AssetPath)));
                Assert.IsFalse(snapshot.Manifest.Packages
                    .SelectMany(manifestPackage => manifestPackage.Bundles)
                    .SelectMany(manifestBundle => manifestBundle.Assets)
                    .Any(asset => asset.AssetPath == excluded.AssetPath || asset.AssetPath == deleted.AssetPath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void BuildSnapshot_WhenStoredPathAppears_DoesNotRebindUnresolvedGuid()
        {
            const string folderPath = "Assets/ResourceAuthoringServiceTests";
            const string assetPath = folderPath + "/Created.asset";
            EnsureFolder(folderPath);
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            try
            {
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "State", "state-assets");
                var entry = CreateEntry(assetPath);
                entry.Guid = UnresolvedGuid;
                package.Bundles[0].Entries.Add(entry);
                var registry = ResourceEditorRegistry.Scan();
                var missing = ResourceAuthoringService.BuildSnapshot(settings, registry);

                var asset = ScriptableObject.CreateInstance<ResourceEditorSettings>();
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.SaveAssets();
                var existing = ResourceAuthoringService.BuildSnapshot(settings, registry);

                Assert.AreEqual(missing.Revision, existing.Revision);
                Assert.IsTrue(missing.Issues.Any(issue =>
                    issue.Message == $"Configured resource GUID cannot be resolved: {UnresolvedGuid}"));
                Assert.IsTrue(existing.Issues.Any(issue =>
                    issue.Message == $"Configured resource GUID cannot be resolved: {UnresolvedGuid}"));
                Assert.IsFalse(existing.Manifest.Packages
                    .SelectMany(manifestPackage => manifestPackage.Bundles)
                    .SelectMany(bundle => bundle.Assets)
                    .Any(assetInfo => assetInfo.AssetPath == assetPath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                AssetDatabase.DeleteAsset(folderPath);
            }
        }

        [Test]
        public void BuildWorkflow_WhenPreflightFails_DoesNotChangeExistingOutput()
        {
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            var version = "preflight-" + Guid.NewGuid().ToString("N");
            var channel = "preflight-test";
            var outputRoot = Path.GetFullPath(Path.Combine(
                ResourceBuildSettings.OUTPUT_ROOT,
                channel,
                EditorUserBuildSettings.activeBuildTarget.ToString(),
                version));
            var sentinelPath = Path.Combine(outputRoot, "sentinel.txt");
            try
            {
                settings.EnsureDefaults();
                settings.BuildSettings.Channel = channel;
                settings.BuildSettings.ManifestVersion = version;
                settings.BuildSettings.CleanOutput = true;
                settings.BuildSettings.Scope = ResourceBuildScope.AllPackages;
                var package = CreatePackage(settings, "BuildGuard", "build-guard");
                var missingPath = "Assets/MissingBuildGuard.asset";
                var missingEntry = CreateEntry(missingPath);
                missingEntry.Guid = UnresolvedGuid;
                package.Bundles[0].Entries.Add(missingEntry);
                IODirectory.CreateDirectory(outputRoot);
                IOFile.WriteAllText(sentinelPath, "keep");
                var workflow = new ResourceBuildWorkflow(
                    settings,
                    ResourceEditorRegistry.Scan(),
                    settings.BuildSettings);

                var result = workflow.Build(out var plan);

                Assert.IsFalse(result.Succeeded);
                Assert.IsNull(plan);
                StringAssert.Contains($"Configured resource GUID cannot be resolved: {UnresolvedGuid}", result.ErrorMessage);
                Assert.AreEqual("keep", IOFile.ReadAllText(sentinelPath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                if (IODirectory.Exists(outputRoot))
                {
                    IODirectory.Delete(outputRoot, true);
                }
            }
        }

        [Test]
        public void EnsureDefaults_WhenBuildSettingsExplicit_PreservesConfiguredValues()
        {
            var settings = new ResourceBuildSettings
            {
                OutputRoot = "Temp/ExplicitResourceOutput",
                Target = BuildTarget.StandaloneWindows64.ToString(),
                Channel = "release",
                CleanOutput = false,
                Compression = ResourceBuildCompression.Uncompressed,
                ManifestFileName = "release-manifest.json",
                ManifestVersion = "2.3.4",
                Scope = ResourceBuildScope.HotUpdatePackages
            };

            settings.EnsureDefaults();

            Assert.AreEqual("Temp/ExplicitResourceOutput", settings.OutputRoot);
            Assert.AreEqual(BuildTarget.StandaloneWindows64.ToString(), settings.Target);
            Assert.AreEqual("release", settings.Channel);
            Assert.IsFalse(settings.CleanOutput);
            Assert.AreEqual(ResourceBuildCompression.Uncompressed, settings.Compression);
            Assert.AreEqual("release-manifest.json", settings.ManifestFileName);
            Assert.AreEqual("2.3.4", settings.ManifestVersion);
            Assert.AreEqual(ResourceBuildScope.HotUpdatePackages, settings.Scope);
        }

        [Test]
        public void Build_WhenTargetInvalid_FailsBeforeMutatingOutput()
        {
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            var outputRoot = Path.GetFullPath(Path.Combine(
                "Temp/ResourceBuildTargetPreflightTests",
                Guid.NewGuid().ToString("N")));
            var sentinelPath = Path.Combine(outputRoot, "sentinel.txt");
            try
            {
                settings.EnsureDefaults();
                settings.BuildSettings.OutputRoot = outputRoot;
                settings.BuildSettings.Target = "NotAUnityBuildTarget";
                settings.BuildSettings.Scope = ResourceBuildScope.AllPackages;
                IODirectory.CreateDirectory(outputRoot);
                IOFile.WriteAllText(sentinelPath, "keep");
                var workflow = new ResourceBuildWorkflow(
                    settings,
                    ResourceEditorRegistry.Scan(),
                    settings.BuildSettings);

                var result = workflow.Build(out var plan);

                Assert.IsFalse(result.Succeeded);
                Assert.IsNull(plan);
                StringAssert.Contains("Build target is invalid: NotAUnityBuildTarget", result.ErrorMessage);
                Assert.AreEqual("keep", IOFile.ReadAllText(sentinelPath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                if (IODirectory.Exists(outputRoot))
                {
                    IODirectory.Delete(outputRoot, true);
                }
            }
        }

        [Test]
        public void Build_WhenRequestCustomized_UsesRequestAndPreservesExistingOutput()
        {
            const string assetFolder = "Assets/ResourceBuildRequestTests";
            const string assetPath = assetFolder + "/Payload.asset";
            ResourceEditorSettings settings = null;
            var outputRoot = Path.Combine(
                "Temp/ResourceBuildRequestTests",
                Guid.NewGuid().ToString("N")).Replace('\\', '/');
            var channel = "request-" + Guid.NewGuid().ToString("N");
            var additionalChannel = "request-secondary-" + Guid.NewGuid().ToString("N");
            var version = "2.4.6-" + Guid.NewGuid().ToString("N");
            const string manifestFileName = "request-manifest.json";
            var target = EditorUserBuildSettings.activeBuildTarget;
            var versionRoot = Path.GetFullPath(Path.Combine(outputRoot, channel, target.ToString(), version));
            var additionalVersionRoot = Path.GetFullPath(Path.Combine(outputRoot, additionalChannel, target.ToString(), version));
            var sentinelPath = Path.Combine(versionRoot, "sentinel.txt");
            var additionalSentinelPath = Path.Combine(additionalVersionRoot, "sentinel.txt");
            try
            {
                EnsureFolder(assetFolder);
                AssetDatabase.CreateAsset(new TextAsset("resource build request"), assetPath);
                AssetDatabase.SaveAssets();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "RequestHot", "request-assets");
                package.IsHotUpdate = true;
                var entry = CreateEntry(assetPath);
                entry.TypeName = nameof(TextAsset);
                package.Bundles[0].Entries.Add(entry);
                settings.BuildSettings.OutputRoot = outputRoot;
                settings.BuildSettings.Target = target.ToString();
                settings.BuildSettings.Channel = $"{channel},{additionalChannel}";
                settings.BuildSettings.ManifestVersion = version;
                settings.BuildSettings.ManifestFileName = manifestFileName;
                settings.BuildSettings.CleanOutput = false;
                settings.BuildSettings.Compression = ResourceBuildCompression.Uncompressed;
                settings.BuildSettings.Scope = ResourceBuildScope.HotUpdatePackages;
                IODirectory.CreateDirectory(versionRoot);
                IODirectory.CreateDirectory(additionalVersionRoot);
                IOFile.WriteAllText(sentinelPath, "keep");
                IOFile.WriteAllText(additionalSentinelPath, "keep-secondary");
                var workflow = new ResourceBuildWorkflow(
                    settings,
                    ResourceEditorRegistry.Scan(),
                    settings.BuildSettings);

                var result = workflow.Build(out var plan);

                Assert.IsTrue(result.Succeeded, result.ErrorMessage);
                Assert.IsNotNull(plan);
                Assert.AreEqual(
                    versionRoot.Replace('\\', '/'),
                    result.OutputRoot.Replace('\\', '/'));
                Assert.AreEqual(
                    Path.Combine(versionRoot, manifestFileName).Replace('\\', '/'),
                    result.ManifestPath.Replace('\\', '/'));
                Assert.IsTrue(IOFile.Exists(result.ManifestPath));
                Assert.IsFalse(IOFile.Exists(Path.Combine(versionRoot, ResourceSettings.MANIFEST_NAME)));
                Assert.AreEqual("keep", IOFile.ReadAllText(sentinelPath));
                Assert.IsTrue(IOFile.Exists(Path.Combine(additionalVersionRoot, manifestFileName)));
                Assert.AreEqual("keep-secondary", IOFile.ReadAllText(additionalSentinelPath));
                Assert.IsTrue(result.Artifacts.Any(artifact =>
                    artifact.BundleName == manifestFileName &&
                    artifact.RemoteKey == $"{channel}/{target}/{version}/{manifestFileName}"));
                Assert.IsTrue(result.Artifacts.Any(artifact =>
                    artifact.BundleName == manifestFileName &&
                    artifact.RemoteKey == $"{additionalChannel}/{target}/{version}/{manifestFileName}"));
            }
            finally
            {
                if (settings != null)
                {
                    UnityEngine.Object.DestroyImmediate(settings);
                }
                AssetDatabase.DeleteAsset(assetFolder);
                if (IODirectory.Exists(Path.GetFullPath(outputRoot)))
                {
                    IODirectory.Delete(Path.GetFullPath(outputRoot), true);
                }
            }
        }

        [Test]
        public void BuildEditorSimulatorManifest_WhenPreflightFails_DoesNotOverwriteManifest()
        {
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            var manifestPath = Path.GetFullPath("Temp/ResourceAuthoringServiceTests/manifest.json");
            try
            {
                settings.EnsureDefaults();
                settings.ManifestOutputPath = "Temp/ResourceAuthoringServiceTests/manifest.json";
                var package = CreatePackage(settings, "SimulatorGuard", "simulator-guard");
                var missingPath = "Assets/MissingSimulatorGuard.asset";
                var missingEntry = CreateEntry(missingPath);
                missingEntry.Guid = UnresolvedGuid;
                package.Bundles[0].Entries.Add(missingEntry);
                IODirectory.CreateDirectory(Path.GetDirectoryName(manifestPath) ?? ".");
                IOFile.WriteAllText(manifestPath, "sentinel");
                var writeTime = IOFile.GetLastWriteTimeUtc(manifestPath);

                var exception = Assert.Throws<GameException>(() =>
                    ResourceEditorPlayModeManifestProvider.BuildEditorSimulatorManifest(
                        settings,
                        ResourceEditorRegistry.Scan()));

                StringAssert.Contains($"Configured resource GUID cannot be resolved: {UnresolvedGuid}", exception.Message);
                Assert.AreEqual("sentinel", IOFile.ReadAllText(manifestPath));
                Assert.AreEqual(writeTime, IOFile.GetLastWriteTimeUtc(manifestPath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                if (IOFile.Exists(manifestPath))
                {
                    IOFile.Delete(manifestPath);
                }
            }
        }

        [Test]
        public void BuildSnapshot_WhenActiveAssetExists_IncludesAssetWithoutMissingError()
        {
            var assetPath = GameDeveloperKitEditorPaths.PackageAssetPath("Tests/Editor/Fixtures/Loading.prefab");
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            try
            {
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "Existing", "existing-assets");
                package.Bundles[0].Entries.Add(CreateEntry(assetPath));

                var snapshot = ResourceAuthoringService.BuildSnapshot(settings, ResourceEditorRegistry.Scan());

                Assert.IsTrue(snapshot.Manifest.Packages
                    .SelectMany(manifestPackage => manifestPackage.Bundles)
                    .SelectMany(manifestBundle => manifestBundle.Assets)
                    .Any(asset => asset.AssetPath == assetPath));
                Assert.IsFalse(snapshot.Issues.Any(issue =>
                    issue.Source == ResourceAuthoringService.IssueSource &&
                    issue.Resource?.AssetPath == assetPath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Preflight_WhenOnlyWarningsExist_DoesNotBlockWorkflowOrSimulator()
        {
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            var manifestPath = Path.GetFullPath("Temp/ResourceAuthoringServiceTests/warning-manifest.json");
            try
            {
                settings.EnsureDefaults();
                settings.ManifestOutputPath = "Temp/ResourceAuthoringServiceTests/warning-manifest.json";
                var registry = ResourceEditorRegistry.Scan();
                var snapshot = ResourceAuthoringService.BuildSnapshot(settings, registry);
                Assert.IsTrue(snapshot.Issues.Any(issue => issue.Severity == ResourceValidationSeverity.Warning));
                Assert.IsFalse(snapshot.Issues.Any(issue => issue.Severity == ResourceValidationSeverity.Error));

                var workflow = new ResourceBuildWorkflow(settings, registry, settings.BuildSettings);
                var plan = workflow.CreatePlan(out var error);
                var manifest = ResourceEditorPlayModeManifestProvider.BuildEditorSimulatorManifest(settings, registry);

                Assert.IsNull(error);
                Assert.IsNotNull(plan);
                Assert.IsNotNull(manifest);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                if (IOFile.Exists(manifestPath))
                {
                    IOFile.Delete(manifestPath);
                }
            }
        }

        [Test]
        public void PreflightIntegration_SourceGuardsKeepSingleReadOnlyBoundary()
        {
            var serviceSource = IOFile.ReadAllText(
                "Assets/GameDeveloperKit/Editor/ResourceEditor/Authoring/ResourceAuthoringService.cs");
            var windowSource = IOFile.ReadAllText(
                "Assets/GameDeveloperKit/Editor/ResourceEditor/ResourceEditorWindow.cs");
            var applicationSource = IOFile.ReadAllText(
                "Assets/GameDeveloperKit/Editor/ResourceEditor/ResourceEditorApplicationService.cs");
            var simulatorSource = IOFile.ReadAllText(
                "Assets/GameDeveloperKit/Editor/ResourceEditor/ResourceEditorPlayModeManifestProvider.cs");
            var workflowSource = IOFile.ReadAllText(
                "Assets/GameDeveloperKit/Editor/ResourceEditor/Build/ResourceBuildWorkflow.cs");
            var executorSource = IOFile.ReadAllText(
                "Assets/GameDeveloperKit/Editor/ResourceEditor/Build/ResourceBuildExecutor.cs");

            StringAssert.DoesNotContain("LoadOrCreate(", serviceSource);
            StringAssert.DoesNotContain("SaveSettings(", serviceSource);
            StringAssert.DoesNotContain("WriteManifest(", serviceSource);
            StringAssert.DoesNotContain("ImportAsset(", serviceSource);
            StringAssert.DoesNotContain("ContentPipeline", serviceSource);
            StringAssert.DoesNotContain("Directory.Delete", serviceSource);
            StringAssert.Contains("m_Application.Refresh()", windowSource);
            StringAssert.Contains("m_Application.Build(scope)", windowSource);
            StringAssert.DoesNotContain("ResourceAuthoringService.BuildSnapshot", windowSource);
            StringAssert.DoesNotContain("new ResourceBuildWorkflow", windowSource);
            StringAssert.DoesNotContain("checker.Instance.Check(context, m_Issues)", windowSource);
            StringAssert.Contains("ResourceAuthoringService.BuildSnapshot(m_Settings, m_Registry)", applicationSource);
            StringAssert.Contains("new ResourceBuildWorkflow", applicationSource);
            StringAssert.Contains("ResourceAuthoringService.BuildSnapshot(settings, registry)", simulatorSource);
            StringAssert.DoesNotContain("private static List<ResourceValidationIssue> CheckManifest", simulatorSource);
            StringAssert.DoesNotContain("WriteLocalBaseManifest", simulatorSource);
            StringAssert.DoesNotContain("ResourceManifestPartitioner", simulatorSource);
            StringAssert.Contains("ResourceAuthoringService.BuildSnapshot(m_Settings, m_Registry)", workflowSource);
            StringAssert.DoesNotContain("EditorUserBuildSettings.activeBuildTarget", executorSource);
            StringAssert.DoesNotContain("ResourceBuildSettings.OUTPUT_ROOT", executorSource);
            StringAssert.Contains("context.BuildSettings.ManifestFileName", executorSource);
            StringAssert.Contains("ResourceBuildOutputTransaction.Begin()", executorSource);
            StringAssert.DoesNotContain("Directory.Delete(versionRoot", executorSource);
        }

        [Test]
        public void ApplicationRefresh_UsesSnapshotIssuesAndPreviews()
        {
            var settings = ScriptableObject.CreateInstance<ResourceEditorSettings>();
            try
            {
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "Application", "application-assets");
                var assetPath = GameDeveloperKitEditorPaths.PackageAssetPath("Tests/Editor/Fixtures/Loading.prefab");
                package.Bundles[0].Entries.Add(CreateEntry(assetPath));
                var registry = ResourceEditorRegistry.Scan();
                var expected = ResourceAuthoringService.BuildSnapshot(settings, registry);

                var state = new ResourceEditorApplicationService(settings, registry).Refresh();

                CollectionAssert.AreEqual(
                    expected.Issues.Select(FormatIssue),
                    state.Issues.Select(FormatIssue));
                CollectionAssert.AreEqual(
                    expected.Previews[package.Bundles[0]].Select(DescribePreview),
                    state.GetPreview(package.Bundles[0]).Select(DescribePreview));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        private static ResourceEditorPackage CreatePackage(
            ResourceEditorSettings settings,
            string packageName,
            string bundleName)
        {
            var package = new ResourceEditorPackage
            {
                Name = packageName,
                BuildStrategyId = "single-bundle",
                CollectorId = "explicit-assets"
            };
            package.EnsureDefaults();
            var bundle = new ResourceEditorBundle
            {
                Name = bundleName,
                Group = "Default",
                ProviderId = ResourceProviderIds.AssetBundle,
                CollectorId = "explicit-assets"
            };
            bundle.EnsureDefaults();
            package.Bundles.Add(bundle);
            settings.Packages.Add(package);
            return package;
        }

        private static ResourceEditorAssetEntry CreateEntry(string assetPath)
        {
            var entry = new ResourceEditorAssetEntry
            {
                Guid = AssetDatabase.AssetPathToGUID(assetPath),
                AssetPath = assetPath,
                Location = assetPath,
                TypeName = nameof(ResourceEditorSettings),
                ProviderId = ResourceProviderIds.AssetBundle
            };
            entry.EnsureDefaults(ResourceProviderIds.AssetBundle);
            return entry;
        }

        private static string DescribePreview(ResourceGroupPreview preview)
        {
            return $"{preview.AssetPath}|{preview.Location}|{preview.TypeName}|{string.Join(",", preview.Labels)}|{preview.BundleName}|{preview.Group}";
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException($"Invalid folder path: {path}");
            }

            AssetDatabase.CreateFolder(parent, name);
        }

        private static string FormatIssue(ResourceValidationIssue issue)
        {
            return $"{issue.Severity}|{issue.Source}|{issue.Message}|{issue.Package?.Name}|{issue.Bundle?.Name}|{issue.Resource?.AssetPath}";
        }
    }
}
