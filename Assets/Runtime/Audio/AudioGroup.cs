using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Audio
{
    /// <summary>
    /// 音效组（按组管理音效）
    /// </summary>
    public class AudioGroup
    {
        private readonly string _groupName;
        private readonly List<AudioTrack> _tracks = new List<AudioTrack>();
        private float _groupVolume = 1f;
        private bool _isMuted;

        public string GroupName => _groupName;
        public int TrackCount => _tracks.Count;
        public float Volume => _groupVolume;
        public bool IsMuted => _isMuted;

        public AudioGroup(string groupName)
        {
            _groupName = groupName;
        }

        /// <summary>
        /// 添加音轨
        /// </summary>
        internal void AddTrack(AudioTrack track)
        {
            if (track == null || _tracks.Contains(track)) return;

            _tracks.Add(track);

            // 应用组音量
            track.Volume *= _groupVolume;
        }

        /// <summary>
        /// 移除音轨
        /// </summary>
        internal void RemoveTrack(AudioTrack track)
        {
            _tracks.Remove(track);
        }

        /// <summary>
        /// 设置组音量
        /// </summary>
        public void SetVolume(float volume)
        {
            _groupVolume = Mathf.Clamp01(volume);

            foreach (var track in _tracks)
            {
                if (track != null)
                {
                    track.Volume = track.Volume / Volume * _groupVolume;
                }
            }
        }

        /// <summary>
        /// 静音/取消静音
        /// </summary>
        public void SetMute(bool mute)
        {
            _isMuted = mute;

            foreach (var track in _tracks)
            {
                if (track != null)
                {
                    track.Volume = mute ? 0f : _groupVolume;
                }
            }
        }

        /// <summary>
        /// 停止所有音轨
        /// </summary>
        public void StopAll()
        {
            for (int i = _tracks.Count - 1; i >= 0; i--)
            {
                var track = _tracks[i];
                if (track != null)
                {
                    track.Stop();
                    track.Dispose();
                }
            }

            _tracks.Clear();
        }

        /// <summary>
        /// 暂停所有音轨
        /// </summary>
        public void PauseAll()
        {
            foreach (var track in _tracks)
            {
                track?.Pause();
            }
        }

        /// <summary>
        /// 恢复所有音轨
        /// </summary>
        public void ResumeAll()
        {
            foreach (var track in _tracks)
            {
                track?.Resume();
            }
        }

        /// <summary>
        /// 清理已完成的音轨
        /// </summary>
        internal void Update()
        {
            for (int i = _tracks.Count - 1; i >= 0; i--)
            {
                var track = _tracks[i];
                if (track == null || (!track.IsPlaying && !track.IsPaused))
                {
                    _tracks.RemoveAt(i);
                }
            }
        }
    }
}
