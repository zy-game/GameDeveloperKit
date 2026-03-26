namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 流程转换保护器接口，用于控制流程状态转换的条件
    /// </summary>
    public interface IProcedureTransitionGuard
    {
        /// <summary>
        /// 判断是否可以执行流程状态转换
        /// </summary>
        /// <param name="currentState">当前状态</param>
        /// <param name="nextState">下一个状态</param>
        /// <param name="userData">用户数据</param>
        /// <param name="reason">转换失败的原因</param>
        /// <returns>如果可以转换返回true，否则返回false</returns>
        bool CanTransition(IProcedureState currentState, IProcedureState nextState, object userData, out string reason);
    }
}
