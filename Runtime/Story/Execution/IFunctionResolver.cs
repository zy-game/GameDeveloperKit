using System.Collections.Generic;
using GameDeveloperKit.Story.Model;

namespace GameDeveloperKit.Story.Execution
{
    /// <summary>
    /// 剧情只读函数求值器。
    /// </summary>
    public interface IFunctionResolver
    {
        /// <summary>
        /// 求值指定函数。
        /// </summary>
        /// <param name="functionName">函数名。</param>
        /// <param name="arguments">函数参数。</param>
        /// <param name="context">运行上下文。</param>
        /// <returns>函数结果。</returns>
        Value Evaluate(string functionName, IReadOnlyList<Value> arguments, RuntimeContext context);
    }
}
