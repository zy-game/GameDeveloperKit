using System;

namespace GameDeveloperKit.Config
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class TableOptionAttribute : Attribute
    {
        /// <summary>
        /// 初始化 Table Option Attribute。
        /// </summary>
        public TableOptionAttribute(string path)
        {
            Path = path;
        }

        public string Path { get; }
    }
}
