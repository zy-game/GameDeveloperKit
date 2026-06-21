using System.Collections.Generic;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情只读函数求值器。
    /// </summary>
    public interface IStoryFunctionResolver
    {
        /// <summary>
        /// 求值指定函数。
        /// </summary>
        /// <param name="functionName">函数名。</param>
        /// <param name="arguments">函数参数。</param>
        /// <param name="context">运行上下文。</param>
        /// <returns>函数结果。</returns>
        StoryValue Evaluate(string functionName, IReadOnlyList<StoryValue> arguments, StoryRuntimeContext context);
    }
}
