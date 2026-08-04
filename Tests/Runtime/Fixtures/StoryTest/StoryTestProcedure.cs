using System;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Procedure;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Playback;
using GameDeveloperKit.Tests;

namespace GameDeveloperKit.Scripts.StoryTest
{
    /// <summary>
    /// Simple project-level procedure for testing runtime story playback.
    /// </summary>
    public sealed class StoryTestProcedure : ProcedureBase
    {
        private StoryPlaybackWindow m_PlaybackWindow;
        private bool m_PlaybackStarted;

        /// <inheritdoc />
        public override async UniTask OnEnterAsync(ProcedureBase previous, object userData)
        {
            var request = ResolveRequest(userData);
            m_PlaybackWindow = await App.UI.OpenAsync<StoryPlaybackWindow>();
            try
            {
                await StartPlaybackAsync(request, m_PlaybackWindow);
                m_PlaybackStarted = true;
            }
            catch
            {
                await App.UI.CloseAsync<StoryPlaybackWindow>();
                m_PlaybackWindow = null;
                throw;
            }
        }

        /// <inheritdoc />
        public override async UniTask OnLeaveAsync(ProcedureBase next, object userData)
        {
            if (m_PlaybackStarted && m_PlaybackWindow != null)
            {
                m_PlaybackWindow.StopPlayback();
            }

            if (m_PlaybackWindow != null)
            {
                await App.UI.CloseAsync<StoryPlaybackWindow>();
            }

            m_PlaybackWindow = null;
            m_PlaybackStarted = false;
        }

        /// <inheritdoc />
        public override void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
        }

        private static StoryTestRequest ResolveRequest(object userData)
        {
            switch (userData)
            {
                case StoryTestRequest request:
                    return request;
                case StoryTestRequestAsset requestAsset:
                    return requestAsset.ToRequest();
                default:
                    throw new GameException("StoryTestProcedure requires StoryTestRequest or StoryTestRequestAsset user data.");
            }
        }

        private static async UniTask StartPlaybackAsync(
            StoryTestRequest request,
            StoryPlaybackWindow playbackWindow)
        {
            if (request.Program != null)
            {
                RegisterProgramIfNeeded(request.Program);
                await playbackWindow.PlayStoryAsync(
                    request.Program,
                    request.VolumeId,
                    request.EpisodeId);
            }
            else
            {
                await playbackWindow.PlayRegisteredAsync(
                    request.StoryId,
                    request.VolumeId,
                    request.EpisodeId);
            }

            if (playbackWindow.LastError != null)
            {
                throw new GameException(
                    "StoryTestProcedure failed to start story playback.",
                    playbackWindow.LastError);
            }
        }

        private static void RegisterProgramIfNeeded(Program program)
        {
            var storyModule = App.Story;
            if (storyModule.HasProgram(program.StoryId))
            {
                return;
            }

            storyModule.Register(program);
        }

    }
}
