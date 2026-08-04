using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Text;
using GameDeveloperKit.UI;
using UnityEngine;
using StoryProgram = GameDeveloperKit.Story.Model.Program;

namespace GameDeveloperKit.Story.Playback
{
    /// <summary>
    /// 组合剧情推进与通用视频播放器能力的业务窗口。
    /// </summary>
    [UIOption("Assets/Bundles/Playback/StoryPlaybackWindow.prefab", 500, CacheEnabled = false)]
    public partial class StoryPlaybackWindow : VideoPlayerWindow
    {
        private StoryModule m_StoryModule;
        private StoryModule m_StoryEventSource;
        private ITextResolver m_TextResolver;
        private DefaultInteractionChannel m_DefaultInteractionChannel;
        private IInteractionChannel m_InteractionChannelOverride;
        private IInteractionChannel m_ActiveInteractionChannel;
        private CancellationTokenSource m_PlaybackCancellation;
        private VideoPlayableHandle m_ObservedVideo;
        private Frame m_CurrentFrame;
        private Episode m_CurrentEpisode;
        private string m_ActiveStoryId;
        private int m_SessionVersion;
        private bool m_FirstVideoFrameReported;
        private VideoQualitySelection m_PreferredQuality = new VideoQualitySelection(VideoQualityMode.Auto);

        public StoryModule StoryModule => m_StoryModule;

        public Frame CurrentFrame => m_CurrentFrame;

        public string ActiveStoryId => m_ActiveStoryId;

        public Exception LastError { get; private set; }

        public event Action<VideoPlayableHandle> FirstVideoFrameReady;

        public event Action ExitRequested;

        public override async UniTask OnAwakeAsync()
        {
            await base.OnAwakeAsync();
            BindStoryDocument();
            EnsureDefaultInteractionChannel();
            ResolveStoryModule();
            SubscribeStoryEvents();
            ResetStoryPresentation();
        }

        public override async UniTask OnOpenAsync()
        {
            await base.OnOpenAsync();
            SetPresentationVisible(true);
        }

        public override void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
            base.OnUpdate(deltaTime, unscaledDeltaTime);
            UpdateLoadingSpinner(unscaledDeltaTime);
            m_ActiveInteractionChannel?.Tick(deltaTime);

            if (m_CurrentFrame != null &&
                m_CurrentFrame.WaitsForTime &&
                m_CurrentFrame.IsCompleted is false &&
                LastError == null)
            {
                ExecuteAdvance(() => RequireStoryModule().Evaluate(deltaTime));
            }
        }

        public void ConfigureModules(StoryModule storyModule)
        {
            m_StoryModule = storyModule ?? throw new ArgumentNullException(nameof(storyModule));
            SubscribeStoryEvents();
        }

        public void SetTextResolver(ITextResolver resolver)
        {
            m_TextResolver = resolver;
        }

        public void SetInteractionChannel(IInteractionChannel channel)
        {
            if (ReferenceEquals(m_InteractionChannelOverride, channel))
            {
                return;
            }

            m_InteractionChannelOverride?.Dispose();
            m_InteractionChannelOverride = channel;
            m_ActiveInteractionChannel = null;
        }

        public UniTask PlayStoryAsync(
            StoryProgram program,
            string volumeId,
            string episodeId,
            CancellationToken cancellationToken = default)
        {
            if (program == null)
            {
                throw new ArgumentNullException(nameof(program));
            }

            return StartPlaybackAsync(
                program.StoryId,
                program,
                module => module.Start(program, volumeId, episodeId),
                cancellationToken);
        }

        public UniTask PlayRegisteredAsync(
            string storyId,
            string volumeId,
            string episodeId,
            CancellationToken cancellationToken = default)
        {
            ValidateStoryId(storyId);
            var module = RequireStoryModule();
            if (module.TryGetProgram(storyId, out var program) is false)
            {
                throw new GameException($"Story program is not registered. story:{storyId}");
            }

            return StartPlaybackAsync(
                storyId,
                program,
                value => value.StartEpisode(storyId, volumeId, episodeId),
                cancellationToken);
        }

        public void Continue()
        {
            ExecuteAdvance(() => RequireStoryModule().Continue());
        }

        public void Select(string choiceId)
        {
            if (string.IsNullOrWhiteSpace(choiceId))
            {
                throw new ArgumentException("Story choice id cannot be empty.", nameof(choiceId));
            }

            PrepareChoiceVideoSelection(choiceId);
            ExecuteAdvance(() => RequireStoryModule().Select(choiceId));
        }

        public void CompleteInstruction(string instructionId)
        {
            if (string.IsNullOrWhiteSpace(instructionId))
            {
                throw new ArgumentException("Story instruction id cannot be empty.", nameof(instructionId));
            }

            ExecuteAdvance(() => RequireStoryModule().CompleteInstruction(instructionId));
        }

        public void Evaluate(double time)
        {
            ExecuteAdvance(() => RequireStoryModule().Evaluate(time));
        }

        public void StopPlayback()
        {
            m_SessionVersion++;
            CancelPlaybackSession();
            DetachObservedVideo();
            StopStoryMedia();
            ReleaseVideoLookahead();
            ReleaseChoiceVideoLookaheads();
            StopCurrentVideo();
            m_ActiveInteractionChannel?.OnStoryStopped();
            ClearStoryPresentation();
            m_ActiveInteractionChannel = null;
            m_ActiveStoryId = null;
            m_CurrentEpisode = null;
            m_CurrentFrame = null;
            m_FirstVideoFrameReported = false;
            SetLoadingVisible(false);
            LastError = null;
        }

        public override void Release()
        {
            StopPlayback();
            UnsubscribeStoryEvents();
            m_InteractionChannelOverride?.Dispose();
            m_InteractionChannelOverride = null;
            m_DefaultInteractionChannel?.Dispose();
            m_DefaultInteractionChannel = null;
            m_StoryModule = null;
            m_TextResolver = null;
            m_PreferredQuality = new VideoQualitySelection(VideoQualityMode.Auto);
            FirstVideoFrameReady = null;
            ExitRequested = null;
            ReleaseStoryDocument();
            base.Release();
        }

        protected virtual UniTask OnPlaybackAwakeAsync(
            InteractionContext context,
            CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        protected virtual void OnEpisodeChanged(EpisodeInteractionContext context)
        {
        }

        protected virtual void OnEpisodeCompleted(EpisodeCompletion completion)
        {
        }

        protected virtual void OnStoryVideoFirstFrameReady(VideoPlayableHandle playback)
        {
        }

        protected override void OnVideoOpened(VideoPlayableHandle playback)
        {
            base.OnVideoOpened(playback);
            ObserveVideo(playback);
        }

        protected override void OnQualityChanged(VideoQualitySelection selection)
        {
            m_PreferredQuality = selection;
        }

        protected override void OnVideoPlaybackCompleted(VideoPlayableHandle playback)
        {
            base.OnVideoPlaybackCompleted(playback);
            HandleStoryVideoCompleted(playback);
        }

        protected override void OnBackRequested()
        {
            var handler = ExitRequested;
            if (handler == null)
            {
                base.OnBackRequested();
                return;
            }

            handler.Invoke();
        }

        internal string ResolveText(TextReference reference)
        {
            m_TextResolver ??= new LocalizationTextResolver();
            return m_TextResolver.Resolve(reference);
        }

        internal bool IsSessionCurrent(int sessionVersion)
        {
            return sessionVersion == m_SessionVersion &&
                   m_PlaybackCancellation?.IsCancellationRequested == false;
        }

        internal int SessionVersion => m_SessionVersion;

        internal CancellationToken SessionCancellationToken =>
            m_PlaybackCancellation?.Token ?? default;

        internal void SetPlaybackError(Exception exception)
        {
            if (exception is OperationCanceledException &&
                m_PlaybackCancellation?.IsCancellationRequested == true)
            {
                return;
            }

            SetError(exception);
        }

        internal void AdvanceCompletedInstruction(StoryInstruction instruction)
        {
            if (instruction == null)
            {
                return;
            }

            CompleteInstruction(instruction.InstructionId);
        }

        private async UniTask StartPlaybackAsync(
            string storyId,
            StoryProgram program,
            Func<StoryModule, Runner> start,
            CancellationToken cancellationToken)
        {
            ValidateStoryId(storyId);
            if (start == null)
            {
                throw new ArgumentNullException(nameof(start));
            }

            StopPlayback();
            ResolveStoryModule();
            SubscribeStoryEvents();
            var session = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            m_PlaybackCancellation = session;
            var sessionVersion = ++m_SessionVersion;
            m_FirstVideoFrameReported = false;
            LastError = null;
            ClearError();
            SetLoadingVisible(true);

            try
            {
                var channel = ResolveInteractionChannel();
                m_ActiveInteractionChannel = channel;
                var context = new InteractionContext(m_StoryModule, storyId, program);
                await OnPlaybackAwakeAsync(context, session.Token);
                session.Token.ThrowIfCancellationRequested();
                await channel.OnAwake(context, session.Token);
                session.Token.ThrowIfCancellationRequested();
                if (IsSessionCurrent(sessionVersion) is false)
                {
                    return;
                }

                m_ActiveStoryId = storyId;
                channel.OnStoryStarted(context);
                var runner = start(m_StoryModule);
                PresentFrame(runner.CurrentFrame);
            }
            catch (OperationCanceledException) when (session.IsCancellationRequested)
            {
                SetLoadingVisible(false);
            }
            catch (Exception exception)
            {
                if (IsSessionCurrent(sessionVersion))
                {
                    SetError(exception);
                }
            }
        }

        private void ExecuteAdvance(Func<Frame> advance)
        {
            if (advance == null)
            {
                throw new ArgumentNullException(nameof(advance));
            }

            try
            {
                var frame = advance();
                if (ReferenceEquals(m_CurrentFrame, frame) is false)
                {
                    PresentFrame(frame);
                }
            }
            catch (Exception exception)
            {
                SetError(exception);
            }
        }

        private void ObserveVideo(VideoPlayableHandle playback)
        {
            DetachObservedVideo();
            m_ObservedVideo = playback;
            if (m_ObservedVideo == null)
            {
                return;
            }

            m_ObservedVideo.FirstFrameReady += HandleVideoFirstFrameReady;
            if (m_ObservedVideo.HasFirstFrame)
            {
                HandleVideoFirstFrameReady(m_ObservedVideo);
            }
        }

        private void DetachObservedVideo()
        {
            if (m_ObservedVideo != null)
            {
                m_ObservedVideo.FirstFrameReady -= HandleVideoFirstFrameReady;
                m_ObservedVideo = null;
            }
        }

        private void HandleVideoFirstFrameReady(VideoPlayableHandle playback)
        {
            if (!ReferenceEquals(Playback, playback) ||
                playback?.HasFirstFrame != true)
            {
                return;
            }

            PrewarmNextVideo(playback);
            PrewarmEpisodeChoiceVideos(m_CurrentFrame);

            if (m_FirstVideoFrameReported)
            {
                return;
            }

            m_FirstVideoFrameReported = true;
            SetLoadingVisible(false);
            OnStoryVideoFirstFrameReady(playback);
            FirstVideoFrameReady?.Invoke(playback);
        }

        private StoryModule RequireStoryModule()
        {
            ResolveStoryModule();
            return m_StoryModule;
        }

        private void ResolveStoryModule()
        {
            m_StoryModule ??= App.Story;
        }

        private void CancelPlaybackSession()
        {
            var cancellation = m_PlaybackCancellation;
            m_PlaybackCancellation = null;
            cancellation?.Cancel();
            cancellation?.Dispose();
        }

        private void SubscribeStoryEvents()
        {
            if (ReferenceEquals(m_StoryEventSource, m_StoryModule))
            {
                return;
            }

            UnsubscribeStoryEvents();
            m_StoryEventSource = m_StoryModule;
            if (m_StoryEventSource != null)
            {
                m_StoryEventSource.EpisodeCompleted += HandleEpisodeCompleted;
            }
        }

        private void UnsubscribeStoryEvents()
        {
            if (m_StoryEventSource == null)
            {
                return;
            }

            m_StoryEventSource.EpisodeCompleted -= HandleEpisodeCompleted;
            m_StoryEventSource = null;
        }

        private void HandleEpisodeCompleted(EpisodeCompletion completion)
        {
            if (completion == null ||
                string.Equals(completion.StoryId, m_ActiveStoryId, StringComparison.Ordinal) is false)
            {
                return;
            }

            OnEpisodeCompleted(completion);
        }

        private static void ValidateStoryId(string storyId)
        {
            if (string.IsNullOrWhiteSpace(storyId))
            {
                throw new ArgumentException("Story id cannot be empty.", nameof(storyId));
            }
        }

    }
}
