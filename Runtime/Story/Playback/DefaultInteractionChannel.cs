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
    /// PlayerView 的默认交互通道。
    /// </summary>
    public sealed class DefaultInteractionChannel : IInteractionChannel
    {
        private readonly PlayerView m_View;
        private readonly List<Button> m_ChoiceButtons = new List<Button>();

        private bool m_Disposed;

        /// <summary>
        /// 初始化默认交互通道。
        /// </summary>
        /// <param name="view">默认播放视图。</param>
        public DefaultInteractionChannel(PlayerView view)
        {
            m_View = view ?? throw new ArgumentNullException(nameof(view));
        }

        /// <inheritdoc />
        public UniTask OnAwake(InteractionContext context, System.Threading.CancellationToken cancellationToken)
        {
            EnsureNotDisposed();
            var template = m_View.DefaultChoiceButtonTemplate;
            if (template != null)
            {
                template.gameObject.SetActive(false);
            }

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
            var template = m_View.DefaultChoiceButtonTemplate;
            if (frame.Choices == null ||
                frame.Choices.Count == 0 ||
                template == null)
            {
                return;
            }

            var parent = m_View.DefaultChoiceRoot != null ? m_View.DefaultChoiceRoot : template.transform.parent;
            for (var i = 0; i < frame.Choices.Count; i++)
            {
                var choice = frame.Choices[i];
                if (choice == null)
                {
                    continue;
                }

                var button = UnityEngine.Object.Instantiate(template, parent);
                var choiceId = choice.ChoiceId;
                button.gameObject.SetActive(true);
                SetButtonText(button, m_View.ResolveText(choice.Text));
                button.onClick.RemoveAllListeners();
                m_ChoiceButtons.Add(button);
            }
        }

        private void ClearFrameUi()
        {
            ClearChoices();
        }

        private void ClearChoices()
        {
            for (var i = 0; i < m_ChoiceButtons.Count; i++)
            {
                var button = m_ChoiceButtons[i];
                if (button == null)
                {
                    continue;
                }

                button.onClick.RemoveAllListeners();
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(button.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(button.gameObject);
                }
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
