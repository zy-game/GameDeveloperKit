using System;

namespace GameDeveloperKit.Log
{
    /// <summary>
    /// 命令接口
    /// </summary>
    public interface ICommand
    {
        string Name { get; }
        string Description { get; }
        string Usage { get; }
        void Execute(string[] args, Action<string> output);
    }
}
