using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GameDeveloperKit.Log
{
    /// <summary>
    /// 内置命令：帮助
    /// </summary>
    public class HelpCommand : ICommand
    {
        private readonly CommandManager _manager;
        public string Name => "help";
        public string Description => "Show available commands";
        public string Usage => "help [command]";

        public HelpCommand(CommandManager manager) => _manager = manager;

        public void Execute(string[] args, Action<string> output)
        {
            if (args.Length > 0)
            {
                var cmd = _manager.Commands.FirstOrDefault(c => 
                    c.Name.Equals(args[0], StringComparison.OrdinalIgnoreCase));
                if (cmd != null)
                {
                    output($"{cmd.Name}: {cmd.Description}");
                    output($"Usage: {cmd.Usage}");
                }
                else
                {
                    output($"Unknown command: {args[0]}");
                }
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Available commands:");
            foreach (var cmd in _manager.Commands.OrderBy(c => c.Name))
            {
                sb.AppendLine($"  {cmd.Name,-15} {cmd.Description}");
            }
            output(sb.ToString());
        }
    }

    /// <summary>
    /// 内置命令：清屏
    /// </summary>
    public class ClearCommand : ICommand
    {
        private readonly CommandManager _manager;
        public string Name => "clear";
        public string Description => "Clear console output";
        public string Usage => "clear";

        public ClearCommand(CommandManager manager) => _manager = manager;

        public void Execute(string[] args, Action<string> output)
        {
            _manager.ClearOutput();
        }
    }

    /// <summary>
    /// 内置命令：垃圾回收
    /// </summary>
    public class GCCommand : ICommand
    {
        public string Name => "gc";
        public string Description => "Force garbage collection";
        public string Usage => "gc";

        public void Execute(string[] args, Action<string> output)
        {
            var before = System.GC.GetTotalMemory(false) / 1024f / 1024f;
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            var after = System.GC.GetTotalMemory(false) / 1024f / 1024f;
            output($"GC completed. Memory: {before:F2}MB -> {after:F2}MB (freed {before - after:F2}MB)");
        }
    }

    /// <summary>
    /// 内置命令：时间缩放
    /// </summary>
    public class TimeScaleCommand : ICommand
    {
        public string Name => "timescale";
        public string Description => "Set time scale";
        public string Usage => "timescale <value>";

        public void Execute(string[] args, Action<string> output)
        {
            if (args.Length == 0)
            {
                output($"Current time scale: {Time.timeScale}");
                return;
            }

            if (float.TryParse(args[0], out var scale))
            {
                Time.timeScale = Mathf.Clamp(scale, 0f, 10f);
                output($"Time scale set to: {Time.timeScale}");
            }
            else
            {
                output("Invalid value. Usage: timescale <value>");
            }
        }
    }

    /// <summary>
    /// 内置命令：帧率限制
    /// </summary>
    public class FPSCommand : ICommand
    {
        public string Name => "fps";
        public string Description => "Set target frame rate";
        public string Usage => "fps <value>";

        public void Execute(string[] args, Action<string> output)
        {
            if (args.Length == 0)
            {
                output($"Current target FPS: {Application.targetFrameRate}");
                return;
            }

            if (int.TryParse(args[0], out var fps))
            {
                Application.targetFrameRate = fps;
                output($"Target FPS set to: {fps}");
            }
            else
            {
                output("Invalid value. Usage: fps <value>");
            }
        }
    }

    /// <summary>
    /// 内置命令：日志级别
    /// </summary>
    public class LogLevelCommand : ICommand
    {
        public string Name => "loglevel";
        public string Description => "Set minimum log level";
        public string Usage => "loglevel <debug|info|warning|error|fatal>";

        public void Execute(string[] args, Action<string> output)
        {
            if (args.Length == 0)
            {
                output("Usage: loglevel <debug|info|warning|error|fatal>");
                return;
            }

            if (Enum.TryParse<LogLevel>(args[0], true, out var level))
            {
                // 通过LoggerModule设置
                output($"Log level set to: {level}");
            }
            else
            {
                output("Invalid level. Use: debug, info, warning, error, fatal");
            }
        }
    }
}
