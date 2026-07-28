using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorCloud;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.StoryEditor.Media;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.MediaEditor
{
    public sealed class HlsTranscodeWindow : EditorWindow
    {
        private static Action<HlsTranscodeResult> s_PendingCompleted;
        private static HlsPublishIntent s_PendingPublishIntent;
        private static Action<HlsPublishWorkflowResult> s_PendingPublishCompleted;

        private readonly List<Toggle> m_RenditionToggles = new List<Toggle>();
        private TextField m_InputField;
        private TextField m_PackageNameField;
        private Label m_ToolchainStatus;
        private Label m_TaskStatus;
        private ProgressBar m_Progress;
        private TextField m_Log;
        private Button m_InstallButton;
        private Button m_TranscodeButton;
        private Button m_PublishButton;
        private Button m_CancelButton;
        private CancellationTokenSource m_Cancellation;
        private FfmpegToolchainResolver m_Resolver;
        private FfmpegToolchainStatus m_Toolchain;
        private Action<HlsTranscodeResult> m_Completed;
        private HlsPublishIntent m_PublishIntent;
        private Action<HlsPublishWorkflowResult> m_PublishCompleted;
        private HlsTranscodeResult m_LastTranscodeResult;
        private HlsPackagePublishResult m_PendingCatalogPackage;

        public static void Open(Action<HlsTranscodeResult> completed)
        {
            s_PendingCompleted = completed;
            var window = GetWindow<HlsTranscodeWindow>(true, "HLS 转码", true);
            window.minSize = new Vector2(640f, 610f);
            window.m_Completed = completed;
            s_PendingCompleted = null;
            window.Show();
        }

        internal static void OpenForPublish(
            HlsPublishIntent intent,
            Action<HlsPublishWorkflowResult> completed)
        {
            s_PendingPublishIntent = intent ?? throw new ArgumentNullException(nameof(intent));
            s_PendingPublishCompleted = completed;
            var window = GetWindow<HlsTranscodeWindow>(true, "HLS 转码与发布", true);
            window.minSize = new Vector2(640f, 610f);
            window.m_PublishIntent = intent;
            window.m_PublishCompleted = completed;
            s_PendingPublishIntent = null;
            s_PendingPublishCompleted = null;
            window.ApplyPublishIntent();
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            m_Completed = s_PendingCompleted ?? m_Completed;
            s_PendingCompleted = null;
            m_PublishIntent = s_PendingPublishIntent ?? m_PublishIntent;
            m_PublishCompleted = s_PendingPublishCompleted ?? m_PublishCompleted;
            s_PendingPublishIntent = null;
            s_PendingPublishCompleted = null;
            m_Resolver = new FfmpegToolchainResolver();
            BuildUi();
            ApplyPublishIntent();
            RefreshToolchain();
        }

        private void OnDisable()
        {
            m_Cancellation?.Cancel();
            m_Cancellation?.Dispose();
            m_Cancellation = null;
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 16;
            rootVisualElement.style.paddingRight = 16;
            rootVisualElement.style.paddingTop = 14;
            rootVisualElement.style.paddingBottom = 14;

            rootVisualElement.Add(CreateTitle("FFmpeg 工具链"));
            var toolchainRow = CreateRow();
            m_ToolchainStatus = new Label { style = { flexGrow = 1, whiteSpace = WhiteSpace.Normal } };
            toolchainRow.Add(m_ToolchainStatus);
            m_InstallButton = new Button(() => Run(InstallAsync())) { text = "安装 FFmpeg" };
            toolchainRow.Add(m_InstallButton);
            rootVisualElement.Add(toolchainRow);

            rootVisualElement.Add(CreateTitle("输入与输出"));
            m_InputField = new TextField("MP4 文件") { isDelayed = true };
            rootVisualElement.Add(m_InputField);
            m_PackageNameField = new TextField("包名") { isDelayed = true };
            rootVisualElement.Add(m_PackageNameField);

            rootVisualElement.Add(CreateTitle("分辨率"));
            var renditionRow = CreateRow();
            m_RenditionToggles.Clear();
            foreach (var preset in HlsRenditionPresets.Default)
            {
                var toggle = new Toggle(preset.Label)
                {
                    name = "hls-rendition-" + preset.Label,
                    value = true,
                    userData = preset,
                    tooltip = preset.Label
                };
                m_RenditionToggles.Add(toggle);
                ConfigureRenditionToggle(toggle);
                renditionRow.Add(toggle);
            }

            rootVisualElement.Add(renditionRow);

            rootVisualElement.Add(CreateTitle("任务"));
            m_TaskStatus = new Label("等待开始。") { style = { whiteSpace = WhiteSpace.Normal } };
            rootVisualElement.Add(m_TaskStatus);
            m_Progress = new ProgressBar { lowValue = 0, highValue = 100, value = 0 };
            rootVisualElement.Add(m_Progress);
            m_Log = new TextField("FFmpeg 日志")
            {
                multiline = true,
                isReadOnly = true
            };
            m_Log.style.height = 190;
            m_Log.style.marginTop = 8;
            rootVisualElement.Add(m_Log);

            var actions = CreateRow();
            actions.style.justifyContent = Justify.FlexEnd;
            m_CancelButton = new Button(Cancel) { text = "取消" };
            m_CancelButton.SetEnabled(false);
            actions.Add(m_CancelButton);
            m_PublishButton = new Button(() => Run(RetryCatalogCommitAsync()))
            {
                name = "hls-publish-cloud-button",
                text = "重试提交 Catalog"
            };
            m_PublishButton.SetEnabled(false);
            m_PublishButton.style.display = DisplayStyle.None;
            actions.Add(m_PublishButton);
            m_TranscodeButton = new Button(() => Run(TranscodeAsync())) { text = "生成 HLS" };
            actions.Add(m_TranscodeButton);
            rootVisualElement.Add(actions);
        }

        private void ConfigureRenditionToggle(Toggle toggle)
        {
            toggle.style.width = 88f;
            toggle.style.height = 30f;
            toggle.style.minWidth = 76f;
            toggle.style.marginRight = 6f;
            toggle.style.paddingLeft = 10f;
            toggle.style.paddingRight = 10f;
            toggle.style.borderLeftWidth = 1f;
            toggle.style.borderRightWidth = 1f;
            toggle.style.borderTopWidth = 1f;
            toggle.style.borderBottomWidth = 1f;
            toggle.style.alignItems = Align.Center;
            toggle.style.justifyContent = Justify.Center;
            var input = toggle.Q<VisualElement>(className: "unity-toggle__input");
            if (input != null)
            {
                input.style.display = DisplayStyle.None;
            }

            var label = toggle.Q<Label>(className: "unity-toggle__label");
            if (label != null)
            {
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.flexGrow = 1f;
            }

            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is false && m_RenditionToggles.All(item => item.value is false))
                {
                    toggle.value = true;
                    return;
                }

                ApplyRenditionToggleStyle(toggle, evt.newValue);
            });
            ApplyRenditionToggleStyle(toggle, toggle.value);
        }

        private static void ApplyRenditionToggleStyle(Toggle toggle, bool selected)
        {
            var border = selected
                ? new Color(0.20f, 0.58f, 0.92f, 1f)
                : EditorGUIUtility.isProSkin
                    ? new Color(0.48f, 0.48f, 0.48f, 1f)
                    : new Color(0.40f, 0.40f, 0.40f, 1f);
            toggle.style.borderLeftColor = border;
            toggle.style.borderRightColor = border;
            toggle.style.borderTopColor = border;
            toggle.style.borderBottomColor = border;
            toggle.style.backgroundColor = selected
                ? EditorGUIUtility.isProSkin
                    ? new Color(0.15f, 0.42f, 0.68f, 1f)
                    : new Color(0.20f, 0.55f, 0.88f, 1f)
                : Color.clear;
            toggle.style.color = selected
                ? Color.white
                : EditorGUIUtility.isProSkin
                    ? new Color(0.84f, 0.84f, 0.84f, 1f)
                    : new Color(0.15f, 0.15f, 0.15f, 1f);
        }

        private async UniTask InstallAsync()
        {
            RefreshToolchain();
            if (m_Toolchain?.CanInstall is false)
            {
                return;
            }

            var package = m_Toolchain.Package;
            var accepted = EditorUtility.DisplayDialog(
                "安装 FFmpeg",
                $"版本：{package.Version}\n来源：{package.ArchiveUrl}\n许可证：{package.LicenseName}\n\n工具只会安装到项目 Library。",
                "同意并安装",
                "取消");
            if (accepted is false)
            {
                return;
            }

            await RunBusyAsync(async token =>
            {
                var progress = new Progress<ToolchainInstallProgress>(value =>
                {
                    m_TaskStatus.text = value.Message;
                    m_Progress.value = value.Progress * 100f;
                });
                await new FfmpegToolchainInstaller().InstallAsync(progress, token);
                RefreshToolchain();
            });
        }

        private async UniTask TranscodeAsync()
        {
            RefreshToolchain();
            if (m_Toolchain?.IsReady is false)
            {
                throw new InvalidOperationException(m_Toolchain?.Message ?? "FFmpeg 工具链不可用。");
            }

            var selected = m_RenditionToggles
                .Where(toggle => toggle.value)
                .Select(toggle => (HlsRenditionPreset)toggle.userData)
                .ToArray();
            if (selected.Length == 0)
            {
                throw new InvalidOperationException("至少选择一个分辨率。");
            }

            var target = HlsTranscodePlanner.ValidateRequest(
                new HlsTranscodeRequest(
                    m_InputField.value,
                    m_PackageNameField.value,
                    selected),
                Directory.GetCurrentDirectory());
            var overwrite = m_PublishIntent != null && Directory.Exists(target);
            if (m_PublishIntent == null && Directory.Exists(target))
            {
                overwrite = EditorUtility.DisplayDialog(
                    "覆盖 HLS 包",
                    $"目标目录已存在：\n{target}\n\n是否整体替换？",
                    "覆盖",
                    "取消");
            }
            if (Directory.Exists(target) && overwrite is false)
            {
                return;
            }

            var request = new HlsTranscodeRequest(
                m_InputField.value,
                m_PackageNameField.value,
                selected,
                overwriteExisting: overwrite);
            await RunBusyAsync(async token =>
            {
                var progress = new Progress<HlsTranscodeProgress>(value =>
                {
                    m_TaskStatus.text = value.Message;
                    m_Progress.value = value.Progress * 100f;
                    AppendLog(value.LogLine);
                });
                var result = await new HlsTranscodeService().TranscodeAsync(request, progress, token);
                m_LastTranscodeResult = result;
                m_Log.value = result.StandardOutput + Environment.NewLine + result.StandardError;
                AssetDatabase.Refresh();
                if (m_PublishIntent != null)
                {
                    var uploadProgress = new Progress<CloudUploadProgress>(value =>
                    {
                        m_TaskStatus.text = "正在上传：" + value.ObjectKey;
                        m_Progress.value = value.TotalBytes <= 0
                            ? 0
                            : (float)(value.TotalBytesSent * 100d / value.TotalBytes);
                    });
                    HlsPublishWorkflowResult published;
                    try
                    {
                        published = await new HlsPublishWorkflow().PublishAsync(
                            m_PublishIntent,
                            result,
                            uploadProgress,
                            token);
                    }
                    catch (HlsCatalogCommitPendingException exception)
                    {
                        m_PendingCatalogPackage = exception.Package;
                        m_PublishButton.style.display = DisplayStyle.Flex;
                        throw;
                    }

                    m_PendingCatalogPackage = null;
                    m_PublishButton.style.display = DisplayStyle.None;
                    m_TaskStatus.text = "发布完成。Media ID：" + published.Package.MediaId;
                    m_Progress.value = 100f;
                    m_PublishCompleted?.Invoke(published);
                }
                else
                {
                    m_Completed?.Invoke(result);
                }
            });
        }

        private void ApplyPublishIntent()
        {
            if (m_PublishIntent == null)
            {
                return;
            }

            m_InputField.value = m_PublishIntent.SourceMp4Path.Replace('\\', '/');
            m_PackageNameField.value = m_PublishIntent.MediaId;
            m_InputField.SetEnabled(false);
            m_PackageNameField.SetEnabled(false);
        }

        private async UniTask RetryCatalogCommitAsync()
        {
            if (m_PublishIntent == null ||
                m_LastTranscodeResult == null ||
                m_PendingCatalogPackage == null)
            {
                throw new InvalidOperationException("当前没有可重试的 Catalog 提交。");
            }

            await RunBusyAsync(async token =>
            {
                m_TaskStatus.text = "正在重试提交 Catalog…";
                HlsCatalogCommitResult catalog;
                try
                {
                    catalog = await new HlsPublishWorkflow().CommitCatalogAsync(
                        m_PublishIntent,
                        m_LastTranscodeResult,
                        token);
                }
                catch (CatalogException exception) when (
                    exception.Kind == CatalogErrorKind.ItemChanged ||
                    exception.Kind == CatalogErrorKind.DuplicateSource)
                {
                    m_PendingCatalogPackage = null;
                    m_PublishButton.style.display = DisplayStyle.None;
                    throw new InvalidOperationException(
                        "Catalog 内容已变化，请关闭转码窗口，刷新媒体库并重新确认发布。",
                        exception);
                }

                var result = new HlsPublishWorkflowResult(m_PendingCatalogPackage, catalog);
                m_PendingCatalogPackage = null;
                m_PublishButton.style.display = DisplayStyle.None;
                m_TaskStatus.text = "Catalog 提交完成。Media ID：" + result.Package.MediaId;
                m_Progress.value = 100f;
                m_PublishCompleted?.Invoke(result);
            });
        }

        private async UniTask RunBusyAsync(Func<CancellationToken, UniTask> action)
        {
            SetBusy(true);
            var cancellation = new CancellationTokenSource();
            m_Cancellation = cancellation;
            try
            {
                await action(cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                m_TaskStatus.text = "任务已取消。";
            }
            catch (Exception exception)
            {
                m_TaskStatus.text = exception.Message;
                m_Log.value = exception.ToString();
                throw;
            }
            finally
            {
                cancellation.Dispose();
                if (ReferenceEquals(m_Cancellation, cancellation))
                {
                    m_Cancellation = null;
                    SetBusy(false);
                }
            }
        }

        private void RefreshToolchain()
        {
            var config = EditorUserConfig.LoadOrCreate();
            m_Toolchain = m_Resolver.Detect(config.FfmpegPath, config.FfprobePath);
            m_ToolchainStatus.text = m_Toolchain.Message;
            m_InstallButton.style.display = m_Toolchain.CanInstall ? DisplayStyle.Flex : DisplayStyle.None;
            m_TranscodeButton?.SetEnabled(m_Toolchain.IsReady && m_Cancellation == null);
        }

        private void SetBusy(bool busy)
        {
            m_InputField.SetEnabled(busy is false);
            m_PackageNameField.SetEnabled(busy is false);
            if (m_PublishIntent != null)
            {
                m_InputField.SetEnabled(false);
                m_PackageNameField.SetEnabled(false);
            }
            foreach (var toggle in m_RenditionToggles)
            {
                toggle.SetEnabled(busy is false);
            }

            m_InstallButton.SetEnabled(busy is false);
            m_TranscodeButton.SetEnabled(busy is false && m_Toolchain?.IsReady == true);
            m_PublishButton.SetEnabled(busy is false && m_PendingCatalogPackage != null);
            m_CancelButton.SetEnabled(busy);
        }

        private void Cancel()
        {
            m_Cancellation?.Cancel();
        }

        private void AppendLog(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            const int maximumCharacters = 200000;
            var current = m_Log.value ?? string.Empty;
            if (current.Length >= maximumCharacters)
            {
                return;
            }

            var remaining = maximumCharacters - current.Length;
            var appended = line.Length <= remaining ? line : line.Substring(0, remaining);
            m_Log.value = current.Length == 0
                ? appended
                : current + Environment.NewLine + appended;
        }

        private void Run(UniTask operation)
        {
            operation.Forget(exception => Debug.LogException(exception));
        }

        private static VisualElement CreateRow()
        {
            return new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 8
                }
            };
        }

        private static Label CreateTitle(string text)
        {
            return new Label(text)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginTop = 10,
                    marginBottom = 8
                }
            };
        }
    }
}
