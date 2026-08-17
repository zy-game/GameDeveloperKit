namespace GameDeveloperKit.Media
{
    /// <summary>
    /// Selects conservative media renditions for memory-constrained player platforms.
    /// </summary>
    public static class MediaPlaybackPlatformPolicy
    {
        public const int DesktopBackgroundVideoHeight = 2160;
        public const int ConstrainedBackgroundVideoHeight = 1080;

        public static int BackgroundVideoHeight
        {
            get
            {
#if UNITY_WEBGL || UNITY_ANDROID
                return ConstrainedBackgroundVideoHeight;
#else
                return DesktopBackgroundVideoHeight;
#endif
            }
        }

        internal static int SelectBackgroundVideoHeight(bool constrainedPlatform)
        {
            return constrainedPlatform
                ? ConstrainedBackgroundVideoHeight
                : DesktopBackgroundVideoHeight;
        }
    }
}
