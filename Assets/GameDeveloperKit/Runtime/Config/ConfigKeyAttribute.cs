using System;

namespace GameDeveloperKit.Config
{
    /// <summary>
    /// 定义 Table Option Attribute 类型。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class TableOptionAttribute : Attribute
    {
        /// <summary>
        /// 初始化 Table Option Attribute。
        /// </summary>
        /// <param name="path">path 参数。</param>
        public TableOptionAttribute(string path)
        {
            Path = path;
        }

        public string Path { get; }
    }
}
