using System;
using System.Diagnostics;

namespace GameDeveloperKit
{
    /// <summary>
    /// 性能监控工具（可选功能）
    /// </summary>
    public static class PerformanceMonitor
    {
        private static long _lastGCMemory;
        private static Stopwatch _frameTimer = new Stopwatch();
        private static float _averageFrameTime;
        private static int _frameCount;
        
        /// <summary>
        /// 是否启用监控
        /// </summary>
        public static bool Enabled { get; set; } = false;
        
        /// <summary>
        /// GC分配量（字节）
        /// </summary>
        public static long GCAllocations { get; private set; }
        
        /// <summary>
        /// 平均帧时间（毫秒）
        /// </summary>
        public static float AverageUpdateTime => _averageFrameTime;
        
        /// <summary>
        /// 总帧数
        /// </summary>
        public static int FrameCount => _frameCount;
        
        /// <summary>
        /// 开始帧计时
        /// </summary>
        public static void StartFrame()
        {
            if (!Enabled) return;
            
            _frameTimer.Restart();
        }
        
        /// <summary>
        /// 结束帧计时
        /// </summary>
        public static void EndFrame()
        {
            if (!Enabled) return;
            
            _frameTimer.Stop();
            
            // 计算平均帧时间（移动平均）
            var frameTime = (float)_frameTimer.Elapsed.TotalMilliseconds;
            _averageFrameTime = (_averageFrameTime * _frameCount + frameTime) / (_frameCount + 1);
            _frameCount++;
            
            // 每100帧检查一次GC分配
            if (_frameCount % 100 == 0)
            {
                var currentMemory = GC.GetTotalMemory(false);
                GCAllocations = currentMemory - _lastGCMemory;
                _lastGCMemory = currentMemory;
                
                // 可选：打印日志
                if (_frameCount % 1000 == 0)
                {
                    Game.Debug.Info($"[Performance] Avg Frame: {_averageFrameTime:F2}ms, GC: {GCAllocations / 1024}KB/100frames");
                }
            }
        }
        
        /// <summary>
        /// 重置统计
        /// </summary>
        public static void Reset()
        {
            _averageFrameTime = 0;
            _frameCount = 0;
            GCAllocations = 0;
            _lastGCMemory = GC.GetTotalMemory(false);
        }
    }
}
