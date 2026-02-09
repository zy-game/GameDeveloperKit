using System;

namespace GameDeveloperKit.Procedure
{
    /// <summary>
    /// 流程变化事件参数
    /// </summary>
    public class ProcedureChangedEventArgs : Events.GameEventArgs
    {
        public override int Id => 0;

        /// <summary>
        /// 上一个流程类型
        /// </summary>
        public Type PreviousProcedure { get; set; }

        /// <summary>
        /// 当前流程类型
        /// </summary>
        public Type CurrentProcedure { get; set; }

        public override void OnClearup()
        {
            PreviousProcedure = null;
            CurrentProcedure = null;
        }
    }
}
