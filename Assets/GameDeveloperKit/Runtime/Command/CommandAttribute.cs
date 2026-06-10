using System;

namespace GameDeveloperKit.Command
{
    /// <summary>
    /// 标记可通过命令名创建的命令类型。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CommandAttribute : Attribute
    {
        /// <summary>
        /// 初始化命令类型标记。
        /// </summary>
        /// <param name="name">命令注册名。</param>
        /// <exception cref="ArgumentException">命令注册名为空时抛出。</exception>
        public CommandAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Command name cannot be empty.", nameof(name));
            }

            Name = name;
        }

        /// <summary>
        /// 命令注册名。
        /// </summary>
        public string Name { get; }
    }
}
