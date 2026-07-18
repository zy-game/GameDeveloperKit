using System;
using System.IO;
using System.Linq;
using GameDeveloperKit.Resource;
using GameDeveloperKit.ResourceEditor;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
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
            var bundle = new GameDeveloperKit.ResourceEditor.Authoring.Bundle
            {
                Name = "identity-assets",
                ProviderId = ResourceProviderIds.AssetBundle,
                CollectorId = "explicit-assets"
            };
            bundle.EnsureDefaults();

            var changed = GameDeveloperKit.ResourceEditor.Authoring.EntryTable.AddEntry(bundle, assetPath);

            Assert.IsTrue(changed);
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(assetPath), bundle.Entries[0].Guid);
            Assert.AreEqual(assetPath, AssetDatabase.GUIDToAssetPath(bundle.Entries[0].Guid));
        }

        [Test]
        public void EnsureDefaults_WhenEntryHasNoGuid_PreservesEntryForPreflight()
        {
            var bundle = new GameDeveloperKit.ResourceEditor.Authoring.Bundle
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
            var asset = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            AssetDatabase.CreateAsset(asset, originalPath);
            AssetDatabase.SaveAssets();
            var guid = AssetDatabase.AssetPathToGUID(originalPath);
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "Move", "move-assets");
                var entry = CreateEntry(originalPath);
                entry.Guid = guid;
                package.Bundles[0].Entries.Add(entry);
                var registry = GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan();
                var before = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, registry);

                Assert.AreEqual(string.Empty, AssetDatabase.MoveAsset(originalPath, movedPath));
                var after = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, registry);
                settings.BuildSettings.Scope = GameDeveloperKit.ResourceEditor.Build.Scope.AllPackages;
                var workflow = new GameDeveloperKit.ResourceEditor.Build.Workflow(settings, registry, settings.BuildSettings);
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
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
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

                var snapshot = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan());

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
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
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

                var snapshot = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan());

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
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>(), originalPath);
            AssetDatabase.SaveAssets();
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "Explicit", "explicit-assets-test");
                var entry = CreateEntry(originalPath);
                entry.Location = "stable/address";
                package.Bundles[0].Entries.Add(entry);

                Assert.AreEqual(string.Empty, AssetDatabase.MoveAsset(originalPath, movedPath));
                GameDeveloperKit.ResourceEditor.Authoring.Service.Reconcile(
                    settings,
                    GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan(),
                    new GameDeveloperKit.ResourceEditor.Authoring.AssetChangeSet(
                        movedAssets: new[] { new GameDeveloperKit.ResourceEditor.Authoring.AssetMove(originalPath, movedPath) }));

                Assert.AreEqual(movedPath, entry.AssetPath);
                Assert.AreEqual("stable/address", entry.Location);

                AssetDatabase.DeleteAsset(movedPath);
                GameDeveloperKit.ResourceEditor.Authoring.Service.Reconcile(
                    settings,
                    GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan(),
                    new GameDeveloperKit.ResourceEditor.Authoring.AssetChangeSet(deletedAssets: new[] { movedPath }));

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
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "Explicit", "explicit-full-scan-test");
                var entry = CreateEntry("Assets/Missing/Deleted.asset");
                entry.Guid = UnresolvedGuid;
                package.Bundles[0].Entries.Add(entry);

                GameDeveloperKit.ResourceEditor.Authoring.Service.Reconcile(
                    settings,
                    GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan(),
                    new GameDeveloperKit.ResourceEditor.Authoring.AssetChangeSet(fullReconcile: true));

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
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                settings.EnsureDefaults();
                var package = new GameDeveloperKit.ResourceEditor.Authoring.Package
                {
                    Name = "Folder",
                    BuildStrategyId = "single-bundle"
                };
                package.EnsureDefaults();
                var bundle = new GameDeveloperKit.ResourceEditor.Authoring.Bundle
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

                GameDeveloperKit.ResourceEditor.Authoring.Service.Reconcile(
                    settings,
                    GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan(),
                    new GameDeveloperKit.ResourceEditor.Authoring.AssetChangeSet(fullReconcile: true));

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
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>(), originalPath);
            AssetDatabase.SaveAssets();
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                settings.EnsureDefaults();
                var package = new GameDeveloperKit.ResourceEditor.Authoring.Package
                {
                    Name = "Folder",
                    BuildStrategyId = "single-bundle"
                };
                package.EnsureDefaults();
                var bundle = new GameDeveloperKit.ResourceEditor.Authoring.Bundle
                {
                    Name = "folder-assets-test",
                    ProviderId = ResourceProviderIds.AssetBundle,
                    CollectorId = "folder-assets",
                    SourceFolder = sourcePath
                };
                bundle.EnsureDefaults();
                package.Bundles.Add(bundle);
                settings.Packages.Add(package);

                GameDeveloperKit.ResourceEditor.Authoring.Service.Reconcile(
                    settings,
                    GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan(),
                    new GameDeveloperKit.ResourceEditor.Authoring.AssetChangeSet(importedAssets: new[] { originalPath }));
                var guid = AssetDatabase.AssetPathToGUID(originalPath);
                Assert.IsTrue(bundle.Entries.Any(entry => entry.Guid == guid));

                Assert.AreEqual(string.Empty, AssetDatabase.MoveAsset(originalPath, movedPath));
                GameDeveloperKit.ResourceEditor.Authoring.Service.Reconcile(
                    settings,
                    GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan(),
                    new GameDeveloperKit.ResourceEditor.Authoring.AssetChangeSet(
                        movedAssets: new[] { new GameDeveloperKit.ResourceEditor.Authoring.AssetMove(originalPath, movedPath) }));

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
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>(), originalPath);
            AssetDatabase.SaveAssets();
            var guid = AssetDatabase.AssetPathToGUID(originalPath);
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                settings.EnsureDefaults();
                var resourcesBundle = settings.Packages
                    .First(GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsBuiltinPackage)
                    .Bundles.First(GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsResourcesGroup);
                GameDeveloperKit.ResourceEditor.Authoring.Service.Reconcile(
                    settings,
                    GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan(),
                    new GameDeveloperKit.ResourceEditor.Authoring.AssetChangeSet(importedAssets: new[] { originalPath }));
                var entry = resourcesBundle.Entries.Single(candidate => candidate.Guid == guid);

                Assert.AreEqual(string.Empty, AssetDatabase.MoveAsset(originalPath, movedPath));
                GameDeveloperKit.ResourceEditor.Authoring.Service.Reconcile(
                    settings,
                    GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan(),
                    new GameDeveloperKit.ResourceEditor.Authoring.AssetChangeSet(
                        movedAssets: new[] { new GameDeveloperKit.ResourceEditor.Authoring.AssetMove(originalPath, movedPath) }));

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
            var rootPath = Path.GetFullPath("Temp/GameDeveloperKit.ResourceEditor.Authoring.SnapshotStoreSuccess");
            var manifestPath = Path.Combine(rootPath, "manifest.json");
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "Store", "store-assets");
                var entry = CreateEntry(assetPath);
                package.Bundles[0].Entries.Add(entry);
                var plan = GameDeveloperKit.ResourceEditor.Authoring.MutationPlan.Capture(settings);
                entry.Location = "changed/location";
                var snapshot = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan());
                var saveCount = 0;

                GameDeveloperKit.ResourceEditor.Authoring.SnapshotStore.Commit(
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
            var rootPath = Path.GetFullPath("Temp/GameDeveloperKit.ResourceEditor.Authoring.SnapshotStoreSaveFailure");
            var manifestPath = Path.Combine(rootPath, "manifest.json");
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                IODirectory.CreateDirectory(rootPath);
                IOFile.WriteAllText(manifestPath, "sentinel");
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "Rollback", "rollback-assets");
                var entry = CreateEntry(assetPath);
                entry.Location = "before/location";
                package.Bundles[0].Entries.Add(entry);
                var plan = GameDeveloperKit.ResourceEditor.Authoring.MutationPlan.Capture(settings);
                entry.Location = "after/location";
                var snapshot = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan());

                Assert.Throws<InvalidOperationException>(() =>
                    GameDeveloperKit.ResourceEditor.Authoring.SnapshotStore.Commit(
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
            var rootPath = Path.GetFullPath("Temp/GameDeveloperKit.ResourceEditor.Authoring.SnapshotStoreTempFailure");
            var blockerPath = Path.Combine(rootPath, "blocker");
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                IODirectory.CreateDirectory(rootPath);
                IOFile.WriteAllText(blockerPath, "blocker");
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "TempFailure", "temp-failure-assets");
                var entry = CreateEntry(assetPath);
                entry.Location = "before/location";
                package.Bundles[0].Entries.Add(entry);
                var plan = GameDeveloperKit.ResourceEditor.Authoring.MutationPlan.Capture(settings);
                entry.Location = "after/location";
                var snapshot = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan());
                var saveCount = 0;

                Assert.Throws<IOException>(() =>
                    GameDeveloperKit.ResourceEditor.Authoring.SnapshotStore.Commit(
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
                "Assets/GameDeveloperKit/Editor/ResourceEditor/Authoring/AssetWatcher.cs");
            var reconciliationSource = IOFile.ReadAllText(
                "Assets/GameDeveloperKit/Editor/ResourceEditor/Authoring/Reconciliation.cs");
            var snapshotStoreSource = IOFile.ReadAllText(
                "Assets/GameDeveloperKit/Editor/ResourceEditor/Authoring/SnapshotStore.cs");
            var validatorSource = IOFile.ReadAllText(
                "Assets/GameDeveloperKit/Editor/ResourceEditor/Authoring/AssetValidator.cs");

            StringAssert.Contains("EditorApplication.delayCall += Drain", watcherSource);
            StringAssert.Contains("Service.Reconcile(changes)", watcherSource);
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
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                settings.EnsureDefaults();
                var registry = GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan();

                var first = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, registry);
                var second = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, registry);

                Assert.AreEqual(first.Revision, second.Revision);
                Assert.AreEqual(
                    JsonConvert.SerializeObject(first.Manifest),
                    JsonConvert.SerializeObject(second.Manifest));
                Assert.IsFalse(first.Issues.Any(issue => issue.Severity == GameDeveloperKit.ResourceEditor.Validation.Severity.Error));
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
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                settings.EnsureDefaults();
                var package = settings.Packages.First();
                var bundle = package.Bundles.First();
                var registry = GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan();
                var baseline = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, registry);

                bundle.Group = string.Empty;
                var first = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, registry);
                var second = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, registry);

                Assert.AreNotEqual(baseline.Revision, first.Revision);
                Assert.AreEqual(first.Revision, second.Revision);
                Assert.IsTrue(first.Issues.Any(issue =>
                    issue.Source == nameof(GameDeveloperKit.ResourceEditor.Validation.BasicChecker) &&
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
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                settings.EnsureDefaults();
                var package = settings.Packages.First();
                package.BuildStrategyId = "missing-strategy";

                var snapshot = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan());

                Assert.IsTrue(snapshot.Issues.Any(issue =>
                    issue.Severity == GameDeveloperKit.ResourceEditor.Validation.Severity.Error &&
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
            const string folderPath = "Assets/GameDeveloperKit.ResourceEditor.Authoring.ServiceTests";
            EnsureFolder(folderPath);
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
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

                var snapshot = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan());
                var authoringErrors = snapshot.Issues
                    .Where(issue => issue.Source == GameDeveloperKit.ResourceEditor.Authoring.Service.IssueSource)
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
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "Excluded", "excluded-assets");
                var bundle = package.Bundles[0];
                var excluded = CreateEntry("Assets/MissingExcluded.asset");
                excluded.ExcludeKind = GameDeveloperKit.ResourceEditor.Authoring.EntryExcludeKind.Excluded;
                var deleted = CreateEntry("Assets/MissingDeleted.asset");
                deleted.ExcludeKind = GameDeveloperKit.ResourceEditor.Authoring.EntryExcludeKind.Deleted;
                bundle.Entries.Add(excluded);
                bundle.Entries.Add(deleted);

                var snapshot = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan());

                Assert.IsFalse(snapshot.Issues.Any(issue =>
                    issue.Source == GameDeveloperKit.ResourceEditor.Authoring.Service.IssueSource &&
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
            const string folderPath = "Assets/GameDeveloperKit.ResourceEditor.Authoring.ServiceTests";
            const string assetPath = folderPath + "/Created.asset";
            EnsureFolder(folderPath);
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "State", "state-assets");
                var entry = CreateEntry(assetPath);
                entry.Guid = UnresolvedGuid;
                package.Bundles[0].Entries.Add(entry);
                var registry = GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan();
                var missing = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, registry);

                var asset = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.SaveAssets();
                var existing = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, registry);

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
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            var version = "preflight-" + Guid.NewGuid().ToString("N");
            var channel = "preflight-test";
            var outputRoot = Path.GetFullPath(Path.Combine(
                GameDeveloperKit.ResourceEditor.Build.Settings.OUTPUT_ROOT,
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
                settings.BuildSettings.Scope = GameDeveloperKit.ResourceEditor.Build.Scope.AllPackages;
                var package = CreatePackage(settings, "BuildGuard", "build-guard");
                var missingPath = "Assets/MissingBuildGuard.asset";
                var missingEntry = CreateEntry(missingPath);
                missingEntry.Guid = UnresolvedGuid;
                package.Bundles[0].Entries.Add(missingEntry);
                IODirectory.CreateDirectory(outputRoot);
                IOFile.WriteAllText(sentinelPath, "keep");
                var workflow = new GameDeveloperKit.ResourceEditor.Build.Workflow(
                    settings,
                    GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan(),
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
            var settings = new GameDeveloperKit.ResourceEditor.Build.Settings
            {
                OutputRoot = "Temp/ExplicitResourceOutput",
                Target = BuildTarget.StandaloneWindows64.ToString(),
                Channel = "release",
                CleanOutput = false,
                Compression = GameDeveloperKit.ResourceEditor.Build.Compression.Uncompressed,
                ManifestFileName = "release-manifest.json",
                ManifestVersion = "2.3.4",
                Scope = GameDeveloperKit.ResourceEditor.Build.Scope.HotUpdatePackages
            };

            settings.EnsureDefaults();

            Assert.AreEqual("Temp/ExplicitResourceOutput", settings.OutputRoot);
            Assert.AreEqual(BuildTarget.StandaloneWindows64.ToString(), settings.Target);
            Assert.AreEqual("release", settings.Channel);
            Assert.IsFalse(settings.CleanOutput);
            Assert.AreEqual(GameDeveloperKit.ResourceEditor.Build.Compression.Uncompressed, settings.Compression);
            Assert.AreEqual("release-manifest.json", settings.ManifestFileName);
            Assert.AreEqual("2.3.4", settings.ManifestVersion);
            Assert.AreEqual(GameDeveloperKit.ResourceEditor.Build.Scope.HotUpdatePackages, settings.Scope);
        }

        [Test]
        public void Build_WhenTargetInvalid_FailsBeforeMutatingOutput()
        {
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            var outputRoot = Path.GetFullPath(Path.Combine(
                "Temp/ResourceBuildTargetPreflightTests",
                Guid.NewGuid().ToString("N")));
            var sentinelPath = Path.Combine(outputRoot, "sentinel.txt");
            try
            {
                settings.EnsureDefaults();
                settings.BuildSettings.OutputRoot = outputRoot;
                settings.BuildSettings.Target = "NotAUnityBuildTarget";
                settings.BuildSettings.Scope = GameDeveloperKit.ResourceEditor.Build.Scope.AllPackages;
                IODirectory.CreateDirectory(outputRoot);
                IOFile.WriteAllText(sentinelPath, "keep");
                var workflow = new GameDeveloperKit.ResourceEditor.Build.Workflow(
                    settings,
                    GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan(),
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
            GameDeveloperKit.ResourceEditor.Authoring.Settings settings = null;
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

                settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
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
                settings.BuildSettings.Compression = GameDeveloperKit.ResourceEditor.Build.Compression.Uncompressed;
                settings.BuildSettings.Scope = GameDeveloperKit.ResourceEditor.Build.Scope.HotUpdatePackages;
                IODirectory.CreateDirectory(versionRoot);
                IODirectory.CreateDirectory(additionalVersionRoot);
                IOFile.WriteAllText(sentinelPath, "keep");
                IOFile.WriteAllText(additionalSentinelPath, "keep-secondary");
                var workflow = new GameDeveloperKit.ResourceEditor.Build.Workflow(
                    settings,
                    GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan(),
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
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            var manifestPath = Path.GetFullPath("Temp/GameDeveloperKit.ResourceEditor.Authoring.ServiceTests/manifest.json");
            try
            {
                settings.EnsureDefaults();
                settings.ManifestOutputPath = "Temp/GameDeveloperKit.ResourceEditor.Authoring.ServiceTests/manifest.json";
                var package = CreatePackage(settings, "SimulatorGuard", "simulator-guard");
                var missingPath = "Assets/MissingSimulatorGuard.asset";
                var missingEntry = CreateEntry(missingPath);
                missingEntry.Guid = UnresolvedGuid;
                package.Bundles[0].Entries.Add(missingEntry);
                IODirectory.CreateDirectory(Path.GetDirectoryName(manifestPath) ?? ".");
                IOFile.WriteAllText(manifestPath, "sentinel");
                var writeTime = IOFile.GetLastWriteTimeUtc(manifestPath);

                var exception = Assert.Throws<GameException>(() =>
                    GameDeveloperKit.ResourceEditor.Build.PlayModeManifestProvider.BuildEditorSimulatorManifest(
                        settings,
                        GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan()));

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
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "Existing", "existing-assets");
                package.Bundles[0].Entries.Add(CreateEntry(assetPath));

                var snapshot = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan());

                Assert.IsTrue(snapshot.Manifest.Packages
                    .SelectMany(manifestPackage => manifestPackage.Bundles)
                    .SelectMany(manifestBundle => manifestBundle.Assets)
                    .Any(asset => asset.AssetPath == assetPath));
                Assert.IsFalse(snapshot.Issues.Any(issue =>
                    issue.Source == GameDeveloperKit.ResourceEditor.Authoring.Service.IssueSource &&
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
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            var manifestPath = Path.GetFullPath("Temp/GameDeveloperKit.ResourceEditor.Authoring.ServiceTests/warning-manifest.json");
            try
            {
                settings.EnsureDefaults();
                settings.ManifestOutputPath = "Temp/GameDeveloperKit.ResourceEditor.Authoring.ServiceTests/warning-manifest.json";
                var registry = GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan();
                var snapshot = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, registry);
                Assert.IsTrue(snapshot.Issues.Any(issue => issue.Severity == GameDeveloperKit.ResourceEditor.Validation.Severity.Warning));
                Assert.IsFalse(snapshot.Issues.Any(issue => issue.Severity == GameDeveloperKit.ResourceEditor.Validation.Severity.Error));

                var workflow = new GameDeveloperKit.ResourceEditor.Build.Workflow(settings, registry, settings.BuildSettings);
                var plan = workflow.CreatePlan(out var error);
                var manifest = GameDeveloperKit.ResourceEditor.Build.PlayModeManifestProvider.BuildEditorSimulatorManifest(settings, registry);

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
                "Assets/GameDeveloperKit/Editor/ResourceEditor/Authoring/Service.cs");
            var windowSource = IOFile.ReadAllText(
                "Assets/GameDeveloperKit/Editor/ResourceEditor/UI/MainWindow.cs");
            var applicationSource = IOFile.ReadAllText(
                "Assets/GameDeveloperKit/Editor/ResourceEditor/UI/ApplicationService.cs");
            var simulatorSource = IOFile.ReadAllText(
                "Assets/GameDeveloperKit/Editor/ResourceEditor/Build/PlayModeManifestProvider.cs");
            var workflowSource = IOFile.ReadAllText(
                "Assets/GameDeveloperKit/Editor/ResourceEditor/Build/Workflow.cs");
            var executorSource = IOFile.ReadAllText(
                "Assets/GameDeveloperKit/Editor/ResourceEditor/Build/Executor.cs");

            StringAssert.DoesNotContain("LoadOrCreate(", serviceSource);
            StringAssert.DoesNotContain("SaveSettings(", serviceSource);
            StringAssert.DoesNotContain("WriteManifest(", serviceSource);
            StringAssert.DoesNotContain("ImportAsset(", serviceSource);
            StringAssert.DoesNotContain("ContentPipeline", serviceSource);
            StringAssert.DoesNotContain("Directory.Delete", serviceSource);
            StringAssert.Contains("m_Application.Refresh()", windowSource);
            StringAssert.Contains("m_Application.Build(scope)", windowSource);
            StringAssert.DoesNotContain("GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot", windowSource);
            StringAssert.DoesNotContain("new GameDeveloperKit.ResourceEditor.Build.Workflow", windowSource);
            StringAssert.DoesNotContain("checker.Instance.Check(context, m_Issues)", windowSource);
            StringAssert.Contains("GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(m_Settings, m_Registry)", applicationSource);
            StringAssert.Contains("new GameDeveloperKit.ResourceEditor.Build.Workflow", applicationSource);
            StringAssert.Contains("GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, registry)", simulatorSource);
            StringAssert.DoesNotContain("private static List<GameDeveloperKit.ResourceEditor.Validation.Issue> CheckManifest", simulatorSource);
            StringAssert.DoesNotContain("WriteLocalBaseManifest", simulatorSource);
            StringAssert.DoesNotContain("GameDeveloperKit.ResourceEditor.Build.ManifestPartitioner", simulatorSource);
            StringAssert.Contains("GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(m_Settings, m_Registry)", workflowSource);
            StringAssert.DoesNotContain("EditorUserBuildSettings.activeBuildTarget", executorSource);
            StringAssert.DoesNotContain("GameDeveloperKit.ResourceEditor.Build.Settings.OUTPUT_ROOT", executorSource);
            StringAssert.Contains("context.BuildSettings.ManifestFileName", executorSource);
            StringAssert.Contains("OutputTransaction.Begin()", executorSource);
            StringAssert.DoesNotContain("Directory.Delete(versionRoot", executorSource);
        }

        [Test]
        public void ApplicationRefresh_UsesSnapshotIssuesAndPreviews()
        {
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                settings.EnsureDefaults();
                var package = CreatePackage(settings, "Application", "application-assets");
                var assetPath = GameDeveloperKitEditorPaths.PackageAssetPath("Tests/Editor/Fixtures/Loading.prefab");
                package.Bundles[0].Entries.Add(CreateEntry(assetPath));
                var registry = GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan();
                var expected = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(settings, registry);

                var state = new GameDeveloperKit.ResourceEditor.UI.ApplicationService(settings, registry).Refresh();

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

        [Test]
        public void ExplicitMembership_WhenFullReconciledAndSerialized_RemainsAfterReload()
        {
            var assetPath = GameDeveloperKitEditorPaths.PackageAssetPath("Tests/Editor/Fixtures/Loading.prefab");
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            GameDeveloperKit.ResourceEditor.Authoring.Settings loaded = null;
            var serializedPath = Path.Combine("Library", "GameDeveloperKit", "Tests", "explicit-membership.asset");
            try
            {
                settings.EnsureDefaults();
                var localPackage = settings.Packages.First(GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsLocalPackage);
                var localGroup = localPackage.Bundles[0];
                Assert.AreEqual(GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.ExplicitCollectorId, localGroup.CollectorId);
                Assert.IsTrue(GameDeveloperKit.ResourceEditor.Authoring.EntryTable.AddEntry(localGroup, assetPath));

                GameDeveloperKit.ResourceEditor.Authoring.Service.Reconcile(
                    settings,
                    GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan(),
                    new GameDeveloperKit.ResourceEditor.Authoring.AssetChangeSet(fullReconcile: true));
                Assert.IsTrue(localGroup.Entries.Any(entry => entry.AssetPath == assetPath));

                IODirectory.CreateDirectory(Path.GetDirectoryName(serializedPath) ?? "Library");
                InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { settings }, serializedPath, true);
                loaded = InternalEditorUtility.LoadSerializedFileAndForget(serializedPath)
                    .OfType<GameDeveloperKit.ResourceEditor.Authoring.Settings>()
                    .Single();
                loaded.EnsureDefaults();

                var reloadedLocal = loaded.Packages.First(GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsLocalPackage);
                Assert.IsTrue(reloadedLocal.Bundles[0].Entries.Any(entry => entry.AssetPath == assetPath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                if (loaded != null)
                {
                    UnityEngine.Object.DestroyImmediate(loaded);
                }

                if (IOFile.Exists(serializedPath))
                {
                    IOFile.Delete(serializedPath);
                }
            }
        }

        [Test]
        public void FolderCollector_CollectsOnlyItsSingleSourceFolder()
        {
            const string rootPath = "Assets/ResourceSingleFolderTests";
            const string firstFolder = rootPath + "/First";
            const string secondFolder = rootPath + "/Second";
            const string firstAsset = firstFolder + "/First.asset";
            const string secondAsset = secondFolder + "/Second.asset";
            EnsureFolder(rootPath);
            EnsureFolder(firstFolder);
            EnsureFolder(secondFolder);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>(), firstAsset);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>(), secondAsset);
            AssetDatabase.SaveAssets();
            try
            {
                var bundle = new GameDeveloperKit.ResourceEditor.Authoring.Bundle
                {
                    Name = "single-folder",
                    CollectorId = GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.FolderCollectorId,
                    SourceFolder = firstFolder
                };
                bundle.EnsureDefaults();

                var previews = new GameDeveloperKit.ResourceEditor.Registry.FolderCollector().Collect(null, bundle);

                Assert.IsTrue(previews.Any(preview => preview.AssetPath == firstAsset));
                Assert.IsFalse(previews.Any(preview => preview.AssetPath == secondAsset));
            }
            finally
            {
                AssetDatabase.DeleteAsset(rootPath);
            }
        }

        [Test]
        public void ResourceEditorSource_UsesSingleFolderModelAndNoCollectorFallback()
        {
            var settingsSource = IOFile.ReadAllText("Assets/GameDeveloperKit/Editor/ResourceEditor/Authoring/Settings.cs");
            var collectorSource = IOFile.ReadAllText("Assets/GameDeveloperKit/Editor/ResourceEditor/Registry/BuiltinExtensions.cs");
            var registrySource = IOFile.ReadAllText("Assets/GameDeveloperKit/Editor/ResourceEditor/Registry/ExtensionRegistry.cs");
            var windowSource = IOFile.ReadAllText("Assets/GameDeveloperKit/Editor/ResourceEditor/UI/MainWindow.cs");

            StringAssert.DoesNotContain("m_AssetPaths", settingsSource);
            StringAssert.DoesNotContain("m_CollectorParameter", settingsSource);
            StringAssert.DoesNotContain("package.CollectorId", collectorSource);
            StringAssert.Contains("new[] { bundle.SourceFolder }", collectorSource);
            StringAssert.Contains("return null;", registrySource);
            StringAssert.Contains("new ObjectField", windowSource);
            StringAssert.Contains("paths.Count == 1 && folderCount == 1", windowSource);
        }

        [Test]
        public void BuildSnapshot_WhenFolderBelongsToTwoGroups_ReportsValidationErrors()
        {
            const string sourceFolder = "Assets/ResourceDuplicateFolderTests";
            EnsureFolder(sourceFolder);
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                settings.EnsureDefaults();
                for (var index = 0; index < 2; index++)
                {
                    var package = new GameDeveloperKit.ResourceEditor.Authoring.Package
                    {
                        Name = $"FolderPackage{index}",
                        BuildStrategyId = "single-bundle"
                    };
                    package.EnsureDefaults();
                    package.Bundles.Add(new GameDeveloperKit.ResourceEditor.Authoring.Bundle
                    {
                        Name = $"FolderGroup{index}",
                        Group = $"FolderGroup{index}",
                        CollectorId = GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.FolderCollectorId,
                        SourceFolder = sourceFolder
                    });
                    package.Bundles[0].EnsureDefaults();
                    settings.Packages.Add(package);
                }

                var snapshot = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(
                    settings,
                    GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan());

                Assert.AreEqual(2, snapshot.Issues.Count(issue => issue.Message == $"Folder can only belong to one Group: {sourceFolder}"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                AssetDatabase.DeleteAsset(sourceFolder);
            }
        }

        [Test]
        public void Reconcile_WhenFolderSourceIsDeleted_ClearsBindingAndMembership()
        {
            const string sourceFolder = "Assets/ResourceDeletedFolderTests";
            const string assetPath = sourceFolder + "/Entry.asset";
            EnsureFolder(sourceFolder);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>(), assetPath);
            AssetDatabase.SaveAssets();
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                settings.EnsureDefaults();
                var package = new GameDeveloperKit.ResourceEditor.Authoring.Package
                {
                    Name = "DeletedFolderPackage",
                    BuildStrategyId = "single-bundle"
                };
                package.EnsureDefaults();
                var bundle = new GameDeveloperKit.ResourceEditor.Authoring.Bundle
                {
                    Name = "DeletedFolderGroup",
                    Group = "DeletedFolderGroup",
                    CollectorId = GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.FolderCollectorId,
                    SourceFolder = sourceFolder
                };
                bundle.EnsureDefaults();
                package.Bundles.Add(bundle);
                settings.Packages.Add(package);

                var registry = GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan();
                GameDeveloperKit.ResourceEditor.Authoring.Service.Reconcile(
                    settings,
                    registry,
                    new GameDeveloperKit.ResourceEditor.Authoring.AssetChangeSet(fullReconcile: true));
                Assert.AreEqual(1, bundle.Entries.Count);

                AssetDatabase.DeleteAsset(sourceFolder);
                GameDeveloperKit.ResourceEditor.Authoring.Service.Reconcile(
                    settings,
                    registry,
                    new GameDeveloperKit.ResourceEditor.Authoring.AssetChangeSet(
                        deletedAssets: new[] { sourceFolder },
                        fullReconcile: true));

                Assert.AreEqual(string.Empty, bundle.SourceFolder);
                Assert.AreEqual(GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.ExplicitCollectorId, bundle.CollectorId);
                Assert.AreEqual(0, bundle.Entries.Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                AssetDatabase.DeleteAsset(sourceFolder);
            }
        }

        [Test]
        public void Reconcile_WhenFolderSourceMoves_UpdatesBindingAndMembershipPath()
        {
            const string rootFolder = "Assets/ResourceMovedFolderTests";
            const string sourceFolder = rootFolder + "/Source";
            const string targetFolder = rootFolder + "/Target";
            const string sourceAsset = sourceFolder + "/Entry.asset";
            const string targetAsset = targetFolder + "/Entry.asset";
            EnsureFolder(rootFolder);
            EnsureFolder(sourceFolder);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>(), sourceAsset);
            AssetDatabase.SaveAssets();
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                settings.EnsureDefaults();
                var package = new GameDeveloperKit.ResourceEditor.Authoring.Package
                {
                    Name = "MovedFolderPackage",
                    BuildStrategyId = "single-bundle"
                };
                package.EnsureDefaults();
                var bundle = new GameDeveloperKit.ResourceEditor.Authoring.Bundle
                {
                    Name = "MovedFolderGroup",
                    Group = "MovedFolderGroup",
                    CollectorId = GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.FolderCollectorId,
                    SourceFolder = sourceFolder
                };
                bundle.EnsureDefaults();
                package.Bundles.Add(bundle);
                settings.Packages.Add(package);
                var registry = GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan();
                GameDeveloperKit.ResourceEditor.Authoring.Service.Reconcile(
                    settings,
                    registry,
                    new GameDeveloperKit.ResourceEditor.Authoring.AssetChangeSet(fullReconcile: true));

                Assert.AreEqual(string.Empty, AssetDatabase.MoveAsset(sourceFolder, targetFolder));
                GameDeveloperKit.ResourceEditor.Authoring.Service.Reconcile(
                    settings,
                    registry,
                    new GameDeveloperKit.ResourceEditor.Authoring.AssetChangeSet(
                        movedAssets: new[] { new GameDeveloperKit.ResourceEditor.Authoring.AssetMove(sourceFolder, targetFolder) }));

                Assert.AreEqual(targetFolder, bundle.SourceFolder);
                Assert.AreEqual(1, bundle.Entries.Count);
                Assert.AreEqual(targetAsset, bundle.Entries[0].AssetPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                AssetDatabase.DeleteAsset(rootFolder);
            }
        }

        [Test]
        public void BundleEnsureDefaults_WhenCollectorIsCustom_PreservesCollectorId()
        {
            var bundle = new GameDeveloperKit.ResourceEditor.Authoring.Bundle
            {
                CollectorId = "custom-collector"
            };

            bundle.EnsureDefaults();

            Assert.AreEqual("custom-collector", bundle.CollectorId);
        }

        [Test]
        public void BuildSnapshot_WhenCustomCollectorIsMissing_ReportsRegistryError()
        {
            var settings = ScriptableObject.CreateInstance<GameDeveloperKit.ResourceEditor.Authoring.Settings>();
            try
            {
                settings.EnsureDefaults();
                var package = new GameDeveloperKit.ResourceEditor.Authoring.Package
                {
                    Name = "CustomCollectorPackage",
                    BuildStrategyId = "single-bundle"
                };
                package.EnsureDefaults();
                package.Bundles.Add(new GameDeveloperKit.ResourceEditor.Authoring.Bundle
                {
                    Name = "CustomCollectorGroup",
                    Group = "CustomCollectorGroup",
                    CollectorId = "missing-custom-collector"
                });
                package.Bundles[0].EnsureDefaults();
                settings.Packages.Add(package);

                var snapshot = GameDeveloperKit.ResourceEditor.Authoring.Service.BuildSnapshot(
                    settings,
                    GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry.Scan());

                Assert.IsTrue(snapshot.Issues.Any(issue => issue.Message == "Missing collector: missing-custom-collector"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        private static GameDeveloperKit.ResourceEditor.Authoring.Package CreatePackage(
            GameDeveloperKit.ResourceEditor.Authoring.Settings settings,
            string packageName,
            string bundleName)
        {
            var package = new GameDeveloperKit.ResourceEditor.Authoring.Package
            {
                Name = packageName,
                BuildStrategyId = "single-bundle"
            };
            package.EnsureDefaults();
            var bundle = new GameDeveloperKit.ResourceEditor.Authoring.Bundle
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

        private static GameDeveloperKit.ResourceEditor.Authoring.AssetEntry CreateEntry(string assetPath)
        {
            var entry = new GameDeveloperKit.ResourceEditor.Authoring.AssetEntry
            {
                Guid = AssetDatabase.AssetPathToGUID(assetPath),
                AssetPath = assetPath,
                Location = assetPath,
                TypeName = nameof(GameDeveloperKit.ResourceEditor.Authoring.Settings),
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

        private static string FormatIssue(GameDeveloperKit.ResourceEditor.Validation.Issue issue)
        {
            return $"{issue.Severity}|{issue.Source}|{issue.Message}|{issue.Package?.Name}|{issue.Bundle?.Name}|{issue.Resource?.AssetPath}";
        }
    }
}
