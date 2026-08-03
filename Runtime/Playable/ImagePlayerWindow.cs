using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.UI;
using UnityEngine;

namespace GameDeveloperKit.Playable
{
    /// <summary>
    /// 通用图片单播/轮播窗口。业务层通过继承并重载 virtual hook 接入自己的 UI。
    /// </summary>
    public abstract class ImagePlayerWindow : UIWindow
    {
        private readonly List<string> m_Locations = new List<string>();
        private ImagePlayableHandle m_Playback;
        private int m_CurrentIndex = -1;

        public IReadOnlyList<string> Locations => m_Locations;

        public int CurrentIndex => m_CurrentIndex;

        public string CurrentLocation => m_CurrentIndex >= 0 && m_CurrentIndex < m_Locations.Count
            ? m_Locations[m_CurrentIndex]
            : null;

        public Texture CurrentTexture => m_Playback?.Texture;

        public event Action<int, string, Texture> ImageChanged;

        public event Action<int, string, Texture> ImageClicked;

        public virtual void SetImages(IReadOnlyList<string> locations)
        {
            m_Locations.Clear();
            if (locations != null)
            {
                for (var i = 0; i < locations.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(locations[i]))
                    {
                        throw new ArgumentException("Image location cannot be empty.", nameof(locations));
                    }

                    m_Locations.Add(locations[i]);
                }
            }

            m_CurrentIndex = m_Locations.Count == 0 ? -1 : 0;
        }

        public virtual UniTask<ImagePlayableHandle> PlayAsync(
            string location,
            CancellationToken cancellationToken = default)
        {
            SetImages(new[] { location });
            return PlayCurrentAsync(cancellationToken);
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

        public virtual void ClickCurrentImage()
        {
            if (m_CurrentIndex < 0 || m_Playback == null)
            {
                return;
            }

            OnImageClicked(m_CurrentIndex, CurrentLocation, m_Playback.Texture);
            ImageClicked?.Invoke(m_CurrentIndex, CurrentLocation, m_Playback.Texture);
        }

        public override void Release()
        {
            StopPlayback();
            m_Locations.Clear();
            m_CurrentIndex = -1;
            base.Release();
        }

        protected virtual void OnImageChanged(int index, string location, Texture texture)
        {
            ImageChanged?.Invoke(index, location, texture);
        }

        protected virtual void OnImageClicked(int index, string location, Texture texture)
        {
        }

        protected virtual void OnImageStopped()
        {
        }

        private async UniTask<ImagePlayableHandle> PlayIndexAsync(
            int index,
            CancellationToken cancellationToken)
        {
            if (index < 0 || index >= m_Locations.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            StopPlayback();
            m_CurrentIndex = index;
            var location = m_Locations[index];
            m_Playback = await App.Playable.Image.PlayAsync(
                new ImagePlayableRequest(location, _ => { }),
                cancellationToken);
            OnImageChanged(index, location, m_Playback.Texture);
            return m_Playback;
        }

        private void StopPlayback()
        {
            var playback = m_Playback;
            m_Playback = null;
            if (playback == null)
            {
                return;
            }

            playback.Stop();
            playback.Dispose();
            OnImageStopped();
        }
    }
}
