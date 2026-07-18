using GameDeveloperKit.Resource;
using UnityEngine;

namespace GameDeveloperKit.Playable
{
    internal sealed class AudioRuntimeSource
    {
        public AudioSource AudioSource { get; set; }

        public AudioPlayableHandle Handle { get; set; }

        public AssetHandle AssetHandle { get; set; }

        public AudioTrack Track { get; set; }

        public bool Primary { get; set; }

        public bool InUse { get; set; }

        public float Volume { get; set; } = 1f;

        public float FadeGain { get; set; } = 1f;

        public float FadeIn { get; set; }

        public float FadeOut { get; set; }

        public bool NaturalFadeOutStarted { get; set; }

        public int Priority { get; set; }

        public long Sequence { get; set; }

        public int Version { get; set; }

        public int FadeVersion { get; set; }
    }
}
