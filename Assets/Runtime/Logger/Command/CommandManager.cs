using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameDeveloperKit.Log
{
    /// <summary>
    /// 命令管理器
    /// </summary>
    public class CommandManager
    {
        private readonly Dictionary<string, ICommand> _commands = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _history = new();
        private readonly List<string> _output = new();
        private readonly int _maxHistory;
        private readonly int _maxOutput;

        public IReadOnlyList<string> History => _history;
        public IReadOnlyList<string> Output => _output;
        public IReadOnlyCollection<ICommand> Commands => _commands.Values;

        public event Action<string> OnOutput;

        public CommandManager(int maxHistory = 50, int maxOutput = 200)
        {
            _maxHistory = maxHistory;
            _maxOutput = maxOutput;
            RegisterBuiltInCommands();
        }

        public void Register(ICommand command)
        {
            _commands[command.Name] = command;
        }

        public void Register(string name, string description, Action<string[], Action<string>> handler, string usage = null)
        {
            Register(new DelegateCommand(name, description, usage ?? name, handler));
        }

        public void Unregister(string name)
        {
            _commands.Remove(name);
        }

        public bool Execute(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;

            _history.Add(input);
            if (_history.Count > _maxHistory) _history.RemoveAt(0);

            var parts = ParseInput(input);
            if (parts.Length == 0) return false;

            var cmdName = parts[0];
            var args = parts.Skip(1).ToArray();

            if (!_commands.TryGetValue(cmdName, out var command))
            {
                AppendOutput($"Unknown command: {cmdName}. Type 'help' for available commands.");
                return false;
            }

            try
            {
                command.Execute(args, AppendOutput);
                return true;
            }
            catch (Exception ex)
            {
                AppendOutput($"Error executing '{cmdName}': {ex.Message}");
                return false;
            }
        }

        public List<string> GetSuggestions(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                return _commands.Keys.OrderBy(k => k).ToList();

            return _commands.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k)
                .ToList();
        }

        public void ClearOutput()
        {
            _output.Clear();
        }

        public void ClearHistory()
        {
            _history.Clear();
        }

        private void AppendOutput(string text)
        {
            _output.Add(text);
            if (_output.Count > _maxOutput) _output.RemoveAt(0);
            OnOutput?.Invoke(text);
        }

        private static string[] ParseInput(string input)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            foreach (char c in input)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
                result.Add(current.ToString());

            return result.ToArray();
        }

        private void RegisterBuiltInCommands()
        {
            Register(new HelpCommand(this));
            Register(new ClearCommand(this));
            Register(new GCCommand());
            Register(new TimeScaleCommand());
            Register(new FPSCommand());
            Register(new LogLevelCommand());
        }
    }
}
