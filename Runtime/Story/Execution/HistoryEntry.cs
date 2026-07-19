namespace GameDeveloperKit.Story.Execution
{
    /// <summary>
    /// 记录一次剧情推进来源的历史项。
    /// </summary>
    public readonly struct HistoryEntry
    {
        /// <summary>
        /// 初始化剧情历史项。
        /// </summary>
        /// <param name="episodeId">推进发生时所在剧情段 ID。</param>
        /// <param name="nodeId">推进发生时所在节点 ID。</param>
        /// <param name="portId">被触发的输出端口。</param>
        /// <param name="interactionId">触发推进的交互 ID。</param>
        /// <param name="actionId">触发推进的动作 ID。</param>
        /// <param name="outcomeId">外部动作返回的结果 ID。</param>
        /// <param name="time">推进发生时的节点时间。</param>
        public HistoryEntry(
            string episodeId,
            string nodeId,
            string portId,
            string interactionId,
            string actionId,
            string outcomeId,
            float time)
        {
            EpisodeId = episodeId;
            NodeId = nodeId;
            PortId = portId;
            InteractionId = interactionId;
            ActionId = actionId;
            OutcomeId = outcomeId;
            Time = time;
        }

        /// <summary>
        /// 推进发生时所在剧情段 ID。
        /// </summary>
        public string EpisodeId { get; }

        /// <summary>
        /// 推进发生时所在节点 ID。
        /// </summary>
        public string NodeId { get; }

        /// <summary>
        /// 被触发的输出端口。
        /// </summary>
        public string PortId { get; }

        /// <summary>
        /// 触发推进的交互 ID。
        /// </summary>
        public string InteractionId { get; }

        /// <summary>
        /// 触发推进的动作 ID。
        /// </summary>
        public string ActionId { get; }

        /// <summary>
        /// 外部动作返回的结果 ID。
        /// </summary>
        public string OutcomeId { get; }

        /// <summary>
        /// 推进发生时的节点时间。
        /// </summary>
        public float Time { get; }
    }
}
