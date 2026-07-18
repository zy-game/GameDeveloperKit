using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.Story
{
    public sealed partial class StoryPlayerView
    {
        public static StoryPlayerView CreateDefault(Transform parent = null)
        {
            var rootObject = new GameObject("StoryPlayerView", typeof(RectTransform));
            rootObject.SetActive(false);

            var root = (RectTransform)rootObject.transform;
            root.SetParent(parent, false);
            Stretch(root, 0f, 0f, 0f, 0f);

            var view = rootObject.AddComponent<StoryPlayerView>();
            var playbackRoot = new GameObject("PlaybackRoot").transform;
            playbackRoot.SetParent(root, false);

            var mediaLayer = CreateRect(root, "MediaLayer", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
            Stretch(mediaLayer, 0f, 0f, 0f, 0f);

            var imageOutput = CreateRawImage(mediaLayer, "ImageOutput", Color.white);
            Stretch(imageOutput.rectTransform, 0f, 0f, 0f, 0f);
            imageOutput.gameObject.SetActive(false);

            var videoOutput = CreateRawImage(mediaLayer, "VideoOutput", Color.white);
            Stretch(videoOutput.rectTransform, 0f, 0f, 0f, 0f);
            videoOutput.gameObject.SetActive(false);

            var dialoguePanel = CreatePanel(root, "DialoguePanel", new Color(0.04f, 0.05f, 0.06f, 0.86f));
            Anchor(dialoguePanel.rectTransform, new Vector2(0.05f, 0f), new Vector2(0.95f, 0f), new Vector2(0.5f, 0f));
            dialoguePanel.rectTransform.sizeDelta = new Vector2(0f, 220f);
            dialoguePanel.rectTransform.anchoredPosition = new Vector2(0f, 36f);

            var speakerText = CreateText(dialoguePanel.transform, "SpeakerText", "旁白", 26f, FontStyles.Bold, new Color(0.95f, 0.86f, 0.62f, 1f));
            Anchor(speakerText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            speakerText.rectTransform.sizeDelta = new Vector2(300f, 42f);
            speakerText.rectTransform.anchoredPosition = new Vector2(28f, -22f);

            var bodyText = CreateText(dialoguePanel.transform, "BodyText", "剧情文本", 28f, FontStyles.Normal, new Color(0.94f, 0.95f, 0.96f, 1f));
            Anchor(bodyText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            bodyText.rectTransform.offsetMin = new Vector2(28f, 70f);
            bodyText.rectTransform.offsetMax = new Vector2(-28f, -64f);
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.enableWordWrapping = true;
            bodyText.overflowMode = TextOverflowModes.Overflow;

            var continueButton = CreateButton(dialoguePanel.transform, "ContinueButton", "继续", new Color(0.17f, 0.22f, 0.28f, 0.96f));
            var continueRect = continueButton.GetComponent<RectTransform>();
            Anchor(continueRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            continueRect.sizeDelta = new Vector2(140f, 42f);
            continueRect.anchoredPosition = new Vector2(-28f, 22f);

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

            var errorText = CreateText(root, "ErrorText", string.Empty, 20f, FontStyles.Normal, new Color(1f, 0.42f, 0.38f, 1f));
            Anchor(errorText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            errorText.rectTransform.sizeDelta = new Vector2(0f, 48f);
            errorText.rectTransform.offsetMin = new Vector2(24f, -68f);
            errorText.rectTransform.offsetMax = new Vector2(-24f, -20f);
            errorText.alignment = TextAlignmentOptions.TopLeft;
            errorText.gameObject.SetActive(false);

            view.m_PlaybackRoot = playbackRoot;
            view.m_VideoOutput = videoOutput;
            view.m_ImageOutput = imageOutput;
            view.m_SpeakerText = speakerText;
            view.m_BodyText = bodyText;
            view.m_ErrorText = errorText;
            view.m_ContinueButton = continueButton;
            view.m_ChoiceRoot = choiceRoot;
            view.m_ChoiceButtonTemplate = choiceButtonTemplate;
            view.EnsureDefaultVideoSeekSurface();

            rootObject.SetActive(true);
            return view;
        }

        private static Image CreatePanel(Transform parent, string name, Color color)
        {
            var rect = CreateRect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static RawImage CreateRawImage(Transform parent, string name, Color color)
        {
            var rect = CreateRect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
            var rawImage = rect.gameObject.AddComponent<RawImage>();
            rawImage.color = color;
            rawImage.raycastTarget = false;
            return rawImage;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string text,
            float fontSize,
            FontStyles fontStyle,
            Color color)
        {
            var rect = CreateRect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            var font = GetDefaultTextFont();
            if (font != null)
            {
                label.font = font;
            }

            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.alignment = TextAlignmentOptions.Left;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(Transform parent, string name, string text, Color color)
        {
            var rect = CreateRect(parent, name, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var label = CreateText(rect.transform, "Label", text, 22f, FontStyles.Bold, Color.white);
            Stretch(label.rectTransform, 14f, 8f, 14f, 8f);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static Slider CreateSlider(Transform parent, string name)
        {
            var rect = CreateRect(parent, name, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
            rect.sizeDelta = new Vector2(0f, 28f);

            var slider = rect.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;

            var background = CreatePanel(rect, "Background", new Color(0.1f, 0.12f, 0.14f, 0.95f));
            Stretch(background.rectTransform, 0f, 10f, 0f, 10f);

            var fillArea = CreateRect(rect, "Fill Area", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
            Stretch(fillArea, 4f, 10f, 4f, 10f);

            var fill = CreatePanel(fillArea, "Fill", new Color(0.18f, 0.62f, 0.82f, 1f));
            Stretch(fill.rectTransform, 0f, 0f, 0f, 0f);

            var handleArea = CreateRect(rect, "Handle Slide Area", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
            Stretch(handleArea, 4f, 0f, 4f, 0f);

            var handle = CreatePanel(handleArea, "Handle", new Color(0.94f, 0.95f, 0.96f, 1f));
            handle.rectTransform.sizeDelta = new Vector2(18f, 28f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            return slider;
        }

        private static TMP_FontAsset GetDefaultTextFont()
        {
            if (s_DefaultTextFont == null)
            {
                s_DefaultTextFont = Resources.Load<TMP_FontAsset>(DefaultTextFontResourcePath);
            }

            return s_DefaultTextFont;
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
            var rect = (RectTransform)gameObject.transform;
            Anchor(rect, anchorMin, anchorMax, pivot);
            rect.anchoredPosition = anchoredPosition;
            return rect;
        }

        private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            Anchor(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.localScale = Vector3.one;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
        }
    }
}
