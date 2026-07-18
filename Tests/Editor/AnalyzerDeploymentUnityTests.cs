using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.CodeAnalysis;
using NUnit.Framework;
using UnityEditor;

namespace GameDeveloperKit.Tests
{
    public sealed class AnalyzerDeploymentUnityTests
    {
        [Test]
        public void Manifest_DefinesUniqueAnalyzerAndGeneratorDestinations()
        {
            var manifest = AnalyzerDeploymentManifestAsset.Load(out var packageRoot, out var manifestAssetPath);

            Assert.AreEqual(1, manifest.SchemaVersion);
            Assert.AreEqual(2, manifest.Components.Count);
            Assert.AreEqual(1, manifest.ExternalComponents.Count);
            Assert.AreEqual(2, manifest.Components.Select(static component => component.Name).Distinct(StringComparer.Ordinal).Count());
            Assert.AreEqual(3, manifest.Components.Concat(manifest.ExternalComponents).Select(static component => component.UnityAsset).Distinct(StringComparer.Ordinal).Count());
            Assert.IsTrue(manifest.Components.All(static component => component.UnityAsset.StartsWith("Analyzers/", StringComparison.Ordinal)));
            Assert.IsTrue(manifestAssetPath.StartsWith(packageRoot + "/", StringComparison.Ordinal));
            StringAssert.DoesNotContain("/Editor/", AnalyzerDeploymentManifestAsset.DeploymentDirectory);
        }

        [Test]
        public void SynchronizeNow_ConfiguresRoslynAnalyzerOnlyImports()
        {
            AnalyzerDeploymentAssetPostprocessor.SynchronizeNow();
            var manifest = AnalyzerDeploymentManifestAsset.Load(out var packageRoot, out _);

            foreach (var component in manifest.Components.Concat(manifest.ExternalComponents))
            {
                var unityAssetPath = AnalyzerDeploymentManifestAsset.ResolveUnityAssetPath(packageRoot, component);
                var asset = AssetDatabase.LoadMainAssetAtPath(unityAssetPath);
                Assert.IsNotNull(asset, unityAssetPath);
                CollectionAssert.Contains(AssetDatabase.GetLabels(asset), AnalyzerDeploymentAssetPostprocessor.RoslynAnalyzerLabel);

                var importer = AssetImporter.GetAtPath(unityAssetPath) as PluginImporter;
                Assert.IsNotNull(importer, unityAssetPath);
                Assert.IsFalse(importer.GetCompatibleWithAnyPlatform(), unityAssetPath);
                Assert.IsFalse(importer.GetCompatibleWithEditor(), unityAssetPath);

                foreach (BuildTarget buildTarget in Enum.GetValues(typeof(BuildTarget)))
                {
                    if (buildTarget != BuildTarget.NoTarget)
                    {
                        Assert.IsFalse(importer.GetCompatibleWithPlatform(buildTarget), $"{unityAssetPath}: {buildTarget}");
                    }
                }
            }
        }

        [Test]
        public void DeploymentDirectory_ContainsOnlyManifestAssets()
        {
            var manifest = AnalyzerDeploymentManifestAsset.Load(out var packageRoot, out _);
            var expected = new HashSet<string>(
                manifest.Components.Select(component => AnalyzerDeploymentManifestAsset.ResolveUnityAssetPath(packageRoot, component)),
                StringComparer.Ordinal);
            var prefix = AnalyzerDeploymentManifestAsset.DeploymentDirectory + "/";
            var actual = new HashSet<string>(
                AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith(prefix, StringComparison.Ordinal)),
                StringComparer.Ordinal);

            CollectionAssert.AreEquivalent(expected, actual);
        }
    }
}
