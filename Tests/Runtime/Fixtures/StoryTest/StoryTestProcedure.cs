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
        private PlaybackView m_PlaybackView;
        private bool m_PlaybackStarted;

        /// <inheritdoc />
        public override async UniTask OnEnterAsync(ProcedureBase previous, object userData)
        {
            var request = ResolveRequest(userData);
            m_PlaybackView = await App.UI.OpenAsync<PlaybackView>();
            try
            {
                StartPlayback(request, m_PlaybackView);
                m_PlaybackStarted = true;
            }
            catch
            {
                await App.UI.CloseAsync<PlaybackView>();
                m_PlaybackView = null;
                throw;
            }
        }

        /// <inheritdoc />
        public override async UniTask OnLeaveAsync(ProcedureBase next, object userData)
        {
            if (m_PlaybackStarted && m_PlaybackView != null)
            {
                m_PlaybackView.StopPlayback();
            }

            if (m_PlaybackView != null)
            {
                await App.UI.CloseAsync<PlaybackView>();
            }

            m_PlaybackView = null;
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

        private static void StartPlayback(StoryTestRequest request, PlaybackView playbackView)
        {
            if (request.Program != null)
            {
                RegisterProgramIfNeeded(request.Program);
                playbackView.Play(request.Program, request.VolumeId, request.EpisodeId);
            }
            else
            {
                playbackView.PlayRegistered(request.StoryId, request.VolumeId, request.EpisodeId);
            }

            if (playbackView.LastError != null)
            {
                throw new GameException("StoryTestProcedure failed to start story playback.", playbackView.LastError);
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
