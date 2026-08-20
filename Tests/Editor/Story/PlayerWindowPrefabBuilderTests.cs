using System.Linq;
using GameDeveloperKit.EditorPlayable;
using GameDeveloperKit.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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
                    "PlaybackFeaturesRoot",
                    "SkipButton",
                    "SettingsButton",
                    "VolumeSlider",
                    "VolumeText",
                    "SpeedButton",
                    "QualityButton",
                    "QualityMenuRoot",
                    "QualityOptionTemplate");
                AssertPlaybackFeatureControls(VideoPath);
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
                    "PlaybackFeaturesRoot",
                    "SkipButton",
                    "SettingsButton",
                    "VolumeSlider",
                    "VolumeText",
                    "LoadingRoot");
                AssertPlaybackFeatureControls(StoryPath);

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

        private static void AssertPlaybackFeatureControls(string path)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var document = root.GetComponent<UIDocument>();
            var skipButton = document.GetComponent<Button>("SkipButton");
            var timeText = root.transform.Find("ChromeRoot/BottomControls/TimeText")
                .GetComponent<RectTransform>();
            var settingsButton = document.GetComponent<Button>("SettingsButton");
            var volumeSlider = document.GetComponent<Slider>("VolumeSlider");
            var speedButton = document.GetComponent<Button>("SpeedButton");
            var expectedIcon = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Bundles/Images/Icon/zy_icon_sz.png");

            Assert.LessOrEqual(
                skipButton.GetComponent<RectTransform>().anchoredPosition.x +
                skipButton.GetComponent<RectTransform>().rect.width,
                timeText.anchoredPosition.x);
            Assert.LessOrEqual(
                volumeSlider.GetComponent<RectTransform>().anchoredPosition.x,
                speedButton.GetComponent<RectTransform>().anchoredPosition.x -
                speedButton.GetComponent<RectTransform>().rect.width);
            Assert.AreSame(expectedIcon, settingsButton.image.sprite);
            Assert.IsTrue(settingsButton.image.preserveAspect);
            Assert.AreEqual(Color.white, settingsButton.image.color);
            var label = settingsButton.transform.Find("Label");
            var labelComponent = label.GetComponents<Component>()
                .First(component => component.GetType().GetProperty("text") != null);
            Assert.AreEqual(
                string.Empty,
                labelComponent.GetType().GetProperty("text").GetValue(labelComponent));
        }
    }
}
