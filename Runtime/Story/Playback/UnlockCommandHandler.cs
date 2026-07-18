using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Text;
using GameDeveloperKit.Story.Settlement;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace GameDeveloperKit.Story.Playback
{
    /// <summary>
    /// 默认解锁命令执行器。
    /// </summary>
    public sealed class UnlockCommandHandler : ICommandHandler
    {
        private readonly Func<RectTransform> m_CustomRootProvider;
        private readonly Func<IUnlockStateProvider> m_UnlockStateProvider;
        private readonly Func<TextReference, string> m_TextResolver;

        /// <summary>
        /// 初始化默认解锁命令执行器。
        /// </summary>
        /// <param name="customRootProvider">自定义 UI 根节点提供器。</param>
        /// <param name="unlockStateProvider">解锁状态提供器。</param>
        public UnlockCommandHandler(
            Func<RectTransform> customRootProvider,
            Func<IUnlockStateProvider> unlockStateProvider)
            : this(customRootProvider, unlockStateProvider, reference => reference.Value)
        {
        }

        public UnlockCommandHandler(
            Func<RectTransform> customRootProvider,
            Func<IUnlockStateProvider> unlockStateProvider,
            Func<TextReference, string> textResolver)
        {
            m_CustomRootProvider = customRootProvider ?? throw new ArgumentNullException(nameof(customRootProvider));
            m_UnlockStateProvider = unlockStateProvider ?? throw new ArgumentNullException(nameof(unlockStateProvider));
            m_TextResolver = textResolver ?? throw new ArgumentNullException(nameof(textResolver));
        }

        /// <inheritdoc />
        public bool CanHandle(global::GameDeveloperKit.Story.Model.Command command)
        {
            return command != null && string.Equals(command.Name, InteractionCommandNames.Unlock, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public ICommandHandle Execute(global::GameDeveloperKit.Story.Model.Command command, RuntimeContext context)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var unlockId = GetRequiredArgument(command, InteractionCommandNames.UnlockIdArgument);
            var puzzleType = GetRequiredArgument(command, InteractionCommandNames.PuzzleTypeArgument);
            if (IsSupportedPuzzleType(puzzleType) is false)
            {
                throw new GameException($"Story unlock puzzle type is invalid. command:{command.CommandId} puzzleType:{puzzleType}");
            }

            var promptTextKey = GetRequiredArgument(command, InteractionCommandNames.PromptTextKeyArgument);
            var promptText = m_TextResolver(TextReferenceCodec.DeserializeOrLegacy(promptTextKey));
            var provider = m_UnlockStateProvider();
            if (provider == null)
            {
                throw new GameException($"Story unlock state provider is missing. command:{command.CommandId}");
            }

            var handle = new CommandHandle(command);
            if (provider.TryGetUnlockState(unlockId, out var unlocked) && unlocked)
            {
                handle.Complete(InteractionCommandNames.SuccessOutcome);
                return handle;
            }

            var root = m_CustomRootProvider();
            if (root == null)
            {
                throw new GameException($"Story custom root surface is missing. command:{command.CommandId}");
            }

            UnlockOverlaySession.Create(root, handle, provider, unlockId, puzzleType, promptText);
            return handle;
        }

        private static string GetRequiredArgument(global::GameDeveloperKit.Story.Model.Command command, string key)
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
            return string.Equals(puzzleType, InteractionCommandNames.PuzzleTypeLineConnect, StringComparison.Ordinal) ||
                   string.Equals(puzzleType, InteractionCommandNames.PuzzleTypeNodeUnlock, StringComparison.Ordinal) ||
                   string.Equals(puzzleType, InteractionCommandNames.PuzzleTypeCustom, StringComparison.Ordinal);
        }
    }

    internal sealed class UnlockOverlaySession : MonoBehaviour
    {
        private CommandHandle m_Handle;
        private IUnlockStateProvider m_StateProvider;
        private TMP_Text m_PromptText;
        private TMP_Text m_StatusText;
        private Button m_UnlockButton;
        private Button m_FailButton;
        private Button m_CancelButton;
        private string m_UnlockId;
        private string m_PuzzleType;

        public static UnlockOverlaySession Create(
            RectTransform parent,
            CommandHandle handle,
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

            var session = root.AddComponent<UnlockOverlaySession>();
            session.Initialize(handle, stateProvider, unlockId, puzzleType, promptTextKey);
            return session;
        }

        private void Initialize(
            CommandHandle handle,
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
            if (MediaCommandUtility.IsTerminal(m_Handle))
            {
                return;
            }

            if (m_StateProvider.TrySetUnlockState(m_UnlockId, true, out _) is false)
            {
                m_Handle.Complete(InteractionCommandNames.FailOutcome);
                return;
            }

            m_Handle.Complete(InteractionCommandNames.SuccessOutcome);
        }

        private void OnFailClicked()
        {
            if (MediaCommandUtility.IsTerminal(m_Handle))
            {
                return;
            }

            m_Handle.Complete(InteractionCommandNames.FailOutcome);
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

        private void OnHandleFinished(ICommandHandle handle)
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

    public sealed class SettlementCommandHandler : ICommandHandler
    {
        private readonly Func<ISettlementExecutor> m_Executor;

        public SettlementCommandHandler(Func<ISettlementExecutor> executor)
        {
            m_Executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        public bool CanHandle(global::GameDeveloperKit.Story.Model.Command command)
        {
            return command != null && string.Equals(command.Name, SettlementCommandNames.SettleChapter, StringComparison.Ordinal);
        }

        public ICommandHandle Execute(global::GameDeveloperKit.Story.Model.Command command, RuntimeContext context)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var handle = new CommandHandle(command);
            ExecuteAsync(handle, context).Forget(Debug.LogException);
            return handle;
        }

        private async UniTask ExecuteAsync(CommandHandle handle, RuntimeContext context)
        {
            using var cancellation = new CancellationTokenSource();
            void CancelExecution(ICommandHandle _)
            {
                cancellation.Cancel();
            }
            handle.Canceled += CancelExecution;
            handle.Stopped += CancelExecution;
            try
            {
                var planVersion = handle.Command.Arguments.GetNumber(SettlementCommandNames.PlanVersionArgument);
                if (planVersion != SettlementPlan.CurrentVersion)
                {
                    throw new GameException($"Story settlement plan version is unsupported. command:{handle.Command.CommandId} version:{planVersion}");
                }
                if (SettlementPlanCodec.TryDeserialize(handle.Command.Arguments.GetString(SettlementCommandNames.PlanArgument), out var plan, out var error) is false)
                {
                    throw new GameException($"Story settlement plan is invalid. command:{handle.Command.CommandId} error:{error}");
                }
                var executor = m_Executor();
                if (executor == null)
                {
                    handle.Complete(SettlementCommandNames.FailedOutcome);
                    return;
                }
                var settlementContext = new SettlementContext(context.Program.StoryId, context.Chapter.ChapterId, handle.Command.Arguments.GetString(SettlementCommandNames.SettlementIdArgument));
                var result = await executor.ExecuteAsync(plan, settlementContext, cancellation.Token);
                if (handle.IsCanceled || handle.IsStopped) return;
                switch (result.Status)
                {
                    case SettlementStatus.Applied:
                    case SettlementStatus.AlreadyApplied:
                        handle.Complete(SettlementCommandNames.CompletedOutcome);
                        break;
                    case SettlementStatus.Failed:
                        handle.Complete(SettlementCommandNames.FailedOutcome);
                        break;
                    default:
                        throw new GameException($"Story settlement executor returned an invalid status. command:{handle.Command.CommandId} status:{result.Status}");
                }
            }
            catch (Exception exception)
            {
                if (handle.IsCanceled || handle.IsStopped) return;
                handle.Fail(exception);
            }
            finally
            {
                handle.Canceled -= CancelExecution;
                handle.Stopped -= CancelExecution;
            }
        }
    }
}
