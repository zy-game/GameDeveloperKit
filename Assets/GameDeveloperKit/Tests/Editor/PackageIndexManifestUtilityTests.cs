using System;
using System.IO;
using NUnit.Framework;
using GameDeveloperKit.Runtime;

namespace GameDeveloperKit.Tests.Editor
{
    public sealed class PackageIndexManifestUtilityTests
    {
        private string _rootPath;

        [SetUp]
        public void SetUp()
        {
            _rootPath = Path.Combine(Path.GetTempPath(), "GameDeveloperKitTests", "PackageIndexManifest", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, true);
            }
        }

        [Test]
        public void LoadFromFile_WhenFileDoesNotExist_ReturnsDefaultManifest()
        {
            var manifest = PackageIndexManifestUtility.LoadFromFile(Path.Combine(_rootPath, "missing.json"));

            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.Version, Is.EqualTo("1.0"));
            Assert.That(manifest.Packages, Is.Not.Null);
            Assert.That(manifest.Packages.Count, Is.EqualTo(0));
            Assert.That(string.IsNullOrWhiteSpace(manifest.UpdateTimeUtc), Is.False);
        }

        [Test]
        public void SaveToFile_ThenLoadFromFile_PreservesPackageIndexData()
        {
            var path = Path.Combine(_rootPath, "manifest.json");
            var manifest = new PackageIndexManifest
            {
                Version = "2.0",
                UpdateTimeUtc = "2026-04-01T00:00:00.0000000Z"
            };
            manifest.Packages.Add(new PackageIndexEntry
            {
                Name = "test",
                CurrentVersion = "1.2.3",
                PackageRole = ResourcePackageRole.HotUpdate.ToString(),
                ManifestPath = "test/1.2.3/manifest.json",
                VersionFilePath = "test/version.txt",
                HashFilePath = "test/hash.txt",
                RemoteRoot = "https://cdn.example.com/test",
                IsBuiltinBootstrap = true,
                AutoPrepareOnStartup = true
            });
            manifest.Packages[0].StartupTags.Add("startup");
            manifest.Packages[0].Versions.Add(new PackageVersionEntry
            {
                Version = "1.2.3",
                BuildTimeUtc = "2026-04-01T00:00:00.0000000Z",
                SizeBytes = 1024,
                BundleCount = 3,
                ManifestPath = "test/1.2.3/manifest.json"
            });

            PackageIndexManifestUtility.SaveToFile(path, manifest);
            var loaded = PackageIndexManifestUtility.LoadFromFile(path);

            Assert.That(File.Exists(path), Is.True);
            Assert.That(loaded.Version, Is.EqualTo("2.0"));
            Assert.That(loaded.Packages.Count, Is.EqualTo(1));
            Assert.That(loaded.Packages[0].Name, Is.EqualTo("test"));
            Assert.That(loaded.Packages[0].CurrentVersion, Is.EqualTo("1.2.3"));
            Assert.That(loaded.Packages[0].ManifestPath, Is.EqualTo("test/1.2.3/manifest.json"));
            Assert.That(loaded.Packages[0].VersionFilePath, Is.EqualTo("test/version.txt"));
            Assert.That(loaded.Packages[0].HashFilePath, Is.EqualTo("test/hash.txt"));
            Assert.That(loaded.Packages[0].RemoteRoot, Is.EqualTo("https://cdn.example.com/test"));
            Assert.That(loaded.Packages[0].IsBuiltinBootstrap, Is.True);
            Assert.That(loaded.Packages[0].AutoPrepareOnStartup, Is.True);
            Assert.That(loaded.Packages[0].StartupTags, Is.EquivalentTo(new[] { "startup" }));
            Assert.That(loaded.Packages[0].Versions.Count, Is.EqualTo(1));
            Assert.That(loaded.Packages[0].Versions[0].Version, Is.EqualTo("1.2.3"));
            Assert.That(loaded.Packages[0].Versions[0].BundleCount, Is.EqualTo(3));
        }
    }
}
