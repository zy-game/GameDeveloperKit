using System;

namespace GameDeveloperKit.Procedure
{
    /// <summary>
    /// 流程执行结果
    /// </summary>
    public readonly struct ProcedureResult
    {
        /// <summary>
        /// 下一个流程类型，null表示流程链结束
        /// </summary>
        public readonly Type NextType;

        /// <summary>
        /// 传递给下一个流程的参数
        /// </summary>
        public readonly object[] Args;

        public ProcedureResult(Type nextType, object[] args = null)
        {
            NextType = nextType;
            Args = args;
        }

        /// <summary>
        /// 流程链结束
        /// </summary>
        public static ProcedureResult End => new ProcedureResult(null, null);

        /// <summary>
        /// 跳转到下一个流程
        /// </summary>
        public static ProcedureResult Next<T>(params object[] args) where T : StateBase
            => new ProcedureResult(typeof(T), args);

        /// <summary>
        /// 跳转到下一个流程（通过类型）
        /// </summary>
        public static ProcedureResult Next(Type procedureType, params object[] args)
            => new ProcedureResult(procedureType, args);
    }
}
