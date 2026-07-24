using System;
using System.IO;
using GameDeveloperKit.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.StoryEditor.UI
{
    public static class PlayerViewPrefabBuilder
    {
        private readonly struct VideoSeekElement
        {
            public VideoSeekElement(RectTransform root, Slider slider, TMP_Text timeText, Button pauseButton)
            {
                Root = root;
                Slider = slider;
                TimeText = timeText;
                PauseButton = pauseButton;
            }

            public RectTransform Root { get; }

            public Slider Slider { get; }

            public TMP_Text TimeText { get; }

            public Button PauseButton { get; }
        }

        private readonly struct VideoQualityElement
        {
            public VideoQualityElement(RectTransform root, Button button, TMP_Text label)
            {
                Root = root;
                Button = button;
                Label = label;
            }

            public RectTransform Root { get; }

            public Button Button { get; }

            public TMP_Text Label { get; }
        }

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

        private const string PrefabPath = "Assets/Bundles/Playback/PlaybackView.prefab";
        private const string TempRootName = "__PlaybackViewPrefabBuilder";
        private const int DefaultChoiceButtonCount = 4;

        [MenuItem("GameDeveloperKit/剧情编辑/生成默认播放器")]
        public static void BuildPrefabFromMenu()
        {
            BuildPrefab();
        }

        public static string BuildPrefab()
        {
            return BuildPrefab(PrefabPath);
        }

        internal static string BuildPrefab(string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                throw new ArgumentException("Prefab path cannot be empty.", nameof(prefabPath));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
            var oldRoot = GameObject.Find(TempRootName);
            if (oldRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(oldRoot);
            }

            var root = CreateRoot();
            try
            {
                var document = root.GetComponent<UIDocument>();
                var playbackRoot = CreateChild(root.transform, "PlaybackRoot");
                var mediaLayer = CreateRect(
                    root.transform,
                    "MediaLayer",
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero);
                var imageOutput = CreateRawImage(mediaLayer, "ImageOutput", Color.white);
                Stretch(imageOutput.rectTransform, 0f, 0f, 0f, 0f);
                var videoOutput = CreateRawImage(mediaLayer, "VideoOutput", Color.white);
                Stretch(videoOutput.rectTransform, 0f, 0f, 0f, 0f);
                videoOutput.gameObject.SetActive(false);

                var dialoguePanel = CreatePanel(
                    root.transform,
                    "DialoguePanel",
                    new Color(0.04f, 0.05f, 0.06f, 0.86f));
                Anchor(
                    dialoguePanel.rectTransform,
                    new Vector2(0.05f, 0f),
                    new Vector2(0.95f, 0f),
                    new Vector2(0.5f, 0f));
                dialoguePanel.rectTransform.sizeDelta = new Vector2(0f, 220f);
                dialoguePanel.rectTransform.anchoredPosition = new Vector2(0f, 36f);

                var speakerText = CreateText(
                    dialoguePanel.transform,
                    "SpeakerText",
                    "旁白",
                    26,
                    FontStyles.Bold,
                    new Color(0.95f, 0.86f, 0.62f, 1f));
                Anchor(
                    speakerText.rectTransform,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f));
                speakerText.rectTransform.sizeDelta = new Vector2(300f, 42f);
                speakerText.rectTransform.anchoredPosition = new Vector2(28f, -22f);

                var bodyText = CreateText(
                    dialoguePanel.transform,
                    "BodyText",
                    "剧情文本",
                    28,
                    FontStyles.Normal,
                    new Color(0.94f, 0.95f, 0.96f, 1f));
                Anchor(
                    bodyText.rectTransform,
                    new Vector2(0f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0.5f, 0.5f));
                bodyText.rectTransform.offsetMin = new Vector2(28f, 70f);
                bodyText.rectTransform.offsetMax = new Vector2(-28f, -64f);
                bodyText.alignment = TextAlignmentOptions.TopLeft;
                bodyText.enableWordWrapping = true;
                bodyText.overflowMode = TextOverflowModes.Overflow;

                var continueButton = CreateButton(
                    dialoguePanel.transform,
                    "ContinueButton",
                    "继续",
                    new Color(0.17f, 0.22f, 0.28f, 0.96f));
                var continueRect = continueButton.GetComponent<RectTransform>();
                Anchor(
                    continueRect,
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f));
                continueRect.sizeDelta = new Vector2(140f, 42f);
                continueRect.anchoredPosition = new Vector2(-28f, 22f);

                var choiceRoot = CreateRect(
                    dialoguePanel.transform,
                    "ChoiceRoot",
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 26f));
                choiceRoot.sizeDelta = new Vector2(0f, 52f);
                choiceRoot.offsetMin = new Vector2(28f, 16f);
                choiceRoot.offsetMax = new Vector2(-184f, 68f);
                var layout = choiceRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 12f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = false;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = true;
                for (var i = 0; i < DefaultChoiceButtonCount; i++)
                {
                    var choiceButton = CreateButton(
                        choiceRoot,
                        "ChoiceButton" + i,
                        "选项",
                        new Color(0.22f, 0.28f, 0.36f, 0.96f));
                    choiceButton.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 44f);
                    var choiceLayout = choiceButton.gameObject.AddComponent<LayoutElement>();
                    choiceLayout.preferredWidth = 220f;
                    choiceLayout.preferredHeight = 44f;
                    choiceButton.gameObject.SetActive(false);
                }

                var errorText = CreateText(
                    root.transform,
                    "ErrorText",
                    string.Empty,
                    20,
                    FontStyles.Normal,
                    new Color(1f, 0.42f, 0.38f, 1f));
                Anchor(
                    errorText.rectTransform,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(0.5f, 1f));
                errorText.rectTransform.sizeDelta = new Vector2(0f, 48f);
                errorText.rectTransform.offsetMin = new Vector2(24f, -68f);
                errorText.rectTransform.offsetMax = new Vector2(-24f, -20f);
                errorText.alignment = TextAlignmentOptions.TopLeft;
                errorText.gameObject.SetActive(false);

                var videoSeek = CreateVideoSeekSurface(root.transform);
                var videoQuality = CreateVideoQualitySurface(root.transform);
                AssignDocumentBindings(
                    document,
                    root.GetComponent<RectTransform>(),
                    new Binding("PlaybackRoot", playbackRoot.transform),
                    new Binding("VideoOutput", videoOutput),
                    new Binding("ImageOutput", imageOutput),
                    new Binding("VideoSeekRoot", videoSeek.Root),
                    new Binding("VideoSeekSlider", videoSeek.Slider),
                    new Binding("VideoSeekTimeText", videoSeek.TimeText),
                    new Binding("VideoSeekPauseButton", videoSeek.PauseButton),
                    new Binding("VideoQualityRoot", videoQuality.Root),
                    new Binding("VideoQualityButton", videoQuality.Button),
                    new Binding("VideoQualityText", videoQuality.Label),
                    new Binding("DialogueRoot", dialoguePanel.rectTransform),
                    new Binding("SpeakerText", speakerText),
                    new Binding("BodyText", bodyText),
                    new Binding("ErrorText", errorText),
                    new Binding("ContinueButton", continueButton),
                    new Binding("ChoiceRoot", choiceRoot));

                root.name = "PlaybackView";
                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                {
                    throw new IOException($"Failed to create prefab: {prefabPath}");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"Story playback prefab generated: {prefabPath}");
                return prefabPath;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateRoot()
        {
            var root = new GameObject(
                TempRootName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(UIDocument));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            canvas.sortingOrder = 1000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var rect = root.GetComponent<RectTransform>();
            rect.localScale = Vector3.one;
            Anchor(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return root;
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static Image CreatePanel(Transform parent, string name, Color color)
        {
            var image = CreateRect(
                parent,
                name,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static RawImage CreateRawImage(Transform parent, string name, Color color)
        {
            var image = CreateRect(
                parent,
                name,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero).gameObject.AddComponent<RawImage>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string text,
            int fontSize,
            FontStyles fontStyle,
            Color color)
        {
            var component = CreateRect(
                parent,
                name,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero).gameObject.AddComponent<TextMeshProUGUI>();
            component.text = text;
            component.fontSize = fontSize;
            component.fontStyle = fontStyle;
            component.alignment = TextAlignmentOptions.Left;
            component.color = color;
            component.raycastTarget = false;
            return component;
        }

        private static Button CreateButton(Transform parent, string name, string text, Color color)
        {
            var rect = CreateRect(
                parent,
                name,
                Vector2.zero,
                Vector2.zero,
                new Vector2(0.5f, 0.5f),
                Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var label = CreateText(rect, "Label", text, 22, FontStyles.Bold, Color.white);
            Stretch(label.rectTransform, 14f, 8f, 14f, 8f);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static VideoSeekElement CreateVideoSeekSurface(Transform parent)
        {
            var root = CreatePanel(
                parent,
                "VideoSeek",
                new Color(0.04f, 0.05f, 0.06f, 0.82f)).rectTransform;
            Anchor(
                root,
                new Vector2(0.05f, 0f),
                new Vector2(0.95f, 0f),
                new Vector2(0.5f, 0f));
            root.sizeDelta = new Vector2(0f, 56f);
            root.anchoredPosition = new Vector2(0f, 282f);

            var pauseButton = CreateButton(
                root,
                "PauseButton",
                "暂停",
                new Color(0.18f, 0.24f, 0.3f, 0.96f));
            var pauseRect = pauseButton.GetComponent<RectTransform>();
            Anchor(
                pauseRect,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f));
            pauseRect.sizeDelta = new Vector2(84f, 36f);
            pauseRect.anchoredPosition = new Vector2(24f, 0f);

            var slider = CreateSlider(root, "Slider");
            Stretch(slider.GetComponent<RectTransform>(), 120f, 14f, 156f, 14f);
            var timeText = CreateText(
                root,
                "TimeText",
                "00:00 / 00:00",
                20,
                FontStyles.Normal,
                new Color(0.94f, 0.95f, 0.96f, 1f));
            Anchor(
                timeText.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f));
            timeText.rectTransform.sizeDelta = new Vector2(128f, 28f);
            timeText.rectTransform.anchoredPosition = new Vector2(-24f, 0f);
            timeText.alignment = TextAlignmentOptions.MidlineRight;
            root.gameObject.SetActive(false);
            return new VideoSeekElement(root, slider, timeText, pauseButton);
        }

        private static VideoQualityElement CreateVideoQualitySurface(Transform parent)
        {
            var root = CreatePanel(
                parent,
                "VideoQuality",
                new Color(0.04f, 0.05f, 0.06f, 0.82f)).rectTransform;
            Anchor(root, Vector2.one, Vector2.one, Vector2.one);
            root.sizeDelta = new Vector2(140f, 48f);
            root.anchoredPosition = new Vector2(-24f, -24f);

            var button = CreateButton(
                root,
                "QualityButton",
                "自动",
                new Color(0.18f, 0.24f, 0.3f, 0.96f));
            Stretch(button.GetComponent<RectTransform>(), 4f, 4f, 4f, 4f);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            root.gameObject.SetActive(false);
            return new VideoQualityElement(root, button, label);
        }

        private static Slider CreateSlider(Transform parent, string name)
        {
            var rect = CreateRect(
                parent,
                name,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero);
            rect.sizeDelta = new Vector2(0f, 28f);
            var slider = rect.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;

            var background = CreatePanel(
                rect,
                "Background",
                new Color(0.1f, 0.12f, 0.14f, 0.95f));
            Stretch(background.rectTransform, 0f, 10f, 0f, 10f);
            var fillArea = CreateRect(
                rect,
                "Fill Area",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero);
            Stretch(fillArea, 4f, 10f, 4f, 10f);
            var fill = CreatePanel(fillArea, "Fill", new Color(0.18f, 0.62f, 0.82f, 1f));
            Stretch(fill.rectTransform, 0f, 0f, 0f, 0f);
            var handleArea = CreateRect(
                rect,
                "Handle Slide Area",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero);
            Stretch(handleArea, 4f, 0f, 4f, 0f);
            var handle = CreatePanel(
                handleArea,
                "Handle",
                new Color(0.94f, 0.95f, 0.96f, 1f));
            handle.rectTransform.sizeDelta = new Vector2(18f, 28f);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            return slider;
        }

        private static RectTransform CreateRect(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            Anchor(rect, anchorMin, anchorMax, pivot);
            rect.anchoredPosition = anchoredPosition;
            return rect;
        }

        private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            Anchor(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
        }

        private static void AssignDocumentBindings(
            UIDocument document,
            RectTransform fullScreenRoot,
            params Binding[] bindings)
        {
            var serializedDocument = new SerializedObject(document);
            serializedDocument.FindProperty("fullScreenRoot").objectReferenceValue = fullScreenRoot;
            serializedDocument.FindProperty("layerOrder").intValue = 500;
            var mappings = serializedDocument.FindProperty("mappings");
            mappings.arraySize = bindings.Length;
            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                var mapping = mappings.GetArrayElementAtIndex(i);
                mapping.FindPropertyRelative("Name").stringValue = binding.Name;
                mapping.FindPropertyRelative("Target").objectReferenceValue = binding.Component.gameObject;
                var components = mapping.FindPropertyRelative("Components");
                components.arraySize = 1;
                components.GetArrayElementAtIndex(0).objectReferenceValue = binding.Component;
            }

            serializedDocument.FindProperty("localizedTexts").arraySize = 0;
            serializedDocument.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
