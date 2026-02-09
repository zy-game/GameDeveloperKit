using System;
using GameDeveloperKit.Network;

namespace GameDeveloperKit.Log
{
    /// <summary>
    /// 远程日志配置
    /// </summary>
    [Serializable]
    public class RemoteLoggerConfig
    {
        public bool Enabled = false;
        public string Host = "localhost";
        public int Port = 9000;
        public NetworkProtocol Protocol = NetworkProtocol.WebSocket;
        public LogLevel MinUploadLevel = LogLevel.Warning;
        public int ReconnectIntervalMs = 5000;
        public int MaxRetryCount = 3;
        public int BatchSize = 10;
        public int FlushIntervalMs = 1000;

        public string GetAddress()
        {
            return Protocol == NetworkProtocol.WebSocket
                ? $"ws://{Host}:{Port}"
                : $"{Host}:{Port}";
        }
    }
}
