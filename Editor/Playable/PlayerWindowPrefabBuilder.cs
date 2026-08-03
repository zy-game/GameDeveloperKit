using System;
using System.Collections.Generic;
using System.IO;
using GameDeveloperKit.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.EditorPlayable
{
    public static class PlayerWindowPrefabBuilder
    {
        private readonly struct Binding
        {
            public Binding(string name, Component component)
            {
                Name = name;
                Component = component;
            }

            public string Name { get; }

            public Component Component { get; }
        }

        private sealed class VideoControls
        {
            public Transform PlaybackRoot;
            public RawImage VideoOutput;
            public RectTransform ChromeRoot;
            public Button ToggleChromeButton;
            public Button BackButton;
            public TMP_Text TitleText;
            public Button PlayPauseButton;
            public TMP_Text PlayPauseText;
            public TMP_Text TimeText;
            public RectTransform ProgressRoot;
            public Slider ProgressSlider;
            public Button SpeedButton;
            public TMP_Text SpeedText;
            public Button QualityButton;
            public TMP_Text QualityText;
            public RectTransform QualityMenuRoot;
            public RectTransform QualityOptionsRoot;
            public Button QualityOptionTemplate;
        }

        private sealed class StoryControls
        {
            public RawImage ImageOutput;
            public RectTransform DialogueRoot;
            public TMP_Text SpeakerText;
            public TMP_Text BodyText;
            public TMP_Text ErrorText;
            public Button ContinueButton;
            public RectTransform ChoiceRoot;
            public RectTransform LoadingRoot;
            public RectTransform LoadingSpinner;
        }

        public const string VideoPrefabPath = "Assets/Bundles/Playback/VideoPlayerWindow.prefab";
        public const string ImagePrefabPath = "Assets/Bundles/Playback/ImagePlayerWindow.prefab";
        public const string StoryPrefabPath = "Assets/Bundles/Playback/StoryPlaybackWindow.prefab";

        private const string TempRootName = "__PlayerWindowPrefabBuilder";
        private const int PlayerLayerOrder = 500;
        private const int DefaultChoiceButtonCount = 4;

        [MenuItem("GameDeveloperKit/播放器/生成全部播放器窗口")]
        public static void BuildAllFromMenu()
        {
            BuildAll();
        }

        public static IReadOnlyList<string> BuildAll()
        {
            return new[]
            {
                BuildVideoPlayerPrefab(VideoPrefabPath),
                BuildImagePlayerPrefab(ImagePrefabPath),
                BuildStoryPlaybackPrefab(StoryPrefabPath)
            };
        }

        public static string BuildVideoPlayerPrefab()
        {
            return BuildVideoPlayerPrefab(VideoPrefabPath);
        }

        public static string BuildImagePlayerPrefab()
        {
            return BuildImagePlayerPrefab(ImagePrefabPath);
        }

        public static string BuildStoryPlaybackPrefab()
        {
            return BuildStoryPlaybackPrefab(StoryPrefabPath);
        }

        internal static string BuildVideoPlayerPrefab(string prefabPath)
        {
            return BuildVideoWindow(prefabPath, false);
        }

        internal static string BuildStoryPlaybackPrefab(string prefabPath)
        {
            return BuildVideoWindow(prefabPath, true);
        }

        internal static string BuildImagePlayerPrefab(string prefabPath)
        {
            ValidatePrefabPath(prefabPath);
            EnsureAssetFolder(Path.GetDirectoryName(prefabPath)?.Replace('\\', '/'));
            DestroyTemporaryRoot();
            var root = CreateRoot("ImagePlayerWindow");
            try
            {
                var document = root.GetComponent<UIDocument>();
                var backdrop = CreatePanel(root.transform, "Backdrop", Color.black);
                Stretch(backdrop.rectTransform);

                var imageOutput = CreateRawImage(root.transform, "ImageOutput", Color.clear);
                Stretch(imageOutput.rectTransform, 72f, 72f, 72f, 72f);
                var fitter = imageOutput.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = 16f / 9f;

                var imageClickButton = CreateTransparentButton(root.transform, "ImageClickButton");
                Stretch(imageClickButton.GetComponent<RectTransform>());

                var backButton = CreateButton(root.transform, "BackButton", "<", new Color(0.03f, 0.04f, 0.05f, 0.82f));
                AnchorTopLeft(backButton.GetComponent<RectTransform>(), 36f, -32f, 64f, 56f);

                var previousButton = CreateButton(root.transform, "PreviousButton", "<", new Color(0.03f, 0.04f, 0.05f, 0.72f));
                AnchorMiddleLeft(previousButton.GetComponent<RectTransform>(), 36f, 72f, 72f);
                var nextButton = CreateButton(root.transform, "NextButton", ">", new Color(0.03f, 0.04f, 0.05f, 0.72f));
                AnchorMiddleRight(nextButton.GetComponent<RectTransform>(), -36f, 72f, 72f);

                var counterText = CreateText(root.transform, "CounterText", "0 / 0", 22, FontStyles.Normal, Color.white);
                AnchorBottomCenter(counterText.rectTransform, 0f, 28f, 180f, 40f);
                counterText.alignment = TextAlignmentOptions.Center;

                AssignDocumentBindings(
                    document,
                    root.GetComponent<RectTransform>(),
                    PlayerLayerOrder,
                    new Binding("ImageOutput", imageOutput),
                    new Binding("ImageClickButton", imageClickButton),
                    new Binding("BackButton", backButton),
                    new Binding("PreviousButton", previousButton),
                    new Binding("NextButton", nextButton),
                    new Binding("CounterText", counterText));
                return SavePrefab(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static string BuildVideoWindow(string prefabPath, bool includeStory)
        {
            ValidatePrefabPath(prefabPath);
            EnsureAssetFolder(Path.GetDirectoryName(prefabPath)?.Replace('\\', '/'));
            DestroyTemporaryRoot();
            var root = CreateRoot(includeStory ? "StoryPlaybackWindow" : "VideoPlayerWindow");
            try
            {
                var document = root.GetComponent<UIDocument>();
                var video = CreateVideoControls(root.transform);
                var bindings = CreateVideoBindings(video);
                if (includeStory)
                {
                    var story = CreateStoryControls(root.transform, video.VideoOutput.transform.parent);
                    AddStoryBindings(bindings, story);
                }

                AssignDocumentBindings(
                    document,
                    root.GetComponent<RectTransform>(),
                    PlayerLayerOrder,
                    bindings.ToArray());
                return SavePrefab(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static VideoControls CreateVideoControls(Transform root)
        {
            var result = new VideoControls();
            result.PlaybackRoot = CreateChild(root, "PlaybackRoot").transform;

            var mediaLayer = CreateRect(root, "MediaLayer");
            Stretch(mediaLayer);
            var backdrop = CreatePanel(mediaLayer, "Backdrop", Color.black);
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = false;
            result.VideoOutput = CreateRawImage(mediaLayer, "VideoOutput", Color.white);
            Stretch(result.VideoOutput.rectTransform);
            result.VideoOutput.raycastTarget = false;

            result.ToggleChromeButton = CreateTransparentButton(root, "ToggleChromeButton");
            Stretch(result.ToggleChromeButton.GetComponent<RectTransform>());

            result.ChromeRoot = CreateRect(root, "ChromeRoot");
            Stretch(result.ChromeRoot);

            var header = CreatePanel(result.ChromeRoot, "Header", new Color(0.02f, 0.025f, 0.03f, 0.68f));
            AnchorTopStretch(header.rectTransform, 96f);
            result.BackButton = CreateButton(header.transform, "BackButton", "<", Color.clear);
            AnchorTopLeft(result.BackButton.GetComponent<RectTransform>(), 28f, -20f, 64f, 56f);
            result.TitleText = CreateText(header.transform, "TitleText", string.Empty, 28, FontStyles.Normal, Color.white);
            Stretch(result.TitleText.rectTransform, 112f, 18f, 32f, 18f);
            result.TitleText.alignment = TextAlignmentOptions.MidlineLeft;
            result.TitleText.overflowMode = TextOverflowModes.Ellipsis;

            var bottom = CreatePanel(result.ChromeRoot, "BottomControls", new Color(0.02f, 0.025f, 0.03f, 0.78f));
            AnchorBottomStretch(bottom.rectTransform, 142f);

            result.ProgressRoot = CreateRect(bottom.transform, "ProgressRoot");
            AnchorTopStretch(result.ProgressRoot, 34f);
            result.ProgressSlider = CreateSlider(result.ProgressRoot, "ProgressSlider");
            Stretch(result.ProgressSlider.GetComponent<RectTransform>(), 28f, 4f, 28f, 4f);

            result.PlayPauseButton = CreateButton(bottom.transform, "PlayPauseButton", "II", Color.clear);
            AnchorBottomLeft(result.PlayPauseButton.GetComponent<RectTransform>(), 28f, 24f, 64f, 64f);
            result.PlayPauseText = result.PlayPauseButton.GetComponentInChildren<TMP_Text>(true);
            result.PlayPauseText.name = "PlayPauseText";
            result.PlayPauseText.fontSize = 28f;

            result.TimeText = CreateText(bottom.transform, "TimeText", "00:00 / 00:00", 22, FontStyles.Normal, Color.white);
            AnchorBottomLeft(result.TimeText.rectTransform, 108f, 30f, 240f, 48f);
            result.TimeText.alignment = TextAlignmentOptions.MidlineLeft;

            result.SpeedButton = CreateButton(bottom.transform, "SpeedButton", "1x", new Color(0.12f, 0.14f, 0.17f, 0.94f));
            AnchorBottomRight(result.SpeedButton.GetComponent<RectTransform>(), -176f, 26f, 108f, 54f);
            result.SpeedText = result.SpeedButton.GetComponentInChildren<TMP_Text>(true);
            result.SpeedText.name = "SpeedText";
            result.SpeedText.fontSize = 20f;

            result.QualityButton = CreateButton(bottom.transform, "QualityButton", "自动", new Color(0.12f, 0.14f, 0.17f, 0.94f));
            AnchorBottomRight(result.QualityButton.GetComponent<RectTransform>(), -36f, 26f, 118f, 54f);
            result.QualityText = result.QualityButton.GetComponentInChildren<TMP_Text>(true);
            result.QualityText.name = "QualityText";
            result.QualityText.fontSize = 20f;

            result.QualityMenuRoot = CreatePanel(result.ChromeRoot, "QualityMenuRoot", new Color(0.035f, 0.04f, 0.05f, 0.98f)).rectTransform;
            AnchorBottomRight(result.QualityMenuRoot, -36f, 150f, 188f, 0f);
            var menuLayout = result.QualityMenuRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            menuLayout.padding = new RectOffset(8, 8, 8, 8);
            menuLayout.childControlWidth = true;
            menuLayout.childControlHeight = true;
            menuLayout.childForceExpandWidth = true;
            menuLayout.childForceExpandHeight = false;
            var menuFitter = result.QualityMenuRoot.gameObject.AddComponent<ContentSizeFitter>();
            menuFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            result.QualityOptionsRoot = CreateRect(result.QualityMenuRoot, "QualityOptionsRoot");
            var optionsLayout = result.QualityOptionsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            optionsLayout.spacing = 2f;
            optionsLayout.childControlWidth = true;
            optionsLayout.childControlHeight = true;
            optionsLayout.childForceExpandWidth = true;
            optionsLayout.childForceExpandHeight = false;
            var optionsFitter = result.QualityOptionsRoot.gameObject.AddComponent<ContentSizeFitter>();
            optionsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            result.QualityOptionTemplate = CreateButton(
                result.QualityOptionsRoot,
                "QualityOptionTemplate",
                "1080P",
                new Color(0.08f, 0.09f, 0.11f, 1f));
            result.QualityOptionTemplate.GetComponent<RectTransform>().sizeDelta = new Vector2(172f, 42f);
            var optionLayout = result.QualityOptionTemplate.gameObject.AddComponent<LayoutElement>();
            optionLayout.preferredHeight = 42f;
            result.QualityOptionTemplate.gameObject.SetActive(false);
            result.QualityMenuRoot.gameObject.SetActive(false);
            return result;
        }

        private static StoryControls CreateStoryControls(Transform root, Transform mediaLayer)
        {
            var result = new StoryControls();
            result.ImageOutput = CreateRawImage(mediaLayer, "ImageOutput", Color.white);
            Stretch(result.ImageOutput.rectTransform);
            result.ImageOutput.raycastTarget = false;
            result.ImageOutput.gameObject.SetActive(false);

            var dialogue = CreatePanel(root, "DialogueRoot", new Color(0.035f, 0.045f, 0.055f, 0.88f));
            AnchorBottomStretch(dialogue.rectTransform, 224f, 54f, 54f);
            result.DialogueRoot = dialogue.rectTransform;

            result.SpeakerText = CreateText(dialogue.transform, "SpeakerText", "旁白", 26, FontStyles.Bold, new Color(1f, 0.82f, 0.45f, 1f));
            AnchorTopStretch(result.SpeakerText.rectTransform, 46f, 26f, 26f);
            result.SpeakerText.alignment = TextAlignmentOptions.MidlineLeft;

            result.BodyText = CreateText(dialogue.transform, "BodyText", string.Empty, 28, FontStyles.Normal, Color.white);
            Stretch(result.BodyText.rectTransform, 26f, 58f, 26f, 64f);
            result.BodyText.alignment = TextAlignmentOptions.TopLeft;
            result.BodyText.enableWordWrapping = true;

            result.ContinueButton = CreateButton(dialogue.transform, "ContinueButton", "继续", new Color(0.17f, 0.22f, 0.28f, 0.96f));
            AnchorBottomRight(result.ContinueButton.GetComponent<RectTransform>(), -24f, 18f, 132f, 44f);

            result.ChoiceRoot = CreateRect(dialogue.transform, "ChoiceRoot");
            Stretch(result.ChoiceRoot, 24f, 154f, 180f, 16f);
            var layout = result.ChoiceRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            for (var i = 0; i < DefaultChoiceButtonCount; i++)
            {
                var choice = CreateButton(result.ChoiceRoot, $"ChoiceButton{i}", "选项", new Color(0.22f, 0.28f, 0.36f, 0.96f));
                choice.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 44f);
                var choiceLayout = choice.gameObject.AddComponent<LayoutElement>();
                choiceLayout.preferredWidth = 220f;
                choiceLayout.preferredHeight = 44f;
                choice.gameObject.SetActive(false);
            }

            result.ErrorText = CreateText(root, "ErrorText", string.Empty, 20, FontStyles.Normal, new Color(1f, 0.35f, 0.32f, 1f));
            AnchorTopStretch(result.ErrorText.rectTransform, 48f, 112f, 24f);
            result.ErrorText.alignment = TextAlignmentOptions.MidlineLeft;
            result.ErrorText.gameObject.SetActive(false);

            var loading = CreatePanel(root, "LoadingRoot", new Color(0f, 0f, 0f, 0.82f));
            Stretch(loading.rectTransform);
            result.LoadingRoot = loading.rectTransform;
            result.LoadingSpinner = CreateRect(loading.transform, "LoadingSpinner");
            result.LoadingSpinner.anchorMin = result.LoadingSpinner.anchorMax = new Vector2(0.5f, 0.5f);
            result.LoadingSpinner.pivot = new Vector2(0.5f, 0.5f);
            result.LoadingSpinner.sizeDelta = new Vector2(72f, 72f);
            for (var i = 0; i < 8; i++)
            {
                var angle = i * 45f;
                var bar = CreatePanel(result.LoadingSpinner, $"Bar{i}", new Color(0.3f, 0.85f, 0.95f, Mathf.Lerp(0.2f, 1f, (i + 1f) / 8f)));
                bar.rectTransform.sizeDelta = new Vector2(6f, 18f);
                bar.rectTransform.anchoredPosition = Quaternion.Euler(0f, 0f, -angle) * new Vector2(0f, 28f);
                bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }

            result.LoadingRoot.gameObject.SetActive(false);
            return result;
        }

        private static List<Binding> CreateVideoBindings(VideoControls controls)
        {
            return new List<Binding>
            {
                new Binding("PlaybackRoot", controls.PlaybackRoot),
                new Binding("VideoOutput", controls.VideoOutput),
                new Binding("ChromeRoot", controls.ChromeRoot),
                new Binding("ToggleChromeButton", controls.ToggleChromeButton),
                new Binding("BackButton", controls.BackButton),
                new Binding("TitleText", controls.TitleText),
                new Binding("PlayPauseButton", controls.PlayPauseButton),
                new Binding("PlayPauseText", controls.PlayPauseText),
                new Binding("TimeText", controls.TimeText),
                new Binding("ProgressRoot", controls.ProgressRoot),
                new Binding("ProgressSlider", controls.ProgressSlider),
                new Binding("SpeedButton", controls.SpeedButton),
                new Binding("SpeedText", controls.SpeedText),
                new Binding("QualityButton", controls.QualityButton),
                new Binding("QualityText", controls.QualityText),
                new Binding("QualityMenuRoot", controls.QualityMenuRoot),
                new Binding("QualityOptionsRoot", controls.QualityOptionsRoot),
                new Binding("QualityOptionTemplate", controls.QualityOptionTemplate)
            };
        }

        private static void AddStoryBindings(ICollection<Binding> bindings, StoryControls controls)
        {
            bindings.Add(new Binding("ImageOutput", controls.ImageOutput));
            bindings.Add(new Binding("DialogueRoot", controls.DialogueRoot));
            bindings.Add(new Binding("SpeakerText", controls.SpeakerText));
            bindings.Add(new Binding("BodyText", controls.BodyText));
            bindings.Add(new Binding("ErrorText", controls.ErrorText));
            bindings.Add(new Binding("ContinueButton", controls.ContinueButton));
            bindings.Add(new Binding("ChoiceRoot", controls.ChoiceRoot));
            bindings.Add(new Binding("LoadingRoot", controls.LoadingRoot));
            bindings.Add(new Binding("LoadingSpinner", controls.LoadingSpinner));
        }

        private static GameObject CreateRoot(string name)
        {
            var root = new GameObject(
                TempRootName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(UIDocument));
            root.name = name;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Stretch(root.GetComponent<RectTransform>());
            return root;
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private static Image CreatePanel(Transform parent, string name, Color color)
        {
            var image = CreateRect(parent, name).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static RawImage CreateRawImage(Transform parent, string name, Color color)
        {
            var image = CreateRect(parent, name).gameObject.AddComponent<RawImage>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string text,
            float fontSize,
            FontStyles style,
            Color color)
        {
            var component = CreateRect(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            component.text = text;
            component.fontSize = fontSize;
            component.fontStyle = style;
            component.color = color;
            component.raycastTarget = false;
            return component;
        }

        private static Button CreateButton(Transform parent, string name, string text, Color color)
        {
            var rect = CreateRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var label = CreateText(rect, "Label", text, 22f, FontStyles.Normal, Color.white);
            Stretch(label.rectTransform, 8f, 6f, 8f, 6f);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static Button CreateTransparentButton(Transform parent, string name)
        {
            return CreateButton(parent, name, string.Empty, Color.clear);
        }

        private static Slider CreateSlider(Transform parent, string name)
        {
            var rect = CreateRect(parent, name);
            var slider = rect.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            var background = CreatePanel(rect, "Background", new Color(1f, 1f, 1f, 0.28f));
            Stretch(background.rectTransform, 0f, 10f, 0f, 10f);
            var fillArea = CreateRect(rect, "FillArea");
            Stretch(fillArea, 4f, 10f, 4f, 10f);
            var fill = CreatePanel(fillArea, "Fill", new Color(0.95f, 0.3f, 0.55f, 1f));
            Stretch(fill.rectTransform);
            var handleArea = CreateRect(rect, "HandleArea");
            Stretch(handleArea, 4f, 0f, 4f, 0f);
            var handle = CreatePanel(handleArea, "Handle", Color.white);
            handle.rectTransform.sizeDelta = new Vector2(16f, 20f);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            return slider;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.localScale = Vector3.one;
        }

        private static void AnchorTopStretch(RectTransform rect, float height, float left = 0f, float right = 0f)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -height);
            rect.offsetMax = new Vector2(-right, 0f);
        }

        private static void AnchorBottomStretch(RectTransform rect, float height, float left = 0f, float right = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.right;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left, 0f);
            rect.offsetMax = new Vector2(-right, height);
        }

        private static void AnchorTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
        }

        private static void AnchorBottomLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
        }

        private static void AnchorBottomRight(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.right;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
        }

        private static void AnchorMiddleLeft(RectTransform rect, float x, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, 0f);
        }

        private static void AnchorMiddleRight(RectTransform rect, float x, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, 0f);
        }

        private static void AnchorBottomCenter(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
        }

        private static void AssignDocumentBindings(
            UIDocument document,
            RectTransform fullScreenRoot,
            int layerOrder,
            params Binding[] bindings)
        {
            var serialized = new SerializedObject(document);
            serialized.FindProperty("fullScreenRoot").objectReferenceValue = fullScreenRoot;
            serialized.FindProperty("layerOrder").intValue = layerOrder;
            serialized.FindProperty("m_CacheEnabled").boolValue = false;
            var mappings = serialized.FindProperty("mappings");
            mappings.arraySize = bindings.Length;
            for (var i = 0; i < bindings.Length; i++)
            {
                var mapping = mappings.GetArrayElementAtIndex(i);
                mapping.FindPropertyRelative("Name").stringValue = bindings[i].Name;
                mapping.FindPropertyRelative("Target").objectReferenceValue = bindings[i].Component.gameObject;
                var components = mapping.FindPropertyRelative("Components");
                components.arraySize = 1;
                components.GetArrayElementAtIndex(0).objectReferenceValue = bindings[i].Component;
            }

            serialized.FindProperty("localizedTexts").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string SavePrefab(GameObject root, string prefabPath)
        {
            if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
            {
                throw new IOException($"Failed to create prefab: {prefabPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Player window prefab generated: {prefabPath}");
            return prefabPath;
        }

        private static void ValidatePrefabPath(string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(prefabPath) || !prefabPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException("Prefab path must be a project-relative Assets path.", nameof(prefabPath));
            }
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var segments = folder.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        private static void DestroyTemporaryRoot()
        {
            var oldRoot = GameObject.Find(TempRootName);
            if (oldRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(oldRoot);
            }
        }
    }
}
