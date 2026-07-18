using GameDeveloperKit.Story.Model;

namespace GameDeveloperKit.Story.Execution
{
    /// <summary>
    /// 剧情变量存储。
    /// </summary>
    public interface IVariableStore
    {
        /// <summary>
        /// 尝试读取变量。
        /// </summary>
        /// <param name="name">变量名。</param>
        /// <param name="value">变量值。</param>
        /// <returns>找到时返回 true。</returns>
        bool TryGet(string name, out Value value);

        /// <summary>
        /// 写入变量。
        /// </summary>
        /// <param name="name">变量名。</param>
        /// <param name="value">变量值。</param>
        void Set(string name, Value value);
    }
}
