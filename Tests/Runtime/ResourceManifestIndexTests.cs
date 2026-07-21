using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Resource;
using NUnit.Framework;

namespace GameDeveloperKit.Tests
{
    public sealed class ResourceManifestIndexTests
    {
        [Test]
        public void ValidateAndIndex_WhenManifestEmpty_ReturnsEmptyIndex()
        {
            var manifest = new ManifestInfo
            {
                Version = null,
                Packages = new List<PackageInfo>()
            };

            var index = ResourceManifestValidator.ValidateAndIndex(manifest, ResourceMode.Offline);

            Assert.IsNull(index.Version);
            Assert.IsFalse(index.ContainsPackage("Base"));
            Assert.IsFalse(index.ContainsBundle("ui"));
            Assert.IsFalse(index.TryGetAssetLocation("ui/loading", out _));
            Assert.AreEqual(0, index.GetBundleNamesByLabel("ui").Count);
            Assert.AreEqual(0, index.GetBundleNamesByType("GameObject").Count);
            Assert.IsNull(index.CreatePackageBundleSnapshot("Base"));
        }

        [Test]
        public void ValidateAndIndex_WhenCandidateMutates_IndexRemainsUnchanged()
        {
            var asset = new AssetInfo
            {
                Location = "ui/loading",
                TypeName = "GameObject",
                Labels = new List<string> { "ui" }
            };
            var bundle = new BundleInfo
            {
                Name = "ui",
                ProviderId = ResourceProviderIds.AssetBundle,
                Assets = new List<AssetInfo> { asset }
            };
            var package = new PackageInfo
            {
                Name = "Base",
                Bundles = new List<BundleInfo> { bundle }
            };
            var manifest = new ManifestInfo
            {
                Version = "v1",
                Packages = new List<PackageInfo> { package }
            };
            var index = ResourceManifestValidator.ValidateAndIndex(manifest, ResourceMode.Offline);

            manifest.Version = "mutated";
            package.Name = "Changed";
            bundle.Name = "changed";
            asset.Location = "changed/location";
            asset.Labels.Clear();

            Assert.AreEqual("v1", index.Version);
            Assert.IsTrue(index.ContainsPackage("Base"));
            Assert.IsTrue(index.ContainsBundle("ui"));
            Assert.IsTrue(index.TryGetAssetLocation("ui/loading", out var owner));
            Assert.AreEqual("ui", owner);
            CollectionAssert.AreEqual(new[] { "ui" }, index.GetBundleNamesByLabel("ui"));
        }

        [TestCase("Assets/Bundles/UI/SelectChapter/UI_SelectChapter.prefab")]
        [TestCase("Assets/Bundles/UI/SelectChapter/UI_SelectChapter")]
        [TestCase("UI_SelectChapter.prefab")]
        [TestCase("UI_SelectChapter")]
        public void ValidateAndIndex_WhenAddressUsesSupportedAlias_ResolvesCanonicalLocation(
            string address)
        {
            const string expectedLocation = "Assets/Bundles/UI/SelectChapter/UI_SelectChapter.prefab";
            var manifest = CreateSingleAssetManifest(
                ResourceProviderIds.AssetBundle,
                expectedLocation,
                string.Empty);

            var index = ResourceManifestValidator.ValidateAndIndex(manifest, ResourceMode.EditorSimulator);

            Assert.IsTrue(index.TryResolveAssetAddress(address, out var bundleName, out var location));
            Assert.AreEqual("ui.bundle", bundleName);
            Assert.AreEqual(expectedLocation, location);
        }

        [Test]
        public void ValidateAndIndex_WhenShortNameIsDuplicated_RejectsOnlyAmbiguousAlias()
        {
            var first = CreateSingleAssetManifest(
                ResourceProviderIds.AssetBundle,
                "Assets/First/UI_SelectChapter.prefab",
                string.Empty);
            first.Packages[0].Bundles[0].Assets.Add(new AssetInfo
            {
                Location = "Assets/Second/UI_SelectChapter.prefab",
                Labels = new List<string>()
            });

            var index = ResourceManifestValidator.ValidateAndIndex(first, ResourceMode.EditorSimulator);

            Assert.IsTrue(index.TryResolveAssetAddress("Assets/First/UI_SelectChapter.prefab", out _, out var firstLocation));
            Assert.AreEqual("Assets/First/UI_SelectChapter.prefab", firstLocation);
            Assert.IsTrue(index.TryResolveAssetAddress("Assets/Second/UI_SelectChapter", out _, out var secondLocation));
            Assert.AreEqual("Assets/Second/UI_SelectChapter.prefab", secondLocation);
            Assert.IsFalse(index.TryResolveAssetAddress("UI_SelectChapter", out _, out _));
            Assert.IsTrue(index.IsAssetAddressAmbiguous("UI_SelectChapter"));
        }

        [Test]
        public void ValidateAndIndex_WhenCanonicalLocationMatchesAnotherShortName_PrefersCanonicalLocation()
        {
            var manifest = CreateSingleAssetManifest(
                ResourceProviderIds.AssetBundle,
                "UI_SelectChapter",
                string.Empty);
            manifest.Packages[0].Bundles[0].Assets.Add(new AssetInfo
            {
                Location = "Assets/Other/UI_SelectChapter.prefab",
                Labels = new List<string>()
            });

            var index = ResourceManifestValidator.ValidateAndIndex(manifest, ResourceMode.EditorSimulator);

            Assert.IsTrue(index.TryResolveAssetAddress("UI_SelectChapter", out _, out var location));
            Assert.AreEqual("UI_SelectChapter", location);
            Assert.IsFalse(index.IsAssetAddressAmbiguous("UI_SelectChapter"));
        }

        [Test]
        public void CreatePackageBundleSnapshot_WhenSnapshotMutates_IndexRemainsUnchanged()
        {
            var manifest = new ManifestInfo
            {
                Packages = new List<PackageInfo>
                {
                    new PackageInfo
                    {
                        Name = "Base",
                        Bundles = new List<BundleInfo>
                        {
                            new BundleInfo
                            {
                                Name = "ui",
                                ProviderId = ResourceProviderIds.AssetBundle,
                                Assets = new List<AssetInfo>
                                {
                                    new AssetInfo
                                    {
                                        Location = "ui/loading",
                                        Labels = new List<string> { "ui" }
                                    }
                                }
                            }
                        }
                    }
                }
            };
            var index = ResourceManifestValidator.ValidateAndIndex(manifest, ResourceMode.Offline);
            var first = index.CreatePackageBundleSnapshot("Base");

            first[0].Name = "changed";
            first[0].Assets[0].Location = "changed/location";
            first[0].Assets[0].Labels.Clear();
            var second = index.CreatePackageBundleSnapshot("Base");

            Assert.AreEqual("ui", second[0].Name);
            Assert.AreEqual("ui/loading", second[0].Assets[0].Location);
            CollectionAssert.AreEqual(new[] { "ui" }, second[0].Assets[0].Labels);
        }

        [Test]
        public void ValidateAndIndex_WhenStructureInvalid_AggregatesErrorsInStableOrder()
        {
            var manifest = new ManifestInfo
            {
                Packages = new List<PackageInfo>
                {
                    null,
                    new PackageInfo
                    {
                        Name = string.Empty,
                        Bundles = new List<BundleInfo>
                        {
                            null,
                            new BundleInfo
                            {
                                Name = "ui",
                                ProviderId = "unknown",
                                Size = -1,
                                Assets = new List<AssetInfo>
                                {
                                    null,
                                    new AssetInfo { Location = string.Empty }
                                }
                            }
                        }
                    },
                    new PackageInfo
                    {
                        Name = "Base",
                        Bundles = new List<BundleInfo>
                        {
                            new BundleInfo
                            {
                                Name = "ui",
                                ProviderId = ResourceProviderIds.Resources,
                                Assets = new List<AssetInfo>
                                {
                                    new AssetInfo { Location = "Resources/Foo.prefab" }
                                }
                            }
                        }
                    },
                    new PackageInfo { Name = "Base" }
                }
            };

            var exception = Assert.Throws<GameException>(() =>
                ResourceManifestValidator.ValidateAndIndex(manifest, ResourceMode.Offline));

            var message = exception.Message;
            Assert.That(message.IndexOf("Packages[0] cannot be null.", StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(message.IndexOf("Packages[1].Name cannot be empty.", StringComparison.Ordinal), Is.GreaterThan(message.IndexOf("Packages[0]", StringComparison.Ordinal)));
            Assert.That(message.IndexOf("Packages[1].Bundles[0] cannot be null.", StringComparison.Ordinal), Is.GreaterThan(message.IndexOf("Packages[1].Name", StringComparison.Ordinal)));
            StringAssert.Contains("Packages[1].Bundles[1].Size cannot be negative: -1.", message);
            StringAssert.Contains("Packages[1].Bundles[1].ProviderId is required", message);
            StringAssert.Contains("Packages[1].Bundles[1].Assets[0] cannot be null.", message);
            StringAssert.Contains("Packages[1].Bundles[1].Assets[1].Location cannot be empty.", message);
            StringAssert.Contains("Packages[2].Bundles[0].Name duplicates Packages[1].Bundles[1].Name: ui.", message);
            StringAssert.Contains("Packages[2].Bundles[0].Assets[0].Location must be an extensionless 'Resources/...' path", message);
            StringAssert.Contains("Packages[3].Name duplicates Packages[2].Name: Base.", message);
        }

        [Test]
        public void ValidateAndIndex_WhenPackagesNull_ThrowsPathError()
        {
            var manifest = new ManifestInfo { Packages = null };

            var exception = Assert.Throws<GameException>(() =>
                ResourceManifestValidator.ValidateAndIndex(manifest, ResourceMode.Offline));

            StringAssert.Contains("Packages cannot be null.", exception.Message);
        }

        [TestCase(ResourceMode.Online)]
        [TestCase(ResourceMode.Web)]
        public void ValidateAndIndex_WhenRemoteFieldsMissing_AggregatesVersionHashAndSize(ResourceMode mode)
        {
            var manifest = CreateSingleAssetManifest(
                ResourceProviderIds.AssetBundle,
                "ui/loading",
                hash: string.Empty);
            manifest.Version = string.Empty;
            manifest.Packages[0].Bundles[0].Size = -1;

            var exception = Assert.Throws<GameException>(() =>
                ResourceManifestValidator.ValidateAndIndex(
                    manifest,
                    mode,
                    new[] { "ui.bundle" }));

            StringAssert.Contains("Packages[0].Bundles[0].Hash must be a 40-character hexadecimal SHA-1", exception.Message);
            StringAssert.Contains("Packages[0].Bundles[0].Size cannot be negative: -1.", exception.Message);
            StringAssert.Contains($"Version is required when {mode} manifest contains asset-bundle resources.", exception.Message);
        }

        [Test]
        public void ValidateAndIndex_WhenRemoteNamesProvided_TracksOnlyRemoteAndDeepCopies()
        {
            const string remoteHash = "0123456789abcdef0123456789abcdef01234567";
            var local = CreateBundle("local.bundle");
            var remote = CreateBundle("remote.bundle");
            remote.Hash = remoteHash;
            remote.Size = 128;
            var remoteNames = new[] { remote.Name };
            var manifest = new ManifestInfo
            {
                Version = "v1",
                Packages = new List<PackageInfo>
                {
                    new PackageInfo
                    {
                        Name = "Base",
                        Bundles = new List<BundleInfo> { local, remote }
                    }
                }
            };

            var index = ResourceManifestValidator.ValidateAndIndex(
                manifest,
                ResourceMode.Online,
                remoteNames);

            remoteNames[0] = "changed.bundle";
            remote.Hash = "changed";

            Assert.IsFalse(index.IsRemoteBundle("local.bundle"));
            Assert.IsTrue(index.IsRemoteBundle("remote.bundle"));
            Assert.IsFalse(index.IsRemoteBundle("changed.bundle"));
            var snapshot = index.CreateRemoteBundleSnapshot();
            Assert.AreEqual(1, snapshot.Count);
            Assert.AreEqual("remote.bundle", snapshot[0].Name);
            Assert.AreEqual(remoteHash, snapshot[0].Hash);
        }

        [Test]
        public void ValidateAndIndex_WhenOnlinePackagedHashIsEmpty_DoesNotTreatItAsRemote()
        {
            var manifest = CreateSingleAssetManifest(
                ResourceProviderIds.AssetBundle,
                "ui/loading",
                string.Empty);

            var index = ResourceManifestValidator.ValidateAndIndex(
                manifest,
                ResourceMode.Online,
                Array.Empty<string>());

            Assert.IsFalse(index.IsRemoteBundle("ui.bundle"));
        }

        [Test]
        public void ValidateAndIndex_WhenRemoteBundleUsesResourcesProvider_RejectsSource()
        {
            var manifest = CreateSingleAssetManifest(
                ResourceProviderIds.Resources,
                "ui/loading",
                "0123456789abcdef0123456789abcdef01234567");
            manifest.Packages[0].Bundles[0].Size = 128;

            var exception = Assert.Throws<GameException>(() =>
                ResourceManifestValidator.ValidateAndIndex(
                    manifest,
                    ResourceMode.Online,
                    new[] { "ui.bundle" }));

            StringAssert.Contains("ProviderId must be 'asset-bundle' for a remote bundle", exception.Message);
        }

        [Test]
        public void ValidateAndIndex_WhenWebRemoteCrcIsZero_RejectsBeforeCommit()
        {
            var manifest = CreateSingleAssetManifest(
                ResourceProviderIds.AssetBundle,
                "ui/loading",
                "0123456789abcdef0123456789abcdef01234567");
            manifest.Packages[0].Bundles[0].Size = 128;
            manifest.Packages[0].Bundles[0].Crc = 0;

            var exception = Assert.Throws<GameException>(() =>
                ResourceManifestValidator.ValidateAndIndex(
                    manifest,
                    ResourceMode.Web,
                    new[] { "ui.bundle" }));

            StringAssert.Contains("Crc must be non-zero for a Web remote bundle", exception.Message);
        }

        [Test]
        public void ValidateAndIndex_WhenRemoteNameIsMissingFromMergedManifest_RejectsSource()
        {
            var manifest = new ManifestInfo
            {
                Version = "v1",
                Packages = new List<PackageInfo>()
            };

            var exception = Assert.Throws<GameException>(() =>
                ResourceManifestValidator.ValidateAndIndex(
                    manifest,
                    ResourceMode.Online,
                    new[] { "missing.bundle" }));

            StringAssert.Contains("Remote bundle name does not exist in the merged manifest: missing.bundle", exception.Message);
        }

        [Test]
        public void ValidateAndIndex_WhenOfflineHashEmpty_UsesValidNameContract()
        {
            var manifest = CreateSingleAssetManifest(
                ResourceProviderIds.AssetBundle,
                "ui/loading",
                hash: string.Empty);

            var index = ResourceManifestValidator.ValidateAndIndex(manifest, ResourceMode.Offline);

            Assert.IsTrue(index.ContainsBundle("ui.bundle"));
        }

        [Test]
        public void ValidateAndIndex_WhenModeUnknown_ThrowsModeError()
        {
            var manifest = new ManifestInfo { Packages = new List<PackageInfo>() };

            var exception = Assert.Throws<GameException>(() =>
                ResourceManifestValidator.ValidateAndIndex(manifest, (ResourceMode)255));

            StringAssert.Contains("Mode has unsupported value: 255.", exception.Message);
        }

        [Test]
        public void ValidateAndIndex_WhenDependenciesShared_CreatesDependencyFirstClosure()
        {
            var core = CreateBundle("core.bundle");
            var shared = CreateBundle("shared.bundle", "core.bundle");
            var ui = CreateBundle("ui.bundle", "shared.bundle");
            var gameplay = CreateBundle("gameplay.bundle", "shared.bundle");
            var manifest = new ManifestInfo
            {
                Packages = new List<PackageInfo>
                {
                    new PackageInfo
                    {
                        Name = "Base",
                        Bundles = new List<BundleInfo> { ui, gameplay }
                    },
                    new PackageInfo
                    {
                        Name = "Shared",
                        Bundles = new List<BundleInfo> { shared, core }
                    }
                }
            };

            var index = ResourceManifestValidator.ValidateAndIndex(manifest, ResourceMode.Offline);
            var closure = index.CreatePackageBundleSnapshot("Base");

            CollectionAssert.AreEqual(
                new[] { "core.bundle", "shared.bundle", "ui.bundle", "gameplay.bundle" },
                closure.Select(bundle => bundle.Name).ToArray());
        }

        [Test]
        public void ValidateAndIndex_WhenDependencyMissingOrSelf_AggregatesBothErrors()
        {
            var manifest = new ManifestInfo
            {
                Packages = new List<PackageInfo>
                {
                    new PackageInfo
                    {
                        Name = "Base",
                        Bundles = new List<BundleInfo>
                        {
                            CreateBundle("ui.bundle", "missing.bundle", "ui.bundle")
                        }
                    }
                }
            };

            var exception = Assert.Throws<GameException>(() =>
                ResourceManifestValidator.ValidateAndIndex(manifest, ResourceMode.Offline));

            StringAssert.Contains("Packages[0].Bundles[0].Dependencies[0] references missing bundle: missing.bundle.", exception.Message);
            StringAssert.Contains("Packages[0].Bundles[0].Dependencies[1] cannot reference its own bundle: ui.bundle.", exception.Message);
        }

        [Test]
        public void ValidateAndIndex_WhenDependencyCycle_ReportsFullCycle()
        {
            var manifest = new ManifestInfo
            {
                Packages = new List<PackageInfo>
                {
                    new PackageInfo
                    {
                        Name = "Base",
                        Bundles = new List<BundleInfo>
                        {
                            CreateBundle("a.bundle", "b.bundle"),
                            CreateBundle("b.bundle", "c.bundle"),
                            CreateBundle("c.bundle", "a.bundle")
                        }
                    }
                }
            };

            var exception = Assert.Throws<GameException>(() =>
                ResourceManifestValidator.ValidateAndIndex(manifest, ResourceMode.Offline));

            StringAssert.Contains("creates dependency cycle: a.bundle -> b.bundle -> c.bundle -> a.bundle.", exception.Message);
        }

        [Test]
        public void ManifestInfoQueries_WhenGraphAmbiguousOrInvalid_ThrowExplicitly()
        {
            var duplicate = new ManifestInfo
            {
                Packages = new List<PackageInfo>
                {
                    new PackageInfo
                    {
                        Name = "Base",
                        Bundles = new List<BundleInfo>
                        {
                            CreateBundle("same.bundle"),
                            CreateBundle("same.bundle")
                        }
                    }
                }
            };
            StringAssert.Contains(
                "Duplicate bundle name: same.bundle",
                Assert.Throws<GameException>(() => duplicate.GetBundle("same.bundle")).Message);

            var missing = new ManifestInfo
            {
                Packages = new List<PackageInfo>
                {
                    new PackageInfo
                    {
                        Name = "Base",
                        Bundles = new List<BundleInfo>
                        {
                            CreateBundle("ui.bundle", "missing.bundle")
                        }
                    }
                }
            };
            StringAssert.Contains(
                "Bundle dependency not found: missing.bundle",
                Assert.Throws<GameException>(() => missing.GetDependencies("ui.bundle")).Message);

            var cycle = new ManifestInfo
            {
                Packages = new List<PackageInfo>
                {
                    new PackageInfo
                    {
                        Name = "Base",
                        Bundles = new List<BundleInfo>
                        {
                            CreateBundle("a.bundle", "b.bundle"),
                            CreateBundle("b.bundle", "a.bundle")
                        }
                    }
                }
            };
            StringAssert.Contains(
                "Bundle dependency cycle: a.bundle -> b.bundle -> a.bundle",
                Assert.Throws<GameException>(() => cycle.GetPackageBundles("Base")).Message);
        }

        private static ManifestInfo CreateSingleAssetManifest(
            string providerId,
            string location,
            string hash)
        {
            return new ManifestInfo
            {
                Version = "v1",
                Packages = new List<PackageInfo>
                {
                    new PackageInfo
                    {
                        Name = "Base",
                        Bundles = new List<BundleInfo>
                        {
                            new BundleInfo
                            {
                                Name = "ui.bundle",
                                ProviderId = providerId,
                                Hash = hash,
                                Assets = new List<AssetInfo>
                                {
                                    new AssetInfo
                                    {
                                        Location = location,
                                        Labels = new List<string>()
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        private static BundleInfo CreateBundle(string name, params string[] dependencies)
        {
            return new BundleInfo
            {
                Name = name,
                ProviderId = ResourceProviderIds.AssetBundle,
                Dependencies = new List<string>(dependencies),
                Assets = new List<AssetInfo>()
            };
        }
    }
}
