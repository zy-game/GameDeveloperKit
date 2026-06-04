using System;

namespace GameDeveloperKit.Command
{
    /// <summary>
    /// 标记可通过命令名创建的命令类型。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CommandAttribute : Attribute
    {
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
