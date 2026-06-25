using System;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Procedure;
using GameDeveloperKit.Story;
using GameDeveloperKit.UI;
using UnityEngine;

namespace GameDeveloperKit.Scripts.StoryTest
{
    /// <summary>
    /// Simple project-level procedure for testing runtime story playback.
    /// </summary>
    public sealed class StoryTestProcedure : ProcedureBase
    {
        private StoryPlayerView m_PlayerView;
        private bool m_PlaybackStarted;
        private bool m_OwnsPlayerView;

        /// <inheritdoc />
        public override UniTask OnEnterAsync(ProcedureBase previous, object userData)
        {
            var request = ResolveRequest(userData);
            m_PlayerView = ResolvePlayerView(request, out m_OwnsPlayerView);
            StartPlayback(request, m_PlayerView);
            m_PlaybackStarted = true;
            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public override UniTask OnLeaveAsync(ProcedureBase next, object userData)
        {
            if (m_PlaybackStarted && m_PlayerView != null)
            {
                m_PlayerView.StopPlayback();
            }

            if (m_OwnsPlayerView && m_PlayerView != null)
            {
                DestroyPlayerView(m_PlayerView);
            }

            m_PlayerView = null;
            m_PlaybackStarted = false;
            m_OwnsPlayerView = false;
            return UniTask.CompletedTask;
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

        private static StoryPlayerView ResolvePlayerView(StoryTestRequest request, out bool ownsPlayerView)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            ownsPlayerView = false;
            if (request.PlayerView != null)
            {
                return request.PlayerView;
            }

#if UNITY_2023_1_OR_NEWER
            var playerView = UnityEngine.Object.FindFirstObjectByType<StoryPlayerView>();
#else
            var playerView = UnityEngine.Object.FindObjectOfType<StoryPlayerView>();
#endif
            if (playerView != null)
            {
                return playerView;
            }

            if (request.PlayerViewPrefab != null)
            {
                ownsPlayerView = true;
                return InstantiatePlayerView(request.PlayerViewPrefab, ResolveStoryPlaybackLayer());
            }

            ownsPlayerView = true;
            return StoryPlayerView.CreateDefault(ResolveStoryPlaybackLayer());
        }

        private static void StartPlayback(StoryTestRequest request, StoryPlayerView playerView)
        {
            if (request.Program != null)
            {
                RegisterProgramIfNeeded(request.Program);
                playerView.Play(request.Program, request.ChapterId);
            }
            else
            {
                playerView.PlayRegistered(request.StoryId, request.ChapterId);
            }

            if (playerView.LastError != null)
            {
                throw new GameException("StoryTestProcedure failed to start story playback.", playerView.LastError);
            }
        }

        private static void RegisterProgramIfNeeded(StoryProgram program)
        {
            var storyModule = App.Story;
            if (storyModule.HasProgram(program.StoryId))
            {
                return;
            }

            storyModule.Register(program);
        }

        private static void DestroyPlayerView(StoryPlayerView playerView)
        {
            if (playerView == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(playerView.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(playerView.gameObject);
            }
        }

        private static StoryPlayerView InstantiatePlayerView(StoryPlayerView prefab, Transform parent)
        {
            var playerView = UnityEngine.Object.Instantiate(prefab, parent, false);
            playerView.gameObject.name = prefab.gameObject.name;
            if (playerView.gameObject.activeSelf is false)
            {
                playerView.gameObject.SetActive(true);
            }

            return playerView;
        }

        private static Transform ResolveStoryPlaybackLayer()
        {
            return App.UI.GetLayerRoot(UILayer.StoryPlayback);
        }
    }
}
