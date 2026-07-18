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

        /// <summary>
        /// 视频来源参数。
        /// </summary>
        public const string VideoSourceArgument = "source";

        /// <summary>
        /// StreamingAssets 视频来源。
        /// </summary>
        public const string VideoSourceStreamingAssets = MediaSourceStreamingAssets;

        public const string VideoSourceCdn = MediaSourceCdn;

        /// <summary>
        /// 持久化目录视频来源。
        /// </summary>
        public const string VideoSourcePersistentDataPath = "persistent_data_path";

        /// <summary>
        /// 网络流视频来源。
        /// </summary>
        public const string VideoSourceNetworkStream = "network_stream";

        public const string VideoSeekableArgument = "seekable";

        /// <summary>
        /// 图片参数。
        /// </summary>
        public const string ImageArgument = "image";

        /// <summary>
        /// 默认完成结果。
        /// </summary>
        public const string CompletedOutcome = "completed";
    }

    /// <summary>
    /// Story 内置交互命令名。
    /// </summary>
    public static class InteractionCommandNames
    {
        /// <summary>
        /// 限时快速输入互动。
        /// </summary>
        public const string Qte = "qte";

        /// <summary>
        /// 解锁互动。
        /// </summary>
        public const string Unlock = "unlock";

        /// <summary>
        /// 成功结果。
        /// </summary>
        public const string SuccessOutcome = "success";

        /// <summary>
        /// 失败结果。
        /// </summary>
        public const string FailOutcome = "fail";

        /// <summary>
        /// 输入动作 ID 参数。
        /// </summary>
        public const string InputActionIdArgument = "inputActionId";

        /// <summary>
        /// 持续时间参数。
        /// </summary>
        public const string DurationSecondsArgument = "durationSeconds";

        /// <summary>
        /// 需要输入次数参数。
        /// </summary>
        public const string RequiredCountArgument = "requiredCount";

        /// <summary>
        /// 解锁 ID 参数。
        /// </summary>
        public const string UnlockIdArgument = "unlockId";

        /// <summary>
        /// 解锁玩法类型参数。
        /// </summary>
        public const string PuzzleTypeArgument = "puzzleType";

        /// <summary>
        /// 连线解锁玩法。
        /// </summary>
        public const string PuzzleTypeLineConnect = "line_connect";

        /// <summary>
        /// 节点解锁玩法。
        /// </summary>
        public const string PuzzleTypeNodeUnlock = "node_unlock";

        /// <summary>
        /// 自定义解锁玩法。
        /// </summary>
        public const string PuzzleTypeCustom = "custom";

        /// <summary>
        /// 提示文本 key 参数。
        /// </summary>
        public const string PromptTextKeyArgument = "promptTextKey";
    }

    public static class SettlementCommandNames
    {
        public const string SettleChapter = "settle_chapter";
        public const string SettlementIdArgument = "settlementId";
        public const string PlanVersionArgument = "planVersion";
        public const string PlanArgument = "plan";
        public const string CompletedOutcome = "completed";
        public const string FailedOutcome = "failed";
    }
}
