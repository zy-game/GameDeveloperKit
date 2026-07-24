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
                Assert.IsNotNull(qualityRoot.Find("QualityMenu/Options/OptionTemplate"));
                Assert.IsFalse(qualityRoot.Find("QualityMenu").gameObject.activeSelf);
                var seekRect = (RectTransform)seekRoot;
                Assert.AreEqual(Vector2.zero, seekRect.anchorMin);
                Assert.AreEqual(Vector2.right, seekRect.anchorMax);
                Assert.AreEqual(132f, seekRect.sizeDelta.y);
                var sliderRect = (RectTransform)seekRoot.Find("Slider");
                Assert.AreEqual(new Vector2(0f, 1f), sliderRect.anchorMin);
                Assert.AreEqual(new Vector2(1f, 1f), sliderRect.anchorMax);
                Assert.AreEqual(-8f, sliderRect.anchoredPosition.y);
                var qualityRect = (RectTransform)qualityRoot;
                Assert.AreEqual(Vector2.right, qualityRect.anchorMin);
                Assert.AreEqual(Vector2.right, qualityRect.anchorMax);
                Assert.AreEqual(new Vector2(-48f, 26f), qualityRect.anchoredPosition);
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
                    "VideoQualityMenuRoot",
                    "VideoQualityOptionsRoot",
                    "VideoQualityOptionTemplate",
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
