using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameDeveloperKit.Story.Execution;

namespace GameDeveloperKit.Story.Playback
{
    /// <summary>
    /// PlaybackView 的默认交互通道。
    /// </summary>
    public sealed class DefaultInteractionChannel : IInteractionChannel
    {
        private readonly PlaybackView m_View;
        private readonly List<Button> m_ChoiceButtons = new List<Button>();

        private bool m_Disposed;

        /// <summary>
        /// 初始化默认交互通道。
        /// </summary>
        /// <param name="view">默认播放视图。</param>
        public DefaultInteractionChannel(PlaybackView view)
        {
            m_View = view ?? throw new ArgumentNullException(nameof(view));
        }

        /// <inheritdoc />
        public UniTask OnAwake(InteractionContext context, System.Threading.CancellationToken cancellationToken)
        {
            EnsureNotDisposed();
            ClearChoices();

            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public void OnStoryStarted(InteractionContext context)
        {
            EnsureNotDisposed();
        }

        /// <inheritdoc />
        public void OnEpisodeChanged(EpisodeInteractionContext context)
        {
            EnsureNotDisposed();
            ClearChoices();
        }

        /// <inheritdoc />
        public void OnFrameChanged(Frame frame)
        {
            EnsureNotDisposed();
            ClearChoices();

            if (frame != null)
            {
                RenderChoices(frame);
            }
        }

        /// <inheritdoc />
        public PlaybackSurfaceView GetPlaybackSurfaceView(InteractionRequest request)
        {
            EnsureNotDisposed();
            return m_View.CreateDefaultSurfaceView(m_ChoiceButtons);
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
        }

        /// <inheritdoc />
        public void OnStoryStopped()
        {
            if (m_Disposed)
            {
                return;
            }

            ClearFrameUi();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            ClearChoices();
        }

        internal void ClearTransientInputs()
        {
            ClearChoices();
        }

        private void RenderChoices(Frame frame)
        {
            if (frame.Choices == null || frame.Choices.Count == 0)
            {
                return;
            }

            var slots = m_View.DefaultChoiceButtons;
            if (frame.Choices.Count > slots.Count)
            {
                throw new GameException(
                    $"Story choice count exceeds the default playback prefab slots. choices:{frame.Choices.Count} slots:{slots.Count}");
            }

            for (var i = 0; i < frame.Choices.Count; i++)
            {
                var choice = frame.Choices[i];
                var button = slots[i];
                if (button == null)
                {
                    throw new GameException($"Story choice button slot is missing. index:{i}");
                }

                button.gameObject.SetActive(true);
                button.onClick.RemoveAllListeners();
                if (choice != null)
                {
                    SetButtonText(button, m_View.ResolveText(choice.Text));
                }

                m_ChoiceButtons.Add(button);
            }
        }

        private void ClearFrameUi()
        {
            ClearChoices();
        }

        private void ClearChoices()
        {
            var slots = m_View.DefaultChoiceButtons;
            for (var i = 0; i < slots.Count; i++)
            {
                var button = slots[i];
                if (button == null)
                {
                    continue;
                }

                button.onClick.RemoveAllListeners();
                button.gameObject.SetActive(false);
            }

            m_ChoiceButtons.Clear();
        }

        private static void SetButtonText(Button button, string value)
        {
            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = value ?? string.Empty;
                return;
            }

            var legacyText = button.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (legacyText != null)
            {
                legacyText.text = value ?? string.Empty;
            }
        }

        private void EnsureNotDisposed()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(nameof(DefaultInteractionChannel));
            }
        }
    }
}
