using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorConfiguration;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.MediaEditor
{
    internal sealed class HlsBatchPublishWindow : EditorWindow
    {
        private static IReadOnlyList<HlsPublishIntent> s_PendingIntents;
        private static Action s_PendingCompleted;

        private readonly Dictionary<HlsBatchPublishItem, ItemView> m_ItemViews =
            new Dictionary<HlsBatchPublishItem, ItemView>();
        private IReadOnlyList<HlsPublishIntent> m_Intents;
        private Action m_Completed;
        private HlsBatchPublishController m_Controller;
        private FfmpegToolchainResolver m_Resolver;
        private FfmpegToolchainStatus m_Toolchain;
        private CancellationTokenSource m_Cancellation;
        private Label m_ToolchainStatus;
        private Label m_Summary;
        private Button m_InstallButton;
        private Button m_StartButton;
        private Button m_CancelButton;
        private ScrollView m_List;
        private int m_LastReportedCompletedCount;

        public static void OpenForPublish(
            IReadOnlyList<HlsPublishIntent> intents,
            Action completed)
        {
            if (intents == null || intents.Count < 2)
            {
                throw new ArgumentException("Batch publish requires at least two intents.", nameof(intents));
            }

            s_PendingIntents = intents.ToArray();
            s_PendingCompleted = completed;
            var window = GetWindow<HlsBatchPublishWindow>(true, "批量 HLS 转码与发布", true);
            window.minSize = new Vector2(760f, 520f);
            if (s_PendingIntents != null)
            {
                window.m_Intents = s_PendingIntents;
                window.m_Completed = s_PendingCompleted;
                s_PendingIntents = null;
                s_PendingCompleted = null;
                window.InitializeBatch();
            }

            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            m_Intents = s_PendingIntents ?? m_Intents;
            m_Completed = s_PendingCompleted ?? m_Completed;
            s_PendingIntents = null;
            s_PendingCompleted = null;
            m_Resolver = new FfmpegToolchainResolver();
            BuildUi();
            InitializeBatch();
        }

        private void OnDisable()
        {
            m_Cancellation?.Cancel();
            m_Cancellation?.Dispose();
            m_Cancellation = null;
            if (m_Controller != null)
            {
                m_Controller.ItemChanged -= OnItemChanged;
            }
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 14f;
            rootVisualElement.style.paddingRight = 14f;
            rootVisualElement.style.paddingTop = 12f;
            rootVisualElement.style.paddingBottom = 12f;

            var title = new Label("批量 HLS 转码与发布")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 16f,
                    marginBottom = 10f
                }
            };
            rootVisualElement.Add(title);

            var toolchainRow = CreateRow();
            m_ToolchainStatus = new Label { style = { flexGrow = 1f, whiteSpace = WhiteSpace.Normal } };
            toolchainRow.Add(m_ToolchainStatus);
            m_InstallButton = new Button(() => Run(InstallToolchainAsync())) { text = "安装 FFmpeg" };
            toolchainRow.Add(m_InstallButton);
            rootVisualElement.Add(toolchainRow);

            m_Summary = new Label("等待加载批次。")
            {
                style = { whiteSpace = WhiteSpace.Normal, marginBottom = 8f }
            };
            rootVisualElement.Add(m_Summary);

            m_List = new ScrollView
            {
                name = "hls-batch-publish-list",
                style = { flexGrow = 1f, minHeight = 300f }
            };
            rootVisualElement.Add(m_List);

            var actions = CreateRow();
            actions.style.justifyContent = Justify.FlexEnd;
            m_CancelButton = new Button(Cancel) { text = "停止" };
            m_CancelButton.SetEnabled(false);
            actions.Add(m_CancelButton);
            m_StartButton = new Button(() => Run(StartBatchAsync())) { text = "开始批量发布" };
            m_StartButton.SetEnabled(false);
            actions.Add(m_StartButton);
            rootVisualElement.Add(actions);
        }

        private void InitializeBatch()
        {
            if (m_Intents == null || m_Intents.Count == 0 || m_List == null)
            {
                return;
            }

            RefreshToolchain();
            if (m_Controller != null)
            {
                m_Controller.ItemChanged -= OnItemChanged;
            }

            m_Controller = m_Toolchain?.IsReady == true
                ? new HlsBatchPublishController(
                    m_Intents,
                    Directory.GetCurrentDirectory(),
                    m_Toolchain.FfprobePath)
                : null;
            if (m_Controller != null)
            {
                m_Controller.ItemChanged += OnItemChanged;
            }

            RebuildRows();
            UpdateSummary();
            UpdateControls();
        }

        private void RefreshToolchain()
        {
            var config = EditorUserConfig.LoadOrCreate();
            m_Toolchain = m_Resolver.Detect(config.FfmpegPath, config.FfprobePath);
            m_ToolchainStatus.text = m_Toolchain.Message;
            m_InstallButton.style.display = m_Toolchain.CanInstall
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private async UniTask InstallToolchainAsync()
        {
            RefreshToolchain();
            if (m_Toolchain?.CanInstall is false)
            {
                return;
            }

            var package = m_Toolchain.Package;
            if (EditorUtility.DisplayDialog(
                    "安装 FFmpeg",
                    $"版本：{package.Version}\n来源：{package.ArchiveUrl}\n许可证：{package.LicenseName}\n\n工具只会安装到项目 Library。",
                    "同意并安装",
                    "取消") is false)
            {
                return;
            }

            await RunOperationAsync(async token =>
            {
                var progress = new Progress<ToolchainInstallProgress>(value =>
                {
                    m_Summary.text = value.Message;
                });
                await new FfmpegToolchainInstaller().InstallAsync(progress, token);
            });
            InitializeBatch();
        }

        private async UniTask StartBatchAsync()
        {
            if (m_Controller == null)
            {
                throw new InvalidOperationException(m_Toolchain?.Message ?? "FFmpeg 工具链不可用。");
            }

            await RunOperationAsync(token => m_Controller.RunAsync(token));
            AssetDatabase.Refresh();
            ReportCompletedItems();
        }

        private async UniTask RetryAsync(HlsBatchPublishItem item)
        {
            await RunOperationAsync(token => m_Controller.RetryAsync(item, token));
            AssetDatabase.Refresh();
            ReportCompletedItems();
        }

        private async UniTask RunOperationAsync(Func<CancellationToken, UniTask> operation)
        {
            if (m_Cancellation != null)
            {
                return;
            }

            var cancellation = new CancellationTokenSource();
            m_Cancellation = cancellation;
            UpdateControls();
            try
            {
                await operation(cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                m_Summary.text = "批次已停止，尚未开始的任务保持等待状态。";
            }
            catch (Exception exception)
            {
                m_Summary.text = "批次操作失败：" + exception.Message;
                Debug.LogException(exception);
            }
            finally
            {
                cancellation.Dispose();
                if (ReferenceEquals(m_Cancellation, cancellation))
                {
                    m_Cancellation = null;
                }

                UpdateSummary();
                UpdateControls();
            }
        }

        private void RebuildRows()
        {
            m_List.Clear();
            m_ItemViews.Clear();
            if (m_Controller == null)
            {
                foreach (var intent in m_Intents)
                {
                    m_List.Add(CreateUnavailableRow(intent));
                }

                return;
            }

            foreach (var item in m_Controller.Items)
            {
                var view = CreateItemRow(item);
                m_ItemViews[item] = view;
                m_List.Add(view.Root);
            }
        }

        private ItemView CreateItemRow(HlsBatchPublishItem item)
        {
            var root = new VisualElement
            {
                style =
                {
                    minHeight = 74f,
                    borderBottomWidth = 1f,
                    paddingTop = 6f,
                    paddingBottom = 6f,
                    paddingLeft = 6f,
                    paddingRight = 6f
                }
            };
            root.style.borderBottomColor = new Color(0f, 0f, 0f, 0.22f);

            var header = CreateRow();
            var name = new Label(Path.GetFileName(item.Intent.SourceMp4Path))
            {
                tooltip = item.Intent.SourceMp4Path,
                style = { flexGrow = 1f, unityFontStyleAndWeight = FontStyle.Bold }
            };
            header.Add(name);
            var state = new Label();
            state.style.minWidth = 100f;
            header.Add(state);
            var retry = new Button(() => Run(RetryAsync(item))) { text = "重试" };
            retry.style.width = 64f;
            header.Add(retry);
            root.Add(header);

            var progress = new ProgressBar { lowValue = 0f, highValue = 100f, value = 0f };
            root.Add(progress);
            var message = new Label { style = { whiteSpace = WhiteSpace.Normal, marginTop = 3f } };
            root.Add(message);

            var view = new ItemView(root, state, progress, message, retry);
            UpdateItemView(item, view);
            return view;
        }

        private static VisualElement CreateUnavailableRow(HlsPublishIntent intent)
        {
            return new Label(Path.GetFileName(intent.SourceMp4Path) + " · 等待 FFmpeg 工具链")
            {
                tooltip = intent.SourceMp4Path,
                style = { height = 32f, marginLeft = 6f, marginTop = 5f }
            };
        }

        private void OnItemChanged(HlsBatchPublishItem item)
        {
            if (m_ItemViews.TryGetValue(item, out var view))
            {
                UpdateItemView(item, view);
            }

            UpdateSummary();
            UpdateControls();
        }

        private void UpdateItemView(HlsBatchPublishItem item, ItemView view)
        {
            view.State.text = StateLabel(item.State);
            view.Progress.value = item.Progress * 100f;
            view.Message.text = string.IsNullOrWhiteSpace(item.Error)
                ? item.Message
                : item.Message + "\n" + item.Error;
            view.Retry.SetEnabled(
                m_Cancellation == null &&
                (item.State == HlsBatchPublishItemState.Failed ||
                 item.State == HlsBatchPublishItemState.CatalogPending ||
                 item.State == HlsBatchPublishItemState.Cancelled));
        }

        private void UpdateSummary()
        {
            if (m_Controller == null)
            {
                m_Summary.text = m_Toolchain?.Message ?? "FFmpeg 工具链不可用。";
                return;
            }

            var completed = m_Controller.Items.Count(item => item.State == HlsBatchPublishItemState.Completed);
            var failed = m_Controller.Items.Count(item =>
                item.State == HlsBatchPublishItemState.Failed ||
                item.State == HlsBatchPublishItemState.CatalogPending);
            var pending = m_Controller.Items.Count(item => item.State == HlsBatchPublishItemState.Pending);
            m_Summary.text = $"共 {m_Controller.Items.Count} 项 · 完成 {completed} · " +
                             $"失败/待重试 {failed} · 等待 {pending}";
        }

        private void UpdateControls()
        {
            var busy = m_Cancellation != null;
            m_InstallButton?.SetEnabled(busy is false);
            m_CancelButton?.SetEnabled(busy);
            m_StartButton?.SetEnabled(
                busy is false &&
                m_Controller != null &&
                m_Controller.Items.Any(item => item.State == HlsBatchPublishItemState.Pending));
            foreach (var pair in m_ItemViews)
            {
                UpdateItemView(pair.Key, pair.Value);
            }
        }

        private void ReportCompletedItems()
        {
            var completed = m_Controller?.Items.Count(item =>
                item.State == HlsBatchPublishItemState.Completed) ?? 0;
            if (completed <= m_LastReportedCompletedCount)
            {
                return;
            }

            m_LastReportedCompletedCount = completed;
            m_Completed?.Invoke();
        }

        private void Cancel()
        {
            m_Cancellation?.Cancel();
        }

        private void Run(UniTask operation)
        {
            operation.Forget(exception => Debug.LogException(exception));
        }

        private static string StateLabel(HlsBatchPublishItemState state)
        {
            switch (state)
            {
                case HlsBatchPublishItemState.Pending: return "等待";
                case HlsBatchPublishItemState.Probing: return "探测中";
                case HlsBatchPublishItemState.Transcoding: return "转码中";
                case HlsBatchPublishItemState.Uploading: return "上传中";
                case HlsBatchPublishItemState.CommittingCatalog: return "提交 Catalog";
                case HlsBatchPublishItemState.Completed: return "完成";
                case HlsBatchPublishItemState.Failed: return "失败";
                case HlsBatchPublishItemState.CatalogPending: return "待提交 Catalog";
                case HlsBatchPublishItemState.Cancelled: return "已取消";
                default: return state.ToString();
            }
        }

        private static VisualElement CreateRow()
        {
            return new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 6f
                }
            };
        }

        private sealed class ItemView
        {
            public ItemView(
                VisualElement root,
                Label state,
                ProgressBar progress,
                Label message,
                Button retry)
            {
                Root = root;
                State = state;
                Progress = progress;
                Message = message;
                Retry = retry;
            }

            public VisualElement Root { get; }
            public Label State { get; }
            public ProgressBar Progress { get; }
            public Label Message { get; }
            public Button Retry { get; }
        }
    }
}
