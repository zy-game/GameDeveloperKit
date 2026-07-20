using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story;
using GameDeveloperKit.StoryEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.StoryEditor.Media;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Playback;
using GameDeveloperKit.StoryEditor.Validation;
using GameDeveloperKit.StoryEditor.UI;

namespace GameDeveloperKit.StoryEditor.UI
{
    public sealed class PlaybackWindow : EditorWindow
    {
        private const string WindowTitle = "剧情播放窗口";
        private const string StylePath = "Editor/StoryEditor/UI/MainWindow.uss";
        private const string PlayVideoCommandName = "play_video";
        private const string CompletedOutcomeId = "completed";

        private static readonly Rect s_DefaultVideoUvRect = new Rect(0f, 0f, 1f, 1f);

        private AuthoringAsset m_Asset;
        private string m_EpisodeId;
        private Session m_Session;
        private AvProPlayback m_AvProPlayback;

        private Label m_TitleLabel;
        private Label m_StatusLabel;
        private VisualElement m_OutputContainer;
        private VisualElement m_HistoryContainer;
        private Image m_VideoImage;
        private Label m_VideoStatusLabel;
        private Slider m_VideoSeekSlider;
        private Label m_VideoSeekTimeLabel;
        private PopupField<string> m_VideoQualityPopup;
        private string m_RenderedVideoCommandId;
        private bool m_LastVideoFinished;
        private string m_LastVideoError;
        private bool m_UpdatingVideoSeek;

        [MenuItem("GameDeveloperKit/剧情编辑/打开播放窗口")]
        public static void OpenFromMenu()
        {
            var asset = Selection.activeObject as AuthoringAsset;
            if (asset == null)
            {
                asset = AuthoringAssetStore.LoadOrCreate();
            }

            Open(asset, asset?.FindDefaultEpisode()?.EpisodeId);
        }

        public static void Open(AuthoringAsset asset, string episodeId)
        {
            var window = GetWindow<PlaybackWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(820f, 520f);
            window.SetContext(asset, episodeId);
            window.Show();
        }

        public void SetContext(AuthoringAsset asset, string episodeId)
        {
            m_Asset = asset;
            m_EpisodeId = string.IsNullOrWhiteSpace(episodeId) ? asset?.FindDefaultEpisode()?.EpisodeId : episodeId;
            BuildLayout();
            RestartSession();
        }

        public void CreateGUI()
        {
            RegisterEditorUpdate();
            if (m_Asset == null)
            {
                m_Asset = Selection.activeObject as AuthoringAsset ?? AuthoringAssetStore.LoadOrCreate();
            }

            if (string.IsNullOrWhiteSpace(m_EpisodeId))
            {
                m_EpisodeId = m_Asset?.FindDefaultEpisode()?.EpisodeId;
            }

            BuildLayout();
            RestartSession();
        }

        private void OnDisable()
        {
            UnregisterEditorUpdate();
            ShutdownSession();
            ShutdownAvProPlayback();
        }

        private void BuildLayout()
        {
            rootVisualElement.Clear();
            var styleSheet = GameDeveloperKitEditorPaths.LoadPackageAsset<StyleSheet>(StylePath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            var root = new VisualElement();
            root.AddToClassList("story-playback");
            rootVisualElement.Add(root);

            var toolbar = new VisualElement();
            toolbar.AddToClassList("story-playback__toolbar");
            m_TitleLabel = new Label();
            m_TitleLabel.AddToClassList("story-playback__title");
            toolbar.Add(m_TitleLabel);

            var actions = new VisualElement();
            actions.AddToClassList("story-playback__toolbar-actions");
            actions.Add(CreateButton("重启章节", "重新编译当前剧情，并从当前章节入口开始播放。", RestartSession));
            actions.Add(CreateButton("关闭", "关闭剧情播放窗口。", Close));
            toolbar.Add(actions);
            root.Add(toolbar);

            m_StatusLabel = new Label();
            m_StatusLabel.AddToClassList("story-playback__status");
            root.Add(m_StatusLabel);

            var body = new VisualElement();
            body.AddToClassList("story-playback__body");
            m_OutputContainer = new ScrollView();
            m_OutputContainer.AddToClassList("story-playback__output");
            m_HistoryContainer = new ScrollView();
            m_HistoryContainer.AddToClassList("story-playback__history");
            body.Add(m_OutputContainer);
            body.Add(m_HistoryContainer);
            root.Add(body);

            Refresh();
        }

        private void RestartSession()
        {
            ShutdownSession();
            if (m_Asset != null)
            {
                m_Asset.EnsureDefaults();
            }

            m_Session = new Session(m_Asset, m_EpisodeId);
            m_Session.Start();
            Refresh();
        }

        private void ShutdownSession()
        {
            StopVideoPlayback();
            if (m_Session != null)
            {
                m_Session.Dispose();
                m_Session = null;
            }
        }

        private void Refresh()
        {
            if (m_TitleLabel != null)
            {
                var story = string.IsNullOrWhiteSpace(m_Asset?.StoryId) ? "未选择剧情" : m_Asset.StoryId;
                var version = string.IsNullOrWhiteSpace(m_Session?.Program?.Version) ? m_Asset?.Version : m_Session.Program.Version;
                m_TitleLabel.text = $"{story}  v{version ?? "?"}  /  {SafeText(m_EpisodeId, "未选择章节")}";
                m_TitleLabel.tooltip = "当前播放会话使用编译后的 Program 和运行时 StoryModule 推进。";
            }

            if (m_StatusLabel != null)
            {
                m_StatusLabel.text = StatusText();
            }

            RefreshOutput();
            RefreshHistory();
        }

        private void RefreshOutput()
        {
            if (m_OutputContainer == null)
            {
                return;
            }

            m_OutputContainer.Clear();
            if (m_Session == null)
            {
                AddMessage(m_OutputContainer, "播放会话未启动。", "story-playback__message--error");
                return;
            }

            if (m_Session.Report != null && m_Session.Report.HasErrors)
            {
                AddSectionTitle(m_OutputContainer, "编译错误");
                foreach (var issue in m_Session.Report.Issues.Where(x => x.Severity == ValidationSeverity.Error))
                {
                    AddMessage(m_OutputContainer, issue.ToString(), "story-playback__message--error");
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(m_Session.ErrorMessage) is false)
            {
                AddMessage(m_OutputContainer, m_Session.ErrorMessage, "story-playback__message--error");
            }

            var frame = m_Session.CurrentFrame;
            if (frame == null)
            {
                StopVideoPlayback();
                AddMessage(m_OutputContainer, "当前没有运行时帧。", "story-playback__message--empty");
                return;
            }

            AddSectionTitle(m_OutputContainer, FrameTitle(frame));
            AddMeta(m_OutputContainer, "剧情段", frame.Episode?.EpisodeId);
            AddMeta(m_OutputContainer, "出口", frame.CompletedExitId);
            AddMeta(m_OutputContainer, "步骤", frame.AnchorStep?.StepId);
            AddMeta(m_OutputContainer, "轨道", frame.Tracks.Count.ToString());
            AddMeta(m_OutputContainer, "选项", frame.Choices.Count.ToString());

            if (frame.IsCompleted)
            {
                StopVideoPlayback();
                RenderCompleted();
                return;
            }

            m_RenderedVideoCommandId = null;
            StopVideoIfStale(frame);
            RenderTracks(frame);
            RenderChoices(frame);
            RenderFrameActions(frame);
        }

        private void RenderTracks(Frame frame)
        {
            if (frame.Tracks.Count == 0)
            {
                return;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                if (track == null)
                {
                    continue;
                }

                switch (track.Kind)
                {
                    case FrameTrackKind.Text:
                        RenderTextTrack(track);
                        break;
                    case FrameTrackKind.Command:
                        RenderCommandTrack(track, frame);
                        break;
                    case FrameTrackKind.Wait:
                        RenderWaitTrack(track);
                        break;
                }
            }
        }

        private void RenderTextTrack(FrameTrack track)
        {
            var catalog = LocalizationTextCatalog.Build();
            AddSectionTitle(m_OutputContainer, "文本");
            AddBranchMeta(track);
            AddMeta(m_OutputContainer, "说话人", catalog.Resolve(track.Speaker));
            AddMeta(m_OutputContainer, "文本", catalog.Resolve(track.TextKey));
            AddTags(track.Tags);
        }

        private void RenderChoices(Frame frame)
        {
            if (frame.Choices.Count == 0)
            {
                return;
            }

            AddSectionTitle(m_OutputContainer, "选项");
            for (var i = 0; i < frame.Choices.Count; i++)
            {
                var choice = frame.Choices[i];
                var button = CreateButton(
                    $"{LocalizationTextCatalog.Build().Resolve(choice.TextKey)}  ({choice.ChoiceId})",
                    $"调用 StoryModule.Select(\"{choice.ChoiceId}\")。",
                    () => Select(choice.ChoiceId));
                button.AddToClassList("story-playback__choice");
                m_OutputContainer.Add(button);
            }
        }

        private void RenderCommandTrack(FrameTrack track, Frame frame)
        {
            var command = track.Command;
            if (command == null)
            {
                AddMessage(m_OutputContainer, "命令输出缺少命令数据。", "story-playback__message--error");
                return;
            }

            AddSectionTitle(m_OutputContainer, "命令");
            AddBranchMeta(track);
            AddMeta(m_OutputContainer, "命令 ID", command.CommandId);
            AddMeta(m_OutputContainer, "命令名", command.Name);
            AddMeta(m_OutputContainer, "等待完成", command.WaitForCompletion ? "是" : "否");
            AddSectionTitle(m_OutputContainer, "参数");
            if (command.Arguments.Values.Count == 0)
            {
                AddMessage(m_OutputContainer, "无参数。", "story-playback__message--empty");
            }
            else
            {
                foreach (var pair in command.Arguments.Values.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    AddMeta(m_OutputContainer, pair.Key, FormatArgument(pair.Value));
                    var preview = FormatAssetPreview(pair.Value);
                    if (string.IsNullOrWhiteSpace(preview) is false)
                    {
                        AddMessage(m_OutputContainer, preview, "story-playback__message--asset");
                    }
                }
            }

            var isPlayVideo = IsPlayVideoCommand(command);
            if (isPlayVideo)
            {
                RenderVideoCommand(command);
            }

            var outcomes = OutcomeIds(command).ToList();
            if (frame.WaitsForCommand && outcomes.Count > 0 && CanShowCommandCompletion(command))
            {
                AddSectionTitle(m_OutputContainer, "结果");
                for (var i = 0; i < outcomes.Count; i++)
                {
                    var outcome = outcomes[i];
                    m_OutputContainer.Add(CreateButton(
                        outcome,
                        $"调用 StoryModule.CompleteCommand(\"{command.CommandId}\", \"{outcome}\")。",
                        () => CompleteCommand(command.CommandId, outcome)));
                }
            }
            else if (frame.WaitsForCommand && command.WaitForCompletion && CanShowCommandCompletion(command))
            {
                m_OutputContainer.Add(CreateButton(
                    "完成命令",
                    $"调用 StoryModule.CompleteCommand(\"{command.CommandId}\", null)。",
                    () => CompleteCommand(command.CommandId, null)));
            }
        }

        private void RenderVideoCommand(global::GameDeveloperKit.Story.Model.Command command)
        {
            if (string.IsNullOrWhiteSpace(m_RenderedVideoCommandId) is false &&
                string.Equals(m_RenderedVideoCommandId, command.CommandId, StringComparison.Ordinal) is false)
            {
                AddMessage(m_OutputContainer, "当前帧已在播放另一个视频命令。", "story-playback__message--empty");
                return;
            }

            m_RenderedVideoCommandId = command.CommandId;
            var source = command.Arguments.GetString(
                MediaCommandNames.MediaSourceArgument,
                command.Arguments.GetString(MediaCommandNames.VideoSourceArgument));
            var clipPath = command.Arguments.GetString(MediaCommandNames.ClipArgument);
            AddSectionTitle(m_OutputContainer, "视频预览");
            AddMeta(m_OutputContainer, "来源", source);
            AddMeta(m_OutputContainer, "路径", clipPath);
            AddMeta(m_OutputContainer, "允许 Seek", IsSeekRequested(command) ? "是" : "否");

            if (string.IsNullOrWhiteSpace(clipPath))
            {
                AddMessage(m_OutputContainer, "视频命令缺少 clip 参数。", "story-playback__message--error");
                return;
            }

            if (m_AvProPlayback == null)
            {
                m_AvProPlayback = new AvProPlayback();
            }

            if (m_AvProPlayback.IsCurrent(command.CommandId, source, clipPath) is false)
            {
                m_AvProPlayback.Play(command, clipPath);
                m_LastVideoFinished = m_AvProPlayback.IsFinished;
                m_LastVideoError = m_AvProPlayback.ErrorMessage;
            }

            if (string.IsNullOrWhiteSpace(m_AvProPlayback.CurrentResolvedPath) is false)
            {
                AddMeta(m_OutputContainer, "实际路径", m_AvProPlayback.CurrentResolvedPath);
            }

            m_VideoStatusLabel = new Label(VideoStatusText());
            m_VideoStatusLabel.AddToClassList("story-playback__video-status");
            m_OutputContainer.Add(m_VideoStatusLabel);

            m_VideoImage = new Image
            {
                image = VideoPreviewTexture(),
                scaleMode = ScaleMode.StretchToFill,
                uv = VideoPreviewUv(),
                tooltip = "AVProVideo 当前输出纹理。"
            };
            m_VideoImage.AddToClassList("story-playback__video-image");
            m_OutputContainer.Add(m_VideoImage);

            if (m_AvProPlayback.HasFirstFrame is false &&
                string.IsNullOrWhiteSpace(m_AvProPlayback.ErrorMessage))
            {
                AddMessage(m_OutputContainer, "等待 AVPro 输出第一帧。", "story-playback__message--empty");
            }

            if (string.IsNullOrWhiteSpace(m_AvProPlayback.ErrorMessage) is false)
            {
                AddMessage(m_OutputContainer, m_AvProPlayback.ErrorMessage, "story-playback__message--error");
            }

            if (IsSeekRequested(command))
            {
                RenderVideoSeekControls();
                if (m_Session?.CurrentFrame?.Tracks?.Count > 1 || m_Session?.CurrentFrame?.Choices?.Count > 0)
                {
                    AddMessage(
                        m_OutputContainer,
                        "提示：拖动视频进度只改变媒体时间，不会回滚或快进剧情等待、选项、事件或结算状态。",
                        "story-playback__message--empty");
                }
            }

            RenderVideoQualityControls();
        }

        private void RenderWaitTrack(FrameTrack track)
        {
            AddSectionTitle(m_OutputContainer, "等待");
            AddBranchMeta(track);
            AddMeta(m_OutputContainer, "等待秒数", track.WaitSeconds.ToString("0.###"));
            AddMeta(m_OutputContainer, "当前时间", m_Session.CurrentTime.ToString("0.###"));
            m_OutputContainer.Add(CreateButton(
                "完成等待",
                $"调用 StoryModule.Evaluate({track.WaitSeconds:0.###}) 推进等待。",
                () => Evaluate(track.WaitSeconds)));
        }

        private void RenderFrameActions(Frame frame)
        {
            if (frame.WaitsForChoice || frame.WaitsForCommand || frame.WaitsForTime)
            {
                return;
            }

            m_OutputContainer.Add(CreateButton("继续", "调用 StoryModule.Continue() 推进剧情。", Continue));
        }

        private void RenderCompleted()
        {
            AddMessage(m_OutputContainer, "剧情已完成。", "story-playback__message--success");
            m_OutputContainer.Add(CreateButton("重启当前章节", "重新编译并从当前章节入口播放。", RestartSession));
        }

        private void RefreshHistory()
        {
            if (m_HistoryContainer == null)
            {
                return;
            }

            m_HistoryContainer.Clear();
            AddSectionTitle(m_HistoryContainer, "历史");
            if (m_Session == null || m_Session.History.Count == 0)
            {
                AddMessage(m_HistoryContainer, "暂无历史。", "story-playback__message--empty");
                return;
            }

            for (var i = 0; i < m_Session.History.Count; i++)
            {
                var record = m_Session.History[i];
                var row = new Label($"#{record.Index} {record.Action} / {record.Kind} / {record.EpisodeId}:{record.StepId}\n{record.Summary}");
                row.AddToClassList("story-playback__history-row");
                row.tooltip = "播放窗口记录的输出历史，不修改 runtime History 契约。";
                m_HistoryContainer.Add(row);
            }
        }

        private void Continue()
        {
            m_Session?.Continue();
            Refresh();
        }

        private void Select(string choiceId)
        {
            m_Session?.Select(choiceId);
            Refresh();
        }

        private void CompleteCommand(string commandId, string outcomeId)
        {
            StopVideoPlayback();
            m_Session?.CompleteCommand(commandId, outcomeId);
            Refresh();
        }

        private void Evaluate(double time)
        {
            m_Session?.Evaluate(time);
            Refresh();
        }

        private string StatusText()
        {
            if (m_Session == null)
            {
                return "未启动。";
            }

            if (m_Session.Report != null && m_Session.Report.HasErrors)
            {
                return $"编译失败：{m_Session.Report.Issues.Count(x => x.Severity == ValidationSeverity.Error)} 个错误。";
            }

            if (string.IsNullOrWhiteSpace(m_Session.ErrorMessage) is false)
            {
                return m_Session.ErrorMessage;
            }

            if (m_Session.CurrentFrame == null)
            {
                return "未输出。";
            }

            if (m_Session.CurrentFrame.IsCompleted && m_Session.History.Count <= 1)
            {
                return "已完成：入口路径没有可停顿输出。";
            }

            return $"正在播放：{FrameTitle(m_Session.CurrentFrame)}";
        }

        private void OnEditorUpdate()
        {
            if (m_AvProPlayback == null)
            {
                return;
            }

            m_AvProPlayback.Update();
            UpdateVideoUi();

            var currentFrame = m_Session?.CurrentFrame;
            var command = FindPlayVideoCommand(currentFrame, m_AvProPlayback.CurrentCommandId);
            if (command == null)
            {
                StopVideoPlayback();
                return;
            }

            if (m_AvProPlayback.IsFinished && currentFrame.WaitsForCommand)
            {
                var outcomeId = CompletionOutcome(command);
                StopVideoPlayback();
                CompleteCommand(command.CommandId, outcomeId);
                return;
            }

            if (m_LastVideoFinished != m_AvProPlayback.IsFinished ||
                string.Equals(m_LastVideoError, m_AvProPlayback.ErrorMessage, StringComparison.Ordinal) is false)
            {
                m_LastVideoFinished = m_AvProPlayback.IsFinished;
                m_LastVideoError = m_AvProPlayback.ErrorMessage;
                Refresh();
            }
        }

        private void UpdateVideoUi()
        {
            if (m_VideoImage != null)
            {
                m_VideoImage.image = VideoPreviewTexture();
                m_VideoImage.uv = VideoPreviewUv();
            }

            if (m_VideoStatusLabel != null)
            {
                m_VideoStatusLabel.text = VideoStatusText();
            }

            UpdateVideoSeekUi();
            UpdateVideoQualityUi();

            if (m_AvProPlayback.HasFirstFrame)
            {
                Repaint();
            }
        }

        private void RenderVideoSeekControls()
        {
            m_VideoSeekSlider = new Slider(0f, 1f)
            {
                label = "时间",
                tooltip = "拖动只改变 AVPro 当前媒体时间，不推进 Story runtime。"
            };
            m_VideoSeekSlider.AddToClassList("story-playback__video-seek");
            m_VideoSeekSlider.RegisterValueChangedCallback(OnVideoSeekChanged);
            m_OutputContainer.Add(m_VideoSeekSlider);

            m_VideoSeekTimeLabel = new Label();
            m_VideoSeekTimeLabel.AddToClassList("story-playback__video-status");
            m_OutputContainer.Add(m_VideoSeekTimeLabel);
            UpdateVideoSeekUi();
        }

        private void UpdateVideoSeekUi()
        {
            if (m_VideoSeekSlider == null)
            {
                return;
            }

            if (m_AvProPlayback == null || m_AvProPlayback.CanSeek is false)
            {
                m_VideoSeekSlider.SetEnabled(false);
                if (m_VideoSeekTimeLabel != null)
                {
                    m_VideoSeekTimeLabel.text = "等待可用的视频时长。";
                }

                return;
            }

            var duration = m_AvProPlayback.DurationSeconds;
            m_UpdatingVideoSeek = true;
            m_VideoSeekSlider.SetEnabled(true);
            m_VideoSeekSlider.lowValue = 0f;
            m_VideoSeekSlider.highValue = Mathf.Max(0.001f, (float)duration);
            m_VideoSeekSlider.SetValueWithoutNotify((float)Math.Min(m_AvProPlayback.CurrentTimeSeconds, duration));
            m_UpdatingVideoSeek = false;

            if (m_VideoSeekTimeLabel != null)
            {
                m_VideoSeekTimeLabel.text = $"{FormatTime(m_AvProPlayback.CurrentTimeSeconds)} / {FormatTime(duration)}";
            }
        }

        private void OnVideoSeekChanged(ChangeEvent<float> evt)
        {
            if (m_UpdatingVideoSeek || m_AvProPlayback == null || m_AvProPlayback.CanSeek is false)
            {
                return;
            }

            m_AvProPlayback.Seek(evt.newValue);
            UpdateVideoSeekUi();
        }

        private Texture VideoPreviewTexture()
        {
            return m_AvProPlayback != null && m_AvProPlayback.HasFirstFrame
                ? m_AvProPlayback.CurrentTexture
                : null;
        }

        private Rect VideoPreviewUv()
        {
            var texture = VideoPreviewTexture();
            if (texture == null)
            {
                return s_DefaultVideoUvRect;
            }

            var width = m_VideoImage?.resolvedStyle.width ?? 0f;
            var height = m_VideoImage?.resolvedStyle.height ?? 0f;
            var targetAspect = width > 0f && height > 0f ? width / height : 16f / 9f;
            return VideoSurfaceBinder.CalculateCoverUvRect(
                targetAspect,
                (float)texture.width / texture.height,
                m_AvProPlayback?.RequiresVerticalFlip == true);
        }

        private void RenderVideoQualityControls()
        {
            var handle = m_AvProPlayback?.Handle;
            if (handle?.CanSelectQuality != true)
            {
                return;
            }

            var choices = BuildQualityChoices(handle);
            m_VideoQualityPopup = new PopupField<string>("清晰度", choices, QualityIndex(handle));
            m_VideoQualityPopup.RegisterValueChangedCallback(OnVideoQualityChanged);
            m_OutputContainer.Add(m_VideoQualityPopup);
        }

        private void UpdateVideoQualityUi()
        {
            var handle = m_AvProPlayback?.Handle;
            if (m_VideoQualityPopup == null || handle == null)
            {
                return;
            }

            m_VideoQualityPopup.SetValueWithoutNotify(BuildQualityChoices(handle)[QualityIndex(handle)]);
        }

        private void OnVideoQualityChanged(ChangeEvent<string> evt)
        {
            var handle = m_AvProPlayback?.Handle;
            if (handle == null)
            {
                return;
            }

            var selection = string.Equals(evt.newValue, "Auto", StringComparison.Ordinal)
                ? new VideoQualitySelection(VideoQualityMode.Auto)
                : new VideoQualitySelection(VideoQualityMode.FixedHeight, ParseQualityHeight(evt.newValue));
            m_VideoQualityPopup.SetEnabled(false);
            SwitchVideoQualityAsync(selection).Forget(Debug.LogException);
        }

        private async UniTask SwitchVideoQualityAsync(VideoQualitySelection selection)
        {
            try
            {
                await m_AvProPlayback.SetQualityAsync(selection);
            }
            catch (Exception exception)
            {
                m_LastVideoError = exception.Message;
                AddMessage(m_OutputContainer, exception.Message, "story-playback__message--error");
            }
            finally
            {
                m_VideoQualityPopup?.SetEnabled(true);
                UpdateVideoQualityUi();
            }
        }

        private static List<string> BuildQualityChoices(VideoPlayableHandle handle)
        {
            var result = new List<string>();
            if (handle.SupportsAutoQuality)
            {
                result.Add("Auto");
            }

            for (var i = 0; i < handle.QualityOptions.Count; i++)
            {
                result.Add(FormatQuality(handle.QualityOptions[i].Height));
            }

            return result;
        }

        private static int QualityIndex(VideoPlayableHandle handle)
        {
            if (handle.Quality.Mode == VideoQualityMode.Auto)
            {
                return 0;
            }

            var offset = handle.SupportsAutoQuality ? 1 : 0;
            for (var i = 0; i < handle.QualityOptions.Count; i++)
            {
                if (handle.QualityOptions[i].Height == handle.Quality.Height)
                {
                    return i + offset;
                }
            }

            return offset;
        }

        private static int ParseQualityHeight(string value)
        {
            if (string.Equals(value, "2K", StringComparison.Ordinal)) return 1440;
            if (string.Equals(value, "4K", StringComparison.Ordinal)) return 2160;
            return int.Parse(value.TrimEnd('p'));
        }

        private static string FormatQuality(int height)
        {
            return height == 1440 ? "2K" : height == 2160 ? "4K" : $"{height}p";
        }

        private string VideoStatusText()
        {
            if (m_AvProPlayback == null || string.IsNullOrWhiteSpace(m_AvProPlayback.CurrentCommandId))
            {
                return "AVPro 未启动。";
            }

            if (string.IsNullOrWhiteSpace(m_AvProPlayback.ErrorMessage) is false)
            {
                return m_AvProPlayback.ErrorMessage;
            }

            if (m_AvProPlayback.IsFinished)
            {
                return "AVPro 播放完成。";
            }

            if (m_AvProPlayback.IsPlaying)
            {
                return "AVPro 正在播放。";
            }

            return "AVPro 正在打开视频。";
        }

        private void RegisterEditorUpdate()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private void UnregisterEditorUpdate()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void StopVideoIfStale(Frame frame)
        {
            if (m_AvProPlayback == null || string.IsNullOrWhiteSpace(m_AvProPlayback.CurrentCommandId))
            {
                return;
            }

            if (FindPlayVideoCommand(frame, m_AvProPlayback.CurrentCommandId) == null)
            {
                StopVideoPlayback();
            }
        }

        private void StopVideoPlayback()
        {
            m_VideoImage = null;
            m_VideoStatusLabel = null;
            m_VideoSeekSlider = null;
            m_VideoSeekTimeLabel = null;
            m_VideoQualityPopup = null;
            m_RenderedVideoCommandId = null;
            m_LastVideoFinished = false;
            m_LastVideoError = null;
            m_UpdatingVideoSeek = false;
            m_AvProPlayback?.Stop();
        }

        private void ShutdownAvProPlayback()
        {
            if (m_AvProPlayback != null)
            {
                m_AvProPlayback.Dispose();
                m_AvProPlayback = null;
            }
        }

        private void AddTags(IReadOnlyList<string> tags)
        {
            if (tags == null || tags.Count == 0)
            {
                return;
            }

            AddMeta(m_OutputContainer, "标签", string.Join(", ", tags));
        }

        private void AddBranchMeta(FrameTrack track)
        {
            if (string.IsNullOrWhiteSpace(track?.BranchId))
            {
                return;
            }

            AddMeta(m_OutputContainer, "轨道", string.IsNullOrWhiteSpace(track.BranchLabel) ? track.BranchId : $"{track.BranchLabel} ({track.BranchId})");
        }

        private static string BranchPrefix(string branchId)
        {
            return string.IsNullOrWhiteSpace(branchId) ? string.Empty : $"[{branchId}] ";
        }

        private static string FrameTitle(Frame frame)
        {
            if (frame == null)
            {
                return "无帧";
            }

            if (frame.IsCompleted)
            {
                return "完成";
            }

            if (frame.Choices.Count > 0)
            {
                return "选项";
            }

            if (frame.Tracks.Count > 0)
            {
                return TrackTitle(frame.Tracks[0]);
            }

            return "剧情帧";
        }

        private static string TrackTitle(FrameTrack track)
        {
            switch (track.Kind)
            {
                case FrameTrackKind.Text:
                    return "文本";
                case FrameTrackKind.Command:
                    return "命令";
                case FrameTrackKind.Wait:
                    return "等待";
                default:
                    return track.Kind.ToString();
            }
        }

        private static IEnumerable<string> OutcomeIds(global::GameDeveloperKit.Story.Model.Command command)
        {
            if (command.OutcomePorts != null && command.OutcomePorts.Count > 0)
            {
                return command.OutcomePorts.Where(x => string.IsNullOrWhiteSpace(x) is false);
            }

            if (command.OutcomeTargets != null && command.OutcomeTargets.Count > 0)
            {
                return command.OutcomeTargets.Keys.Where(x => string.IsNullOrWhiteSpace(x) is false);
            }

            return Array.Empty<string>();
        }

        private static bool IsPlayVideoCommand(global::GameDeveloperKit.Story.Model.Command command)
        {
            return command != null &&
                   string.Equals(command.Name, PlayVideoCommandName, StringComparison.Ordinal);
        }

        private static bool IsSeekRequested(global::GameDeveloperKit.Story.Model.Command command)
        {
            return command?.Arguments.GetBoolean(MediaCommandNames.VideoSeekableArgument, false) == true;
        }

        private bool CanShowCommandCompletion(global::GameDeveloperKit.Story.Model.Command command)
        {
            if (IsPlayVideoCommand(command) is false)
            {
                return true;
            }

            return m_AvProPlayback == null ||
                   string.Equals(m_AvProPlayback.CurrentCommandId, command.CommandId, StringComparison.Ordinal) is false ||
                   string.IsNullOrWhiteSpace(m_AvProPlayback.ErrorMessage) is false;
        }

        private static global::GameDeveloperKit.Story.Model.Command FindPlayVideoCommand(Frame frame, string commandId)
        {
            if (frame?.Tracks == null || string.IsNullOrWhiteSpace(commandId))
            {
                return null;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var command = frame.Tracks[i]?.Command;
                if (IsPlayVideoCommand(command) &&
                    string.Equals(command.CommandId, commandId, StringComparison.Ordinal))
                {
                    return command;
                }
            }

            return null;
        }

        private static string CompletionOutcome(global::GameDeveloperKit.Story.Model.Command command)
        {
            if (command?.OutcomePorts != null && command.OutcomePorts.Count > 0)
            {
                for (var i = 0; i < command.OutcomePorts.Count; i++)
                {
                    if (string.Equals(command.OutcomePorts[i], CompletedOutcomeId, StringComparison.Ordinal))
                    {
                        return CompletedOutcomeId;
                    }
                }

                return command.OutcomePorts.FirstOrDefault(x => string.IsNullOrWhiteSpace(x) is false);
            }

            if (command?.OutcomeTargets != null && command.OutcomeTargets.Count > 0)
            {
                if (command.OutcomeTargets.ContainsKey(CompletedOutcomeId))
                {
                    return CompletedOutcomeId;
                }

                return command.OutcomeTargets.Keys.FirstOrDefault(x => string.IsNullOrWhiteSpace(x) is false);
            }

            return null;
        }

        private static string FormatArgument(Value value)
        {
            return value.IsString ? value.StringValue : value.ToString();
        }

        private static string FormatAssetPreview(Value value)
        {
            if (!value.TryGetString(out var text) || string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (text.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(text);
                if (asset == null)
                {
                    return $"资源未解析：{text}";
                }

                return $"资源：{asset.name} ({asset.GetType().Name})";
            }

            return text.IndexOf(':') >= 0 ? $"资源键：{text}" : null;
        }

        private static void AddSectionTitle(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.AddToClassList("story-playback__section-title");
            parent.Add(label);
        }

        private static void AddMeta(VisualElement parent, string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("story-playback__meta");
            var nameLabel = new Label(label) { tooltip = label };
            nameLabel.AddToClassList("story-playback__meta-name");
            var valueLabel = new Label(SafeText(value, "-")) { tooltip = value ?? string.Empty };
            valueLabel.AddToClassList("story-playback__meta-value");
            row.Add(nameLabel);
            row.Add(valueLabel);
            parent.Add(row);
        }

        private static void AddMessage(VisualElement parent, string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList("story-playback__message");
            label.AddToClassList(className);
            parent.Add(label);
        }

        private static Button CreateButton(string text, string tooltip, Action click)
        {
            var button = new Button(click) { text = text, tooltip = tooltip };
            button.AddToClassList("story-playback__button");
            return button;
        }

        private static string SafeText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string FormatTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d)
            {
                seconds = 0d;
            }

            var totalSeconds = Mathf.FloorToInt((float)seconds);
            var minutes = totalSeconds / 60;
            var remainingSeconds = totalSeconds % 60;
            return $"{minutes:00}:{remainingSeconds:00}";
        }
    }
}
