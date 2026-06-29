using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 默认解锁命令执行器。
    /// </summary>
    public sealed class StoryUnlockCommandHandler : IStoryCommandHandler
    {
        private readonly Func<RectTransform> m_CustomRootProvider;
        private readonly Func<IUnlockStateProvider> m_UnlockStateProvider;

        /// <summary>
        /// 初始化默认解锁命令执行器。
        /// </summary>
        /// <param name="customRootProvider">自定义 UI 根节点提供器。</param>
        /// <param name="unlockStateProvider">解锁状态提供器。</param>
        public StoryUnlockCommandHandler(
            Func<RectTransform> customRootProvider,
            Func<IUnlockStateProvider> unlockStateProvider)
        {
            m_CustomRootProvider = customRootProvider ?? throw new ArgumentNullException(nameof(customRootProvider));
            m_UnlockStateProvider = unlockStateProvider ?? throw new ArgumentNullException(nameof(unlockStateProvider));
        }

        /// <inheritdoc />
        public bool CanHandle(StoryCommand command)
        {
            return command != null && string.Equals(command.Name, StoryInteractionCommandNames.Unlock, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public IStoryCommandHandle Execute(StoryCommand command, StoryRuntimeContext context)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var unlockId = GetRequiredArgument(command, StoryInteractionCommandNames.UnlockIdArgument);
            var puzzleType = GetRequiredArgument(command, StoryInteractionCommandNames.PuzzleTypeArgument);
            if (IsSupportedPuzzleType(puzzleType) is false)
            {
                throw new GameException($"Story unlock puzzle type is invalid. command:{command.CommandId} puzzleType:{puzzleType}");
            }

            var promptTextKey = GetRequiredArgument(command, StoryInteractionCommandNames.PromptTextKeyArgument);
            var provider = m_UnlockStateProvider();
            if (provider == null)
            {
                throw new GameException($"Story unlock state provider is missing. command:{command.CommandId}");
            }

            var handle = new StoryCommandHandle(command);
            if (provider.TryGetUnlockState(unlockId, out var unlocked) && unlocked)
            {
                handle.Complete(StoryInteractionCommandNames.SuccessOutcome);
                return handle;
            }

            var root = m_CustomRootProvider();
            if (root == null)
            {
                throw new GameException($"Story custom root surface is missing. command:{command.CommandId}");
            }

            StoryUnlockOverlaySession.Create(root, handle, provider, unlockId, puzzleType, promptTextKey);
            return handle;
        }

        private static string GetRequiredArgument(StoryCommand command, string key)
        {
            var value = command.Arguments.GetString(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new GameException($"Story unlock command argument is missing. command:{command.CommandId} argument:{key}");
            }

            return value;
        }

        private static bool IsSupportedPuzzleType(string puzzleType)
        {
            return string.Equals(puzzleType, StoryInteractionCommandNames.PuzzleTypeLineConnect, StringComparison.Ordinal) ||
                   string.Equals(puzzleType, StoryInteractionCommandNames.PuzzleTypeNodeUnlock, StringComparison.Ordinal) ||
                   string.Equals(puzzleType, StoryInteractionCommandNames.PuzzleTypeCustom, StringComparison.Ordinal);
        }
    }

    internal sealed class StoryUnlockOverlaySession : MonoBehaviour
    {
        private StoryCommandHandle m_Handle;
        private IUnlockStateProvider m_StateProvider;
        private TMP_Text m_PromptText;
        private TMP_Text m_StatusText;
        private Button m_UnlockButton;
        private Button m_FailButton;
        private Button m_CancelButton;
        private string m_UnlockId;
        private string m_PuzzleType;

        public static StoryUnlockOverlaySession Create(
            RectTransform parent,
            StoryCommandHandle handle,
            IUnlockStateProvider stateProvider,
            string unlockId,
            string puzzleType,
            string promptTextKey)
        {
            var root = new GameObject("StoryUnlockOverlay", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var session = root.AddComponent<StoryUnlockOverlaySession>();
            session.Initialize(handle, stateProvider, unlockId, puzzleType, promptTextKey);
            return session;
        }

        private void Initialize(
            StoryCommandHandle handle,
            IUnlockStateProvider stateProvider,
            string unlockId,
            string puzzleType,
            string promptTextKey)
        {
            m_Handle = handle ?? throw new ArgumentNullException(nameof(handle));
            m_StateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
            m_UnlockId = unlockId;
            m_PuzzleType = puzzleType;
            BuildOverlay(promptTextKey);
            Refresh();

            m_Handle.Completed += OnHandleFinished;
            m_Handle.Canceled += OnHandleFinished;
            m_Handle.Stopped += OnHandleFinished;
            m_Handle.Failed += OnHandleFinished;
        }

        private void OnUnlockClicked()
        {
            if (StoryMediaCommandUtility.IsTerminal(m_Handle))
            {
                return;
            }

            if (m_StateProvider.TrySetUnlockState(m_UnlockId, true, out _) is false)
            {
                m_Handle.Complete(StoryInteractionCommandNames.FailOutcome);
                return;
            }

            m_Handle.Complete(StoryInteractionCommandNames.SuccessOutcome);
        }

        private void OnFailClicked()
        {
            if (StoryMediaCommandUtility.IsTerminal(m_Handle))
            {
                return;
            }

            m_Handle.Complete(StoryInteractionCommandNames.FailOutcome);
        }

        private void BuildOverlay(string promptTextKey)
        {
            var panel = CreateRect(transform, "Panel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            panel.sizeDelta = new Vector2(620f, 190f);
            panel.anchoredPosition = new Vector2(0f, 92f);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.04f, 0.05f, 0.06f, 0.94f);

            m_PromptText = CreateText(panel, "Prompt", promptTextKey, 28, new Vector2(24f, -20f), new Vector2(-24f, -62f));
            m_StatusText = CreateText(panel, "Status", string.Empty, 20, new Vector2(24f, -68f), new Vector2(-24f, -104f));

            m_UnlockButton = CreateButton(panel, "UnlockButton", "解锁", new Color(0.18f, 0.54f, 0.34f, 0.96f));
            var unlockRect = (RectTransform)m_UnlockButton.transform;
            unlockRect.anchorMin = new Vector2(0f, 0f);
            unlockRect.anchorMax = new Vector2(0f, 0f);
            unlockRect.pivot = new Vector2(0f, 0f);
            unlockRect.sizeDelta = new Vector2(150f, 52f);
            unlockRect.anchoredPosition = new Vector2(24f, 24f);
            m_UnlockButton.onClick.AddListener(OnUnlockClicked);

            m_FailButton = CreateButton(panel, "FailButton", "失败", new Color(0.45f, 0.18f, 0.18f, 0.96f));
            var failRect = (RectTransform)m_FailButton.transform;
            failRect.anchorMin = new Vector2(0f, 0f);
            failRect.anchorMax = new Vector2(0f, 0f);
            failRect.pivot = new Vector2(0f, 0f);
            failRect.sizeDelta = new Vector2(150f, 52f);
            failRect.anchoredPosition = new Vector2(190f, 24f);
            m_FailButton.onClick.AddListener(OnFailClicked);

            m_CancelButton = CreateButton(panel, "CancelButton", "取消", new Color(0.22f, 0.28f, 0.36f, 0.96f));
            var cancelRect = (RectTransform)m_CancelButton.transform;
            cancelRect.anchorMin = new Vector2(0f, 0f);
            cancelRect.anchorMax = new Vector2(0f, 0f);
            cancelRect.pivot = new Vector2(0f, 0f);
            cancelRect.sizeDelta = new Vector2(150f, 52f);
            cancelRect.anchoredPosition = new Vector2(356f, 24f);
            m_CancelButton.onClick.AddListener(OnFailClicked);
        }

        private void Refresh()
        {
            if (m_PromptText != null && string.IsNullOrWhiteSpace(m_PromptText.text))
            {
                m_PromptText.text = m_UnlockId;
            }

            if (m_StatusText != null)
            {
                m_StatusText.text = $"{m_UnlockId}  {m_PuzzleType}";
            }
        }

        private void OnHandleFinished(IStoryCommandHandle handle)
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (m_Handle != null)
            {
                m_Handle.Completed -= OnHandleFinished;
                m_Handle.Canceled -= OnHandleFinished;
                m_Handle.Stopped -= OnHandleFinished;
                m_Handle.Failed -= OnHandleFinished;
                m_Handle = null;
            }

            if (m_UnlockButton != null)
            {
                m_UnlockButton.onClick.RemoveListener(OnUnlockClicked);
                m_UnlockButton = null;
            }

            if (m_FailButton != null)
            {
                m_FailButton.onClick.RemoveListener(OnFailClicked);
                m_FailButton = null;
            }

            if (m_CancelButton != null)
            {
                m_CancelButton.onClick.RemoveListener(OnFailClicked);
                m_CancelButton = null;
            }

            if (gameObject != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }
            }
        }

        private static RectTransform CreateRect(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rect = (RectTransform)gameObject.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static TMP_Text CreateText(
            RectTransform parent,
            string name,
            string value,
            int fontSize,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            var rect = CreateRect(parent, name, Vector2.zero, Vector2.one, new Vector2(0f, 1f));
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value ?? string.Empty;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Left;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Color color)
        {
            var rect = CreateRect(parent, name, Vector2.zero, Vector2.zero, Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var labelText = CreateText(rect, "Label", label, 20, Vector2.zero, Vector2.zero);
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.rectTransform.offsetMin = Vector2.zero;
            labelText.rectTransform.offsetMax = Vector2.zero;
            return button;
        }
    }
}
