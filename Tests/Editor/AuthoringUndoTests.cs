using GameDeveloperKit.Config;
using GameDeveloperKit.StoryEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameDeveloperKit.StoryEditor.Model;

namespace GameDeveloperKit.Tests
{
    public sealed class AuthoringUndoTests
    {
        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
        }

        [Test]
        public void Mutate_TagCatalog_RoundTripsNestedGroup()
        {
            var catalog = ScriptableObject.CreateInstance<TagCatalogAsset>();
            catalog.EnsureDefaults();
            var initialCount = catalog.Groups.Count;

            AuthoringUndo.Mutate(catalog, "Add Test Tag Group", () => catalog.Groups.Add(new TagGroupDefinition
            {
                Key = "test-group",
                DisplayName = "Test Group"
            }));
            Assert.AreEqual(initialCount + 1, catalog.Groups.Count);

            Undo.PerformUndo();
            Assert.AreEqual(initialCount, catalog.Groups.Count);

            Undo.PerformRedo();
            Assert.AreEqual(initialCount + 1, catalog.Groups.Count);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Record_StoryVolumeAsset_RoundTripsNestedEpisode()
        {
            var asset = AuthoringVolumeAsset.CreateDefault("test-volume", "Test Volume");
            var initialCount = asset.Volume.Episodes.Count;

            AuthoringUndo.Record(asset, "Add Test Story Episode");
            asset.Volume.Episodes.Add(new AuthoringEpisode
            {
                EpisodeId = "test-episode",
                Title = "Test Episode"
            });
            EditorUtility.SetDirty(asset);
            Assert.AreEqual(initialCount + 1, asset.Volume.Episodes.Count);

            Undo.PerformUndo();
            Assert.AreEqual(initialCount, asset.Volume.Episodes.Count);

            Undo.PerformRedo();
            Assert.AreEqual(initialCount + 1, asset.Volume.Episodes.Count);
            Object.DestroyImmediate(asset);
        }
    }
}
