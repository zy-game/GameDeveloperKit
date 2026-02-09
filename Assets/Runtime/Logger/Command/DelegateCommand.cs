using System;

namespace GameDeveloperKit.Log
{
    /// <summary>
    /// 委托命令实现
    /// </summary>
    public class DelegateCommand : ICommand
    {
        private readonly Action<string[], Action<string>> _handler;

        public string Name { get; }
        public string Description { get; }
        public string Usage { get; }

        public DelegateCommand(string name, string description, string usage, Action<string[], Action<string>> handler)
        {
            Name = name;
            Description = description;
            Usage = usage;
            _handler = handler;
        }

        public void Execute(string[] args, Action<string> output)
        {
            _handler(args, output);
        }
    }
}
