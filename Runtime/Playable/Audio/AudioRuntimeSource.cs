using System;
using GameDeveloperKit.Resource;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Playable
{
    internal sealed class AudioRuntimeSource
    {
        public AudioSource AudioSource { get; set; }

        public AudioPlayableHandle Handle { get; set; }

        public AudioClipLease ClipLease { get; set; }

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

    internal sealed class AudioClipLease : IDisposable
    {
        private AssetHandle m_AssetHandle;
        private AudioClip m_TemporaryClip;

        private AudioClipLease(AudioClip clip, AssetHandle assetHandle, AudioClip temporaryClip)
        {
            Clip = clip ?? throw new ArgumentNullException(nameof(clip));
            m_AssetHandle = assetHandle;
            m_TemporaryClip = temporaryClip;
        }

        public AudioClip Clip { get; private set; }

        public static AudioClipLease FromAsset(AssetHandle assetHandle, AudioClip clip)
        {
            return new AudioClipLease(clip, assetHandle ?? throw new ArgumentNullException(nameof(assetHandle)), null);
        }

        public static AudioClipLease FromTemporary(AudioClip clip)
        {
            return new AudioClipLease(clip, null, clip);
        }

        public void Dispose()
        {
            var assetHandle = m_AssetHandle;
            var temporaryClip = m_TemporaryClip;
            m_AssetHandle = null;
            m_TemporaryClip = null;
            Clip = null;
            assetHandle?.Release();
            if (temporaryClip != null)
            {
                Object.Destroy(temporaryClip);
            }
        }
    }
}
