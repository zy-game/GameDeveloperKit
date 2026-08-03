using System.Linq;
using GameDeveloperKit.EditorPlayable;
using GameDeveloperKit.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class PlayerWindowPrefabBuilderTests
    {
        private const string VideoPath = "Assets/Bundles/Playback/VideoPlayerWindow.GeneratedTest.prefab";
        private const string ImagePath = "Assets/Bundles/Playback/ImagePlayerWindow.GeneratedTest.prefab";
        private const string StoryPath = "Assets/Bundles/Playback/StoryPlaybackWindow.GeneratedTest.prefab";

        [Test]
        public void BuildVideoPlayerPrefab_CreatesDirectWindowBindings()
        {
            try
            {
                Assert.AreEqual(VideoPath, PlayerWindowPrefabBuilder.BuildVideoPlayerPrefab(VideoPath));
                AssertBindings(VideoPath,
                    "PlaybackRoot",
                    "VideoOutput",
                    "ChromeRoot",
                    "ToggleChromeButton",
                    "BackButton",
                    "TitleText",
                    "PlayPauseButton",
                    "TimeText",
                    "ProgressRoot",
                    "ProgressSlider",
                    "SpeedButton",
                    "QualityButton",
                    "QualityMenuRoot",
                    "QualityOptionTemplate");
            }
            finally
            {
                AssetDatabase.DeleteAsset(VideoPath);
            }
        }

        [Test]
        public void BuildImagePlayerPrefab_CreatesCarouselAndClickBindings()
        {
            try
            {
                Assert.AreEqual(ImagePath, PlayerWindowPrefabBuilder.BuildImagePlayerPrefab(ImagePath));
                AssertBindings(ImagePath,
                    "ImageOutput",
                    "ImageClickButton",
                    "BackButton",
                    "PreviousButton",
                    "NextButton",
                    "CounterText");
            }
            finally
            {
                AssetDatabase.DeleteAsset(ImagePath);
            }
        }

        [Test]
        public void BuildStoryPlaybackPrefab_ContainsVideoBaseAndStoryOverlayBindings()
        {
            try
            {
                Assert.AreEqual(StoryPath, PlayerWindowPrefabBuilder.BuildStoryPlaybackPrefab(StoryPath));
                AssertBindings(StoryPath,
                    "VideoOutput",
                    "ChromeRoot",
                    "ImageOutput",
                    "DialogueRoot",
                    "SpeakerText",
                    "BodyText",
                    "ContinueButton",
                    "ChoiceRoot",
                    "LoadingRoot");

                var root = AssetDatabase.LoadAssetAtPath<GameObject>(StoryPath);
                Assert.AreEqual(4, root.transform.Find("DialogueRoot/ChoiceRoot")
                    .GetComponentsInChildren<UnityEngine.UI.Button>(true).Length);
                Assert.IsFalse(root.transform.Find("LoadingRoot").gameObject.activeSelf);
            }
            finally
            {
                AssetDatabase.DeleteAsset(StoryPath);
            }
        }

        private static void AssertBindings(string path, params string[] expected)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(root);
            var document = root.GetComponent<UIDocument>();
            Assert.IsNotNull(document);
            var names = document.Mappings.Select(mapping => mapping.Name).ToArray();
            CollectionAssert.IsSubsetOf(expected, names);
            Assert.AreEqual(expected.Length, expected.Distinct().Count());
        }
    }
}
