using GameDeveloperKit.Resource;
using UnityEngine;

namespace GameDeveloperKit.Sound
{
    internal sealed class SoundRuntimeSource
    {
        public AudioSource AudioSource { get; set; }

        public SoundHandle Handle { get; set; }

        public AssetHandle AssetHandle { get; set; }

        public AudioClip AudioClip { get; set; }

        public SoundTrack Track { get; set; }

        public bool Pooled { get; set; }

        public bool InUse { get; set; }

        public float Volume { get; set; } = 1f;

        public int Priority { get; set; }

        public long Sequence { get; set; }

        public int Version { get; set; }
    }
}
