using System.Collections.Generic;
using GameDeveloperKit.Log;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络模块
    /// </summary>
    public class NetworkModule : IModule, INetworkManager
    {
        private readonly Dictionary<string, INetworkTerminal> _terminals = new();

        public void OnStartup()
        {
            // 注册网络调试面板
            DebugConsole.Instance?.RegisterPanel(new NetworkDebugPanel());
        }

        public void OnUpdate(float elapseSeconds) { }

        public void OnClearup()
        {
            foreach (var terminal in _terminals.Values)
                terminal.Dispose();
            _terminals.Clear();
        }

        public INetworkTerminal CreateTerminal(string name, string host, NetworkProtocol protocol = NetworkProtocol.TCP, IMessageSerializer serializer = null)
        {
            if (_terminals.ContainsKey(name))
            {
                Game.Debug.Warning($"Terminal '{name}' already exists");
                return _terminals[name];
            }

            var terminal = new NetworkTerminal(name, host, protocol, serializer);
            _terminals[name] = terminal;
            return terminal;
        }

        public INetworkTerminal GetTerminal(string name)
        {
            _terminals.TryGetValue(name, out var terminal);
            return terminal;
        }

        public void RemoveTerminal(string name)
        {
            if (_terminals.TryGetValue(name, out var terminal))
            {
                terminal.Dispose();
                _terminals.Remove(name);
            }
        }
    }
}
