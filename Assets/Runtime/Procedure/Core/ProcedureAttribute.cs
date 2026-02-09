using System;

namespace GameDeveloperKit.Procedure
{
    /// <summary>
    /// 流程特性，用于标记流程类
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ProcedureAttribute : Attribute
    {
        /// <summary>
        /// 分组名称，用于创建独立的流程链
        /// </summary>
        public string Group { get; set; } = "Default";
    }
}
