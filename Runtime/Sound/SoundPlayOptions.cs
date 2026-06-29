namespace GameDeveloperKit.Sound
{
    public sealed class SoundPlayOptions
    {
        public SoundTrack Track { get; set; } = SoundTrack.Master;

        public bool Loop { get; set; }

        public float Volume { get; set; } = 1f;

        public float FadeIn { get; set; }

        public float FadeOut { get; set; }

        public int Priority { get; set; } = 128;
    }
}
