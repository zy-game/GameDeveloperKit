namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 流程转换请求数据模型
    /// </summary>
    public sealed class ProcedureTransitionRequest
    {
        /// <summary>
        /// 目标状态名称
        /// </summary>
        public string StateName;

        /// <summary>
        /// 用户数据
        /// </summary>
        public object UserData;

        /// <summary>
        /// 转换来源
        /// </summary>
        public ProcedureTransitionSource Source = ProcedureTransitionSource.Runtime;

        /// <summary>
        /// 转换触发器标识
        /// </summary>
        public string Trigger;
    }
}
