using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 下载速度统计器（无锁设计）
    /// </summary>
    internal class DownloadSpeedTracker
    {
        private readonly ConcurrentQueue<(long bytes, DateTime time)> _speedSamples;
        private readonly int _maxSamples = 10;
        private DateTime _startTime;
        private long _currentSpeed;
        private long _averageSpeed;

        public DownloadSpeedTracker()
        {
            _speedSamples = new ConcurrentQueue<(long, DateTime)>();
            _startTime = DateTime.UtcNow;
        }

        /// <summary>
        /// 当前下载速度（字节/秒）
        /// </summary>
        public long CurrentSpeed => Interlocked.Read(ref _currentSpeed);

        /// <summary>
        /// 平均下载速度（字节/秒）
        /// </summary>
        public long AverageSpeed => Interlocked.Read(ref _averageSpeed);

        /// <summary>
        /// 更新速度统计
        /// </summary>
        public void Update(long receivedBytes)
        {
            var now = DateTime.UtcNow;

            _speedSamples.Enqueue((receivedBytes, now));

            while (_speedSamples.Count > _maxSamples)
            {
                _speedSamples.TryDequeue(out _);
            }

            if (_speedSamples.Count >= 2)
            {
                var samples = _speedSamples.ToArray();
                if (samples.Length >= 2)
                {
                    var lastTwo = samples.Skip(samples.Length - 2).ToArray();
                    var timeDiff = (lastTwo[1].time - lastTwo[0].time).TotalSeconds;
                    var bytesDiff = lastTwo[1].bytes - lastTwo[0].bytes;

                    if (timeDiff > 0)
                    {
                        var speed = (long)(bytesDiff / timeDiff);
                        Interlocked.Exchange(ref _currentSpeed, speed);
                    }
                }
            }

            var elapsed = (now - _startTime).TotalSeconds;
            if (elapsed > 0)
            {
                var avgSpeed = (long)(receivedBytes / elapsed);
                Interlocked.Exchange(ref _averageSpeed, avgSpeed);
            }
        }

        /// <summary>
        /// 计算预计剩余时间
        /// </summary>
        public TimeSpan CalculateETA(long totalBytes, long receivedBytes)
        {
            var avgSpeed = Interlocked.Read(ref _averageSpeed);
            if (avgSpeed <= 0 || receivedBytes >= totalBytes)
                return TimeSpan.Zero;

            var remaining = totalBytes - receivedBytes;
            var seconds = remaining / (double)avgSpeed;
            return TimeSpan.FromSeconds(seconds);
        }
    }
}
