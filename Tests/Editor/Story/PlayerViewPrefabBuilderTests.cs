using System.Linq;
using GameDeveloperKit.StoryEditor.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class PlayerViewPrefabBuilderTests
    {
        [Test]
        public void BuildPrefab_WhenGenerated_CreatesMediaControlsAndBindings()
        {
            const string generatedPath = "Assets/Bundles/Playback/PlaybackView.GeneratedTest.prefab";
            try
            {
                Assert.AreEqual(generatedPath, PlayerViewPrefabBuilder.BuildPrefab(generatedPath));
                var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(generatedPath);
                Assert.IsNotNull(prefabRoot);

                var seekRoot = prefabRoot.transform.Find("VideoSeek");
                var qualityRoot = prefabRoot.transform.Find("VideoQuality");
                Assert.IsNotNull(seekRoot);
                Assert.IsNotNull(seekRoot.Find("Slider"));
                Assert.IsNotNull(seekRoot.Find("PauseButton"));
                Assert.IsNotNull(qualityRoot);
                Assert.IsNotNull(qualityRoot.Find("QualityButton/Label"));
                var choiceRoot = prefabRoot.transform.Find("DialoguePanel/ChoiceRoot");
                Assert.IsNotNull(choiceRoot);
                Assert.AreEqual(4, choiceRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length);
                Assert.IsFalse(seekRoot.gameObject.activeSelf);
                Assert.IsFalse(qualityRoot.gameObject.activeSelf);

                var document = prefabRoot.GetComponent<GameDeveloperKit.UI.UIDocument>();
                Assert.IsNotNull(document);
                var bindingNames = document.Mappings.Select(mapping => mapping.Name).ToList();
                CollectionAssert.IsSubsetOf(new[]
                {
                    "VideoSeekRoot",
                    "VideoSeekSlider",
                    "VideoSeekTimeText",
                    "VideoSeekPauseButton",
                    "VideoQualityRoot",
                    "VideoQualityButton",
                    "VideoQualityText",
                    "DialogueRoot",
                }, bindingNames);
            }
            finally
            {
                AssetDatabase.DeleteAsset(generatedPath);
            }
        }
    }
}
