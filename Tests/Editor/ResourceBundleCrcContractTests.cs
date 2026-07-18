using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using GameDeveloperKit.Resource;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class ResourceBundleCrcContractTests
    {
        private const string FixturePath = "Assets/GameDeveloperKit/Tests/Runtime/LubanGeneratedTableFixture.json";

        [UnityTest]
        public IEnumerator SbpBundleCrc_WhenPassedToLoadFromStreamAsync_LoadsBuiltBundle()
        {
            Assert.IsTrue(System.IO.File.Exists(FixturePath), $"Fixture is missing: {FixturePath}");
            var outputRoot = Path.Combine(
                Path.GetTempPath(),
                "gdk-resource-bundle-crc-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputRoot);
            AssetBundle loadedBundle = null;
            try
            {
                const string bundleName = "crc-contract.bundle";
                var builtBundle = BuildBundle(outputRoot, bundleName);
                var fileCrc = Crc32Utility.Compute(System.IO.File.ReadAllBytes(builtBundle.Path));
                Assert.AreNotEqual(
                    fileCrc,
                    builtBundle.Crc,
                    "SBP AssetBundle CRC must not be treated as a file-byte CRC32.");

                using (var stream = System.IO.File.OpenRead(builtBundle.Path))
                {
                    var request = AssetBundle.LoadFromStreamAsync(stream, builtBundle.Crc);
                    yield return request;
                    loadedBundle = request.assetBundle;
                }

                Assert.IsNotNull(loadedBundle);
                Assert.IsNotNull(loadedBundle.LoadAsset<TextAsset>(FixturePath));
            }
            finally
            {
                loadedBundle?.Unload(true);
                if (Directory.Exists(outputRoot))
                {
                    Directory.Delete(outputRoot, true);
                }
            }
        }

        [TestCase(ResourceMode.Online)]
        [TestCase(ResourceMode.Web)]
        public void RemoteMode_WhenServerUsesHttp_RejectsBeforeDownload(ResourceMode mode)
        {
            var settings = new ResourceSettings
            {
                Mode = mode,
                ServerUrl = "http://127.0.0.1:18080",
                ChannelName = "release",
                ClientBuild = 100,
                TrustedKeys = new[]
                {
                    new ResourceTrustKey
                    {
                        KeyId = "test-key",
                        Modulus = "AQ==",
                        Exponent = "AQAB"
                    }
                }
            };

            var exception = Assert.Throws<GameException>(() => settings.ValidateRemoteSecurity());
            StringAssert.Contains("absolute HTTPS", exception.Message);
        }

        private static (string Path, uint Crc) BuildBundle(string outputRoot, string bundleName)
        {
            var manifest = BuildPipeline.BuildAssetBundles(
                outputRoot,
                new[]
                {
                    new AssetBundleBuild
                    {
                        assetBundleName = bundleName,
                        assetNames = new[] { FixturePath },
                        addressableNames = new[] { FixturePath }
                    }
                },
                BuildAssetBundleOptions.ChunkBasedCompression,
                EditorUserBuildSettings.activeBuildTarget);
            Assert.IsNotNull(manifest);

            var path = Path.Combine(outputRoot, bundleName);
            Assert.IsTrue(BuildPipeline.GetCRCForAssetBundle(path, out var crc));
            Assert.AreNotEqual(0u, crc);
            using (var sha1 = SHA1.Create())
            {
                Assert.AreEqual(20, sha1.ComputeHash(System.IO.File.ReadAllBytes(path)).Length);
            }

            return (path, crc);
        }
    }
}
