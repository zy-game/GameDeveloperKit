using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Protocol;

namespace GameDeveloperKit.Story.Playback
{
    /// <summary>
    /// Story 媒体命令工具。
    /// </summary>
    public static class MediaCommandUtility
    {
        /// <summary>
        /// 判断命令句柄是否已经进入终态。
        /// </summary>
        /// <param name="handle">命令句柄。</param>
        /// <returns>已结束时返回 true。</returns>
        public static bool IsTerminal(ICommandHandle handle)
        {
            return handle == null ||
                   handle.IsCompleted ||
                   handle.IsCanceled ||
                   handle.IsStopped ||
                   handle.Error != null;
        }

        /// <summary>
        /// 获取默认完成结果。
        /// </summary>
        /// <param name="command">命令。</param>
        /// <returns>命令声明 completed 端口时返回 completed，否则返回 null。</returns>
        public static string GetCompletedOutcome(global::GameDeveloperKit.Story.Model.Command command)
        {
            if (command?.OutcomePorts == null)
            {
                return null;
            }

            for (var i = 0; i < command.OutcomePorts.Count; i++)
            {
                if (string.Equals(command.OutcomePorts[i], MediaCommandNames.CompletedOutcome, StringComparison.Ordinal))
                {
                    return MediaCommandNames.CompletedOutcome;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 默认 QTE 命令执行器。
    /// </summary>
    public sealed class QteCommandHandler : ICommandHandler
    {
        private readonly Func<RectTransform> m_CustomRootProvider;

        /// <summary>
        /// 初始化默认 QTE 命令执行器。
        /// </summary>
        /// <param name="customRootProvider">自定义 UI 根节点提供器。</param>
        public QteCommandHandler(Func<RectTransform> customRootProvider)
        {
            m_CustomRootProvider = customRootProvider ?? throw new ArgumentNullException(nameof(customRootProvider));
        }

        /// <inheritdoc />
        public bool CanHandle(global::GameDeveloperKit.Story.Model.Command command)
        {
            return command != null && string.Equals(command.Name, InteractionCommandNames.Qte, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public ICommandHandle Execute(global::GameDeveloperKit.Story.Model.Command command, RuntimeContext context)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var root = m_CustomRootProvider();
            if (root == null)
            {
                throw new GameException($"Story custom root surface is missing. command:{command.CommandId}");
            }

            var durationSeconds = command.Arguments.GetNumber(InteractionCommandNames.DurationSecondsArgument);
            if (TimeRules.IsFinitePositive(durationSeconds) is false)
            {
                throw new GameException($"Story QTE duration must be finite and greater than zero. command:{command.CommandId}");
            }

            var requiredCountValue = command.Arguments.GetNumber(InteractionCommandNames.RequiredCountArgument, 1d);
            if (TimeRules.IsFinitePositive(requiredCountValue) is false)
            {
                throw new GameException($"Story QTE required count must be finite and greater than zero. command:{command.CommandId}");
            }

            var requiredCount = Mathf.Max(
                1,
                Mathf.CeilToInt((float)requiredCountValue));
            var promptTextKey = command.Arguments.GetString(InteractionCommandNames.PromptTextKeyArgument);
            var handle = new CommandHandle(command);
            QteOverlaySession.Create(root, handle, (float)durationSeconds, requiredCount, promptTextKey);
            return handle;
        }
    }

    internal sealed class QteOverlaySession : MonoBehaviour
    {
        private CommandHandle m_Handle;
        private TMP_Text m_PromptText;
        private TMP_Text m_StatusText;
        private Image m_ProgressFill;
        private Button m_InputButton;
        private float m_DurationSeconds;
        private float m_ElapsedSeconds;
        private int m_RequiredCount;
        private int m_CurrentCount;
        private bool m_CleanupStarted;

        public static QteOverlaySession Create(
            RectTransform parent,
            CommandHandle handle,
            float durationSeconds,
            int requiredCount,
            string promptTextKey)
        {
            var root = new GameObject("StoryQteOverlay", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var session = root.AddComponent<QteOverlaySession>();
            session.Initialize(handle, durationSeconds, requiredCount, promptTextKey);
            return session;
        }

        private void Initialize(CommandHandle handle, float durationSeconds, int requiredCount, string promptTextKey)
        {
            m_Handle = handle ?? throw new ArgumentNullException(nameof(handle));
            m_DurationSeconds = Mathf.Max(0.01f, durationSeconds);
            m_RequiredCount = Mathf.Max(1, requiredCount);
            BuildOverlay(promptTextKey);
            Refresh();

            m_Handle.Completed += OnHandleFinished;
            m_Handle.Canceled += OnHandleFinished;
            m_Handle.Stopped += OnHandleFinished;
            m_Handle.Failed += OnHandleFinished;
        }

        private void Update()
        {
            if (MediaCommandUtility.IsTerminal(m_Handle))
            {
                Cleanup();
                return;
            }

            m_ElapsedSeconds += Time.unscaledDeltaTime;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                RegisterInput();
            }

            if (m_ElapsedSeconds >= m_DurationSeconds)
            {
                m_Handle.Complete(InteractionCommandNames.FailOutcome);
                return;
            }

            Refresh();
        }

        private void RegisterInput()
        {
            if (MediaCommandUtility.IsTerminal(m_Handle))
            {
                return;
            }

            m_CurrentCount++;
            if (m_CurrentCount >= m_RequiredCount)
            {
                m_Handle.Complete(InteractionCommandNames.SuccessOutcome);
                return;
            }

            Refresh();
        }

        private void BuildOverlay(string promptTextKey)
        {
            var panel = CreateRect(transform, "Panel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            panel.sizeDelta = new Vector2(520f, 160f);
            panel.anchoredPosition = new Vector2(0f, 88f);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.04f, 0.05f, 0.06f, 0.92f);

            m_PromptText = CreateText(panel, "Prompt", promptTextKey, 28, new Vector2(24f, -18f), new Vector2(-24f, -58f));
            m_StatusText = CreateText(panel, "Status", string.Empty, 20, new Vector2(24f, -60f), new Vector2(-24f, -92f));

            var progressRoot = CreateRect(panel, "Progress", Vector2.zero, Vector2.one, new Vector2(0f, 1f));
            progressRoot.offsetMin = new Vector2(24f, 44f);
            progressRoot.offsetMax = new Vector2(-160f, -100f);
            var progressBackground = progressRoot.gameObject.AddComponent<Image>();
            progressBackground.color = new Color(0.16f, 0.18f, 0.2f, 0.95f);

            var fill = CreateRect(progressRoot, "Fill", Vector2.zero, Vector2.one, new Vector2(0f, 0.5f));
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            m_ProgressFill = fill.gameObject.AddComponent<Image>();
            m_ProgressFill.color = new Color(0.24f, 0.64f, 0.9f, 0.95f);
            m_ProgressFill.type = Image.Type.Filled;
            m_ProgressFill.fillMethod = Image.FillMethod.Horizontal;
            m_ProgressFill.fillOrigin = 0;

            m_InputButton = CreateButton(panel, "InputButton", "QTE");
            var buttonRect = (RectTransform)m_InputButton.transform;
            buttonRect.anchorMin = new Vector2(1f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(1f, 0f);
            buttonRect.sizeDelta = new Vector2(120f, 54f);
            buttonRect.anchoredPosition = new Vector2(-24f, 44f);
            m_InputButton.onClick.AddListener(RegisterInput);
        }

        private void Refresh()
        {
            if (m_StatusText != null)
            {
                var remaining = Mathf.Max(0f, m_DurationSeconds - m_ElapsedSeconds);
                m_StatusText.text = $"{m_CurrentCount}/{m_RequiredCount}  {remaining:0.0}s";
            }

            if (m_ProgressFill != null)
            {
                m_ProgressFill.fillAmount = Mathf.Clamp01(m_CurrentCount / (float)m_RequiredCount);
            }
        }

        private void OnHandleFinished(ICommandHandle handle)
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (m_CleanupStarted)
            {
                return;
            }

            m_CleanupStarted = true;
            DetachHandle();

            if (m_InputButton != null)
            {
                m_InputButton.onClick.RemoveListener(RegisterInput);
                m_InputButton = null;
            }

            if (this != null)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            m_CleanupStarted = true;
            DetachHandle();
            m_InputButton = null;
        }

        private void DetachHandle()
        {
            if (m_Handle != null)
            {
                m_Handle.Completed -= OnHandleFinished;
                m_Handle.Canceled -= OnHandleFinished;
                m_Handle.Stopped -= OnHandleFinished;
                m_Handle.Failed -= OnHandleFinished;
                m_Handle = null;
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

        private static Button CreateButton(Transform parent, string name, string label)
        {
            var rect = CreateRect(parent, name, Vector2.zero, Vector2.zero, Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.28f, 0.36f, 0.96f);
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
