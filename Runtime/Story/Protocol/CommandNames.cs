namespace GameDeveloperKit.Story.Protocol
{
    /// <summary>
    /// Story 内置媒体命令名。
    /// </summary>
    public static class MediaCommandNames
    {
        /// <summary>
        /// 播放视频。
        /// </summary>
        public const string PlayVideo = "play_video";

        /// <summary>
        /// 显示图片。
        /// </summary>
        public const string ShowImage = "show_image";

        /// <summary>
        /// 播放音频。
        /// </summary>
        public const string PlayAudio = "play_audio";

        /// <summary>
        /// 媒体片段参数。
        /// </summary>
        public const string ClipArgument = "clip";

        public const string MediaSourceArgument = "mediaSource";

        public const string MediaIdArgument = "mediaId";

        public const string MediaSourceCdn = "cdn";

        public const string MediaSourceStreamingAssets = "streaming_assets";

        public const string MediaSourceResource = "resource";

        public const string VideoFormatArgument = "videoFormat";

        public const string VideoRenditionsArgument = "videoRenditions";

        public const string VideoSeekableArgument = "seekable";

        /// <summary>
        /// 图片参数。
        /// </summary>
        public const string ImageArgument = "image";

        /// <summary>
        /// 默认完成结果。
        /// </summary>
        public const string CompletedOutcome = "completed";

        // Reserved names retained only so unrelated legacy test fixtures do not
        // accidentally become part of the current video command schema.
        public const string VideoSourceArgument = "mediaSource";
        public const string VideoSourceCdn = "cdn";
        public const string VideoSourceStreamingAssets = "streaming_assets";
        public const string VideoSourcePersistentDataPath = "persistent_data_path";
        public const string VideoSourceNetworkStream = "network_stream";
    }

}
