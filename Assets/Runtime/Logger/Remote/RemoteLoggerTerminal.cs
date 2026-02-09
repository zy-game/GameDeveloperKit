using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Network;
using UnityEngine;

namespace GameDeveloperKit.Log
{
    /// <summary>
    /// 远程日志终端
    /// </summary>
    public class RemoteLoggerTerminal : IDisposable
    {
        private readonly RemoteLoggerConfig _config;
        private readonly Queue<LogEntry> _pendingLogs = new();
        private readonly object _lock = new();
        
        private INetworkTerminal _terminal;
        private CancellationTokenSource _cts;
        private bool _isRunning;
        private int _retryCount;

        public bool IsConnected => _terminal?.IsConnected ?? false;
        public NetworkState State => _terminal?.State ?? NetworkState.Disconnected;

        public RemoteLoggerTerminal(RemoteLoggerConfig config)
        {
            _config = config;
        }

        public async UniTask StartAsync()
        {
            if (!_config.Enabled || _isRunning) return;
            
            _isRunning = true;
            _cts = new CancellationTokenSource();
            _retryCount = 0;

            _terminal = new NetworkTerminal(
                "RemoteLogger",
                _config.GetAddress(),
                _config.Protocol
            );

            _terminal.OnStateChanged(OnStateChanged);
            
            await ConnectAsync();
            StartFlushLoop().Forget();
        }

        public void Stop()
        {
            _isRunning = false;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _terminal?.Dispose();
            _terminal = null;
        }

        public void Enqueue(LogEntry entry)
        {
            if (!_config.Enabled || entry.Level < _config.MinUploadLevel) return;
            
            lock (_lock)
            {
                _pendingLogs.Enqueue(entry);
            }
        }

        private async UniTask ConnectAsync()
        {
            if (_terminal == null || !_isRunning) return;

            try
            {
                await _terminal.ConnectAsync(_cts.Token);
                _retryCount = 0;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RemoteLogger] Connect failed: {ex.Message}");
            }
        }

        private void OnStateChanged(NetworkState state)
        {
            if (state == NetworkState.Disconnected && _isRunning)
            {
                ScheduleReconnect().Forget();
            }
        }

        private async UniTaskVoid ScheduleReconnect()
        {
            if (!_isRunning || _retryCount >= _config.MaxRetryCount) return;
            
            _retryCount++;
            await UniTask.Delay(_config.ReconnectIntervalMs, cancellationToken: _cts.Token);
            await ConnectAsync();
        }

        private async UniTaskVoid StartFlushLoop()
        {
            while (_isRunning && !_cts.Token.IsCancellationRequested)
            {
                await UniTask.Delay(_config.FlushIntervalMs, cancellationToken: _cts.Token);
                Flush();
            }
        }

        private void Flush()
        {
            if (!IsConnected) return;

            List<LogEntry> batch;
            lock (_lock)
            {
                if (_pendingLogs.Count == 0) return;
                
                int count = Math.Min(_pendingLogs.Count, _config.BatchSize);
                batch = new List<LogEntry>(count);
                for (int i = 0; i < count; i++)
                {
                    batch.Add(_pendingLogs.Dequeue());
                }
            }

            var message = new RemoteLogMessage
            {
                DeviceId = SystemInfo.deviceUniqueIdentifier,
                DeviceModel = SystemInfo.deviceModel,
                AppVersion = Application.version,
                Logs = new RemoteLogMessage.LogEntryData[batch.Count]
            };

            for (int i = 0; i < batch.Count; i++)
            {
                message.Logs[i] = RemoteLogMessage.LogEntryData.FromEntry(batch[i]);
            }

            _terminal.Send(message);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
