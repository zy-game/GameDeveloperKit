using System;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Procedure;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 通过 ProcedureModule 播放剧情的流程。
    /// </summary>
    public sealed class StoryProcedure : ProcedureBase
    {
        private StoryPlayerView m_PlayerView;
        private StoryPlayerView m_InstantiatedPlayer;
        private bool m_DestroyInstantiatedPlayerOnLeave = true;

        /// <summary>
        /// 当前播放器。
        /// </summary>
        public StoryPlayerView PlayerView => m_PlayerView;

        /// <inheritdoc />
        public override UniTask OnEnterAsync(ProcedureBase previous, object userData)
        {
            var request = NormalizeRequest(userData);
            m_PlayerView = ResolvePlayerView(request);
            m_DestroyInstantiatedPlayerOnLeave = request.DestroyInstantiatedPlayerOnLeave;

            if (request.Program != null)
            {
                m_PlayerView.Play(request.Program, request.ChapterId);
            }
            else
            {
                m_PlayerView.PlayRegistered(request.StoryId, request.ChapterId);
            }

            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public override UniTask OnLeaveAsync(ProcedureBase next, object userData)
        {
            StopAndReleasePlayer();
            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public override void Release()
        {
            StopAndReleasePlayer();
        }

        private StoryPlayerView ResolvePlayerView(StoryProcedureRequest request)
        {
            if (IsSceneInstance(request.PlayerView))
            {
                return request.PlayerView;
            }

            var prefab = request.PlayerPrefab != null ? request.PlayerPrefab : request.PlayerView;
            if (prefab != null)
            {
                m_InstantiatedPlayer = request.Parent == null
                    ? Object.Instantiate(prefab)
                    : Object.Instantiate(prefab, request.Parent);
                return m_InstantiatedPlayer;
            }

            var existing = Object.FindObjectOfType<StoryPlayerView>(true);
            if (IsSceneInstance(existing))
            {
                return existing;
            }

            throw new GameException("StoryProcedure requires a StoryPlayerView in scene, or StoryProcedureRequest.PlayerPrefab.");
        }

        private static bool IsSceneInstance(Component component)
        {
            return component != null && component.gameObject.scene.IsValid();
        }

        private void StopAndReleasePlayer()
        {
            if (m_PlayerView != null)
            {
                m_PlayerView.StopPlayback();
            }

            if (m_InstantiatedPlayer != null && m_DestroyInstantiatedPlayerOnLeave)
            {
                Object.Destroy(m_InstantiatedPlayer.gameObject);
            }

            m_PlayerView = null;
            m_InstantiatedPlayer = null;
            m_DestroyInstantiatedPlayerOnLeave = true;
        }

        private static StoryProcedureRequest NormalizeRequest(object userData)
        {
            if (userData is StoryProcedureRequest request)
            {
                ValidateRequest(request);
                return request;
            }

            if (userData is string storyId)
            {
                return StoryProcedureRequest.Registered(storyId);
            }

            if (userData is StoryProgram program)
            {
                return StoryProcedureRequest.Direct(program);
            }

            throw new ArgumentException("StoryProcedure userData must be StoryProcedureRequest, story id string, or StoryProgram.", nameof(userData));
        }

        private static void ValidateRequest(StoryProcedureRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Program == null && string.IsNullOrWhiteSpace(request.StoryId))
            {
                throw new ArgumentException("StoryProcedureRequest requires Program or StoryId.", nameof(request));
            }
        }
    }
}
