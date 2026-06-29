using System;

namespace GameDeveloperKit.Procedure
{
    public sealed partial class ProcedureModule
    {
        private readonly struct ProcedureChangeRequest
        {
            /// <summary>
            /// 初始化 Procedure Change Request。
            /// </summary>
            /// <param name="procedureType">目标流程类型。</param>
            /// <param name="userData">切换参数。</param>
            public ProcedureChangeRequest(Type procedureType, object userData)
            {
                ProcedureType = procedureType;
                UserData = userData;
            }

            /// <summary>
            /// 目标流程类型。
            /// </summary>
            public Type ProcedureType { get; }

            /// <summary>
            /// 切换参数。
            /// </summary>
            public object UserData { get; }

            /// <summary>
            /// 是否有效。
            /// </summary>
            public bool IsValid => ProcedureType != null;
        }
    }
}
