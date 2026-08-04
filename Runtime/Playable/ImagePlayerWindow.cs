using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.Playable
{
    /// <summary>
    /// 可直接打开、也可被业务继承的图片单播/轮播窗口。
    /// </summary>
    [UIOption("Assets/Bundles/Playback/ImagePlayerWindow.prefab", 500, CacheEnabled = false)]
    public class ImagePlayerWindow : UIWindow
    {
        private readonly List<string> m_Locations = new List<string>();
        private readonly SemaphoreSlim m_PlayGate = new SemaphoreSlim(1, 1);
        private ImagePlayableHandle m_Playback;
        private RawImage m_ImageOutput;
        private Button m_ImageClickButton;
        private Button m_BackButton;
        private Button m_PreviousButton;
        private Button m_NextButton;
        private TMP_Text m_CounterText;
        private bool m_CarouselLoop;
        private bool m_CarouselPlaying;
        private bool m_CarouselAdvancePending;
        private float m_CarouselIntervalSeconds;
        private float m_CarouselElapsedSeconds;
        private int m_CurrentIndex = -1;

        public IReadOnlyList<string> Locations => m_Locations;

        public int CurrentIndex => m_CurrentIndex;

        public string CurrentLocation => m_CurrentIndex >= 0 && m_CurrentIndex < m_Locations.Count
            ? m_Locations[m_CurrentIndex]
            : null;

        public Texture CurrentTexture => m_Playback?.Texture;

        public event Action BackRequested;

        public event Action<int, string, Texture> ImageChanged;

        public event Action<int, string, Texture> ImageClicked;

        public override UniTask OnAwakeAsync()
        {
            BindDocument();
            BindControls();
            RefreshNavigation();
            return UniTask.CompletedTask;
        }

        public override void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
            if (!m_CarouselPlaying || m_Locations.Count <= 1)
            {
                return;
            }

            m_CarouselElapsedSeconds += Mathf.Max(0f, unscaledDeltaTime);
            if (m_CarouselElapsedSeconds < m_CarouselIntervalSeconds)
            {
                return;
            }

            m_CarouselElapsedSeconds = 0f;
            AdvanceCarousel();
        }

        public virtual void SetImages(IReadOnlyList<string> locations)
        {
            StopCarousel();
            StopPlayback();
            m_Locations.Clear();
            if (locations != null)
            {
                for (var i = 0; i < locations.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(locations[i]))
                    {
                        throw new ArgumentException("Image location cannot be empty.", nameof(locations));
                    }

                    m_Locations.Add(locations[i].Trim());
                }
            }

            m_CurrentIndex = m_Locations.Count == 0 ? -1 : 0;
            RefreshNavigation();
        }

        public virtual UniTask<ImagePlayableHandle> PlayAsync(
            string location,
            CancellationToken cancellationToken = default)
        {
            SetImages(new[] { location });
            return PlayCurrentAsync(cancellationToken);
        }

        public virtual async UniTask<ImagePlayableHandle> PlayAsync(
            IReadOnlyList<string> locations,
            float intervalSeconds,
            bool loop = true,
            CancellationToken cancellationToken = default)
        {
            SetImages(locations);
            var playback = await PlayCurrentAsync(cancellationToken);
            if (intervalSeconds > 0f && m_Locations.Count > 1)
            {
                StartCarousel(intervalSeconds, loop);
            }

            return playback;
        }

        public virtual UniTask<ImagePlayableHandle> PlayCurrentAsync(
            CancellationToken cancellationToken = default)
        {
            return PlayIndexAsync(m_CurrentIndex, cancellationToken);
        }

        public virtual UniTask<ImagePlayableHandle> NextAsync(
            CancellationToken cancellationToken = default)
        {
            if (m_Locations.Count == 0)
            {
                return UniTask.FromException<ImagePlayableHandle>(
                    new InvalidOperationException("Image playlist is empty."));
            }

            var next = (m_CurrentIndex + 1) % m_Locations.Count;
            return PlayIndexAsync(next, cancellationToken);
        }

        public virtual UniTask<ImagePlayableHandle> PreviousAsync(
            CancellationToken cancellationToken = default)
        {
            if (m_Locations.Count == 0)
            {
                return UniTask.FromException<ImagePlayableHandle>(
                    new InvalidOperationException("Image playlist is empty."));
            }

            var previous = (m_CurrentIndex - 1 + m_Locations.Count) % m_Locations.Count;
            return PlayIndexAsync(previous, cancellationToken);
        }

        public virtual void StartCarousel(float intervalSeconds, bool loop = true)
        {
            if (intervalSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
            }

            StopCarousel();
            if (m_Locations.Count <= 1)
            {
                return;
            }

            m_CarouselLoop = loop;
            m_CarouselIntervalSeconds = intervalSeconds;
            m_CarouselElapsedSeconds = 0f;
            m_CarouselAdvancePending = false;
            m_CarouselPlaying = true;
        }

        public virtual void StopCarousel()
        {
            m_CarouselPlaying = false;
            m_CarouselIntervalSeconds = 0f;
            m_CarouselElapsedSeconds = 0f;
            m_CarouselAdvancePending = false;
        }

        public virtual void ClickCurrentImage()
        {
            if (m_CurrentIndex < 0 || m_Playback == null || m_Playback.Status != PlayableStatus.Playing)
            {
                return;
            }

            var index = m_CurrentIndex;
            var location = CurrentLocation;
            var texture = m_Playback.Texture;
            OnImageClicked(index, location, texture);
            ImageClicked?.Invoke(index, location, texture);
        }

        public virtual void RequestBack()
        {
            OnBackRequested();
            BackRequested?.Invoke();
        }

        public override void Release()
        {
            UnbindControls();
            StopCarousel();
            StopPlayback();
            m_Locations.Clear();
            m_CurrentIndex = -1;
            m_ImageOutput = null;
            m_ImageClickButton = null;
            m_BackButton = null;
            m_PreviousButton = null;
            m_NextButton = null;
            m_CounterText = null;
            base.Release();
        }

        protected virtual void OnImageChanged(int index, string location, Texture texture)
        {
        }

        protected virtual void OnImageClicked(int index, string location, Texture texture)
        {
        }

        protected virtual void OnImageStopped()
        {
        }

        protected virtual void OnBackRequested()
        {
            App.UI.Back().Forget(Debug.LogException);
        }

        private void BindDocument()
        {
            if (Document == null)
            {
                throw new GameException("Image player prefab is missing UIDocument.");
            }

            m_ImageOutput = Document.GetComponent<RawImage>("ImageOutput");
            m_ImageClickButton = Document.GetComponent<Button>("ImageClickButton");
            m_BackButton = Document.GetComponent<Button>("BackButton");
            m_PreviousButton = Document.GetComponent<Button>("PreviousButton");
            m_NextButton = Document.GetComponent<Button>("NextButton");
            m_CounterText = Document.GetComponent<TMP_Text>("CounterText");
        }

        private void BindControls()
        {
            m_ImageClickButton.onClick.AddListener(ClickCurrentImage);
            m_BackButton.onClick.AddListener(RequestBack);
            m_PreviousButton.onClick.AddListener(PreviousFromUi);
            m_NextButton.onClick.AddListener(NextFromUi);
        }

        private void UnbindControls()
        {
            m_ImageClickButton?.onClick.RemoveListener(ClickCurrentImage);
            m_BackButton?.onClick.RemoveListener(RequestBack);
            m_PreviousButton?.onClick.RemoveListener(PreviousFromUi);
            m_NextButton?.onClick.RemoveListener(NextFromUi);
        }

        private void PreviousFromUi()
        {
            PreviousAsync().Forget(Debug.LogException);
        }

        private void NextFromUi()
        {
            NextAsync().Forget(Debug.LogException);
        }

        private void AdvanceCarousel()
        {
            if (m_CarouselAdvancePending)
            {
                return;
            }

            if (m_CurrentIndex >= m_Locations.Count - 1 && !m_CarouselLoop)
            {
                StopCarousel();
                return;
            }

            m_CarouselAdvancePending = true;
            AdvanceCarouselAsync().Forget(Debug.LogException);
        }

        private async UniTask AdvanceCarouselAsync()
        {
            try
            {
                await NextAsync();
            }
            finally
            {
                m_CarouselAdvancePending = false;
            }
        }

        private async UniTask<ImagePlayableHandle> PlayIndexAsync(
            int index,
            CancellationToken cancellationToken)
        {
            if (index < 0 || index >= m_Locations.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            await m_PlayGate.WaitAsync(cancellationToken);
            try
            {
                StopPlayback();
                m_CurrentIndex = index;
                var location = m_Locations[index];
                m_Playback = await App.Playable.Image.PlayAsync(
                    new ImagePlayableRequest(location, SetImageTexture),
                    cancellationToken);
                RefreshNavigation();
                var texture = m_Playback.Texture;
                OnImageChanged(index, location, texture);
                ImageChanged?.Invoke(index, location, texture);
                return m_Playback;
            }
            finally
            {
                m_PlayGate.Release();
            }
        }

        private void StopPlayback()
        {
            var playback = m_Playback;
            m_Playback = null;
            if (playback == null)
            {
                SetImageTexture(null);
                return;
            }

            playback.Stop();
            playback.Dispose();
            SetImageTexture(null);
            OnImageStopped();
        }

        private void SetImageTexture(Texture texture)
        {
            if (m_ImageOutput == null)
            {
                return;
            }

            m_ImageOutput.texture = texture;
            m_ImageOutput.color = texture == null ? Color.clear : Color.white;
            if (texture != null)
            {
                var fitter = m_ImageOutput.GetComponent<AspectRatioFitter>();
                if (fitter != null && texture.height > 0)
                {
                    fitter.aspectRatio = (float)texture.width / texture.height;
                }
            }
        }

        private void RefreshNavigation()
        {
            var hasImages = m_Locations.Count > 0;
            var canNavigate = m_Locations.Count > 1;
            if (m_PreviousButton != null)
            {
                m_PreviousButton.gameObject.SetActive(canNavigate);
                m_PreviousButton.interactable = canNavigate;
            }

            if (m_NextButton != null)
            {
                m_NextButton.gameObject.SetActive(canNavigate);
                m_NextButton.interactable = canNavigate;
            }

            if (m_CounterText != null)
            {
                m_CounterText.text = hasImages ? $"{m_CurrentIndex + 1} / {m_Locations.Count}" : "0 / 0";
                m_CounterText.gameObject.SetActive(canNavigate);
            }
        }
    }
}
