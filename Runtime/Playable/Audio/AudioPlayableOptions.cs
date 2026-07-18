using UnityEngine;

namespace GameDeveloperKit.Playable
{
    public sealed class AudioPlayableOptions
    {
        public AudioTrack Track { get; set; } = AudioTrack.Sfx;

        public bool Loop { get; set; }

        public float Volume { get; set; } = 1f;

        public float FadeIn { get; set; }

        public float FadeOut { get; set; }

        public int Priority { get; set; } = 128;

        public Vector3? Position { get; set; }
    }
}
