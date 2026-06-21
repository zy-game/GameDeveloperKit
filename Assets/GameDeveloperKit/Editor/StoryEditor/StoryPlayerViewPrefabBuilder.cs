using System;
using System.IO;
using GameDeveloperKit.Story;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.StoryEditor
{
    /// <summary>
    /// Story Player UGUI 预制体生成器。
    /// </summary>
    public static class StoryPlayerViewPrefabBuilder
    {
        private const string PrefabPath = "Assets/GameDeveloperKit/Runtime/StoryPresentation.AVPro/StoryPlayerView.prefab";
        private const string TempRootName = "__StoryPlayerViewPrefabBuilder";
        private const string TextMeshProUGUITypeName = "TMPro.TextMeshProUGUI";
        private const string FontStylesTypeName = "TMPro.FontStyles";
        private const string TextAlignmentOptionsTypeName = "TMPro.TextAlignmentOptions";
        private const string TextOverflowModesTypeName = "TMPro.TextOverflowModes";

        [MenuItem("GameDeveloperKit/剧情编辑/生成运行时播放器预制体")]
        public static void BuildPrefabFromMenu()
        {
            BuildPrefab();
        }

        public static string BuildPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

            var oldRoot = GameObject.Find(TempRootName);
            if (oldRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(oldRoot);
            }

            var root = CreateRoot();
            try
            {
                var view = root.GetComponent<StoryPlayerView>();
                var playbackRoot = CreateChild(root.transform, "PlaybackRoot");
                var mediaLayer = CreateRect(root.transform, "MediaLayer", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var imageOutput = CreateRawImage(mediaLayer.transform, "ImageOutput", new Color(1f, 1f, 1f, 1f));
                Stretch(imageOutput.rectTransform, 0f, 0f, 0f, 0f);
                var videoOutput = CreateRawImage(mediaLayer.transform, "VideoOutput", new Color(1f, 1f, 1f, 1f));
                Stretch(videoOutput.rectTransform, 0f, 0f, 0f, 0f);
                videoOutput.gameObject.SetActive(false);

                var dialoguePanel = CreatePanel(root.transform, "DialoguePanel", new Color(0.04f, 0.05f, 0.06f, 0.86f));
                Anchor(dialoguePanel.rectTransform, new Vector2(0.05f, 0f), new Vector2(0.95f, 0f), new Vector2(0.5f, 0f));
                dialoguePanel.rectTransform.sizeDelta = new Vector2(0f, 220f);
                dialoguePanel.rectTransform.anchoredPosition = new Vector2(0f, 36f);

                var speakerText = CreateText(dialoguePanel.transform, "SpeakerText", "旁白", 26, "Bold", new Color(0.95f, 0.86f, 0.62f, 1f));
                Anchor(speakerText.RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                speakerText.RectTransform.sizeDelta = new Vector2(300f, 42f);
                speakerText.RectTransform.anchoredPosition = new Vector2(28f, -22f);

                var bodyText = CreateText(dialoguePanel.transform, "BodyText", "剧情文本", 28, "Normal", new Color(0.94f, 0.95f, 0.96f, 1f));
                Anchor(bodyText.RectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
                bodyText.RectTransform.offsetMin = new Vector2(28f, 70f);
                bodyText.RectTransform.offsetMax = new Vector2(-28f, -64f);
                SetTextAlignment(bodyText.Component, "TopLeft");
                SetProperty(bodyText.Component, "enableWordWrapping", true);
                SetEnumProperty(bodyText.Component, "overflowMode", TextOverflowModesTypeName, "Overflow");

                var continueButton = CreateButton(dialoguePanel.transform, "ContinueButton", "继续", new Color(0.17f, 0.22f, 0.28f, 0.96f));
                Anchor(continueButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
                continueButton.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 42f);
                continueButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-28f, 22f);

                var choiceRoot = CreateRect(dialoguePanel.transform, "ChoiceRoot", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f));
                choiceRoot.sizeDelta = new Vector2(0f, 52f);
                choiceRoot.offsetMin = new Vector2(28f, 16f);
                choiceRoot.offsetMax = new Vector2(-184f, 68f);
                var choiceLayout = choiceRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
                choiceLayout.spacing = 12f;
                choiceLayout.childAlignment = TextAnchor.MiddleLeft;
                choiceLayout.childControlWidth = false;
                choiceLayout.childControlHeight = true;
                choiceLayout.childForceExpandWidth = false;
                choiceLayout.childForceExpandHeight = true;

                var choiceButtonTemplate = CreateButton(choiceRoot.transform, "ChoiceButtonTemplate", "选项", new Color(0.22f, 0.28f, 0.36f, 0.96f));
                choiceButtonTemplate.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 44f);
                var choiceLayoutElement = choiceButtonTemplate.gameObject.AddComponent<LayoutElement>();
                choiceLayoutElement.preferredWidth = 220f;
                choiceLayoutElement.preferredHeight = 44f;
                choiceButtonTemplate.gameObject.SetActive(false);

                var errorText = CreateText(root.transform, "ErrorText", "", 20, "Normal", new Color(1f, 0.42f, 0.38f, 1f));
                Anchor(errorText.RectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
                errorText.RectTransform.sizeDelta = new Vector2(0f, 48f);
                errorText.RectTransform.offsetMin = new Vector2(24f, -68f);
                errorText.RectTransform.offsetMax = new Vector2(-24f, -20f);
                SetTextAlignment(errorText.Component, "TopLeft");
                errorText.Component.gameObject.SetActive(false);

                AssignViewReferences(
                    view,
                    playbackRoot.transform,
                    videoOutput,
                    imageOutput,
                    speakerText.Component,
                    bodyText.Component,
                    errorText.Component,
                    continueButton,
                    choiceRoot.transform,
                    choiceButtonTemplate);

                root.name = "StoryPlayerView";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null)
                {
                    throw new IOException($"Failed to create prefab: {PrefabPath}");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"Story Player prefab generated: {PrefabPath}");
                return PrefabPath;
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
                typeof(StoryPlayerView));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var rect = root.GetComponent<RectTransform>();
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
            var panel = CreateRect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
            var image = panel.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static RawImage CreateRawImage(Transform parent, string name, Color color)
        {
            var rect = CreateRect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
            var rawImage = rect.gameObject.AddComponent<RawImage>();
            rawImage.color = color;
            return rawImage;
        }

        private static TextElement CreateText(
            Transform parent,
            string name,
            string text,
            int fontSize,
            string fontStyle,
            Color color)
        {
            var rect = CreateRect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
            var label = rect.gameObject.AddComponent(ResolveTextMeshProType());
            SetProperty(label, "text", text);
            SetProperty(label, "fontSize", (float)fontSize);
            SetEnumProperty(label, "fontStyle", FontStylesTypeName, fontStyle);
            SetTextAlignment(label, "Left");
            if (label is Graphic graphic)
            {
                graphic.color = color;
                graphic.raycastTarget = false;
            }

            return new TextElement(label, rect);
        }

        private static Button CreateButton(Transform parent, string name, string text, Color color)
        {
            var rect = CreateRect(parent, name, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var label = CreateText(rect.transform, "Label", text, 22, "Bold", Color.white);
            Stretch(label.RectTransform, 14f, 8f, 14f, 8f);
            SetTextAlignment(label.Component, "Center");
            return button;
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

        private static void AssignViewReferences(
            StoryPlayerView view,
            Transform playbackRoot,
            RawImage videoOutput,
            RawImage imageOutput,
            Component speakerText,
            Component bodyText,
            Component errorText,
            Button continueButton,
            Transform choiceRoot,
            Button choiceButtonTemplate)
        {
            var serializedObject = new SerializedObject(view);
            serializedObject.FindProperty("m_PlaybackRoot").objectReferenceValue = playbackRoot;
            serializedObject.FindProperty("m_VideoOutput").objectReferenceValue = videoOutput;
            serializedObject.FindProperty("m_ImageOutput").objectReferenceValue = imageOutput;
            serializedObject.FindProperty("m_SpeakerText").objectReferenceValue = speakerText;
            serializedObject.FindProperty("m_BodyText").objectReferenceValue = bodyText;
            serializedObject.FindProperty("m_ErrorText").objectReferenceValue = errorText;
            serializedObject.FindProperty("m_ContinueButton").objectReferenceValue = continueButton;
            serializedObject.FindProperty("m_ChoiceRoot").objectReferenceValue = choiceRoot;
            serializedObject.FindProperty("m_ChoiceButtonTemplate").objectReferenceValue = choiceButtonTemplate;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Type ResolveTextMeshProType()
        {
            return ResolveType(TextMeshProUGUITypeName);
        }

        private static Type ResolveType(string typeName)
        {
            var type = Type.GetType(typeName + ", Unity.TextMeshPro");
            if (type != null)
            {
                return type;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            throw new InvalidOperationException("TextMesh Pro is required to generate StoryPlayerView prefab.");
        }

        private static void SetTextAlignment(Component component, string value)
        {
            SetEnumProperty(component, "alignment", TextAlignmentOptionsTypeName, value);
        }

        private static void SetEnumProperty(Component component, string propertyName, string enumTypeName, string value)
        {
            var enumType = ResolveType(enumTypeName);
            var enumValue = Enum.Parse(enumType, value);
            SetProperty(component, propertyName, enumValue);
        }

        private static void SetProperty(Component component, string propertyName, object value)
        {
            var property = component.GetType().GetProperty(propertyName);
            if (property == null || property.CanWrite is false)
            {
                return;
            }

            property.SetValue(component, value);
        }

        private readonly struct TextElement
        {
            public TextElement(Component component, RectTransform rectTransform)
            {
                Component = component;
                RectTransform = rectTransform;
            }

            public Component Component { get; }

            public RectTransform RectTransform { get; }
        }
    }
}
