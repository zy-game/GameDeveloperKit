using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace GameDeveloperKit.Log
{
    /// <summary>
    /// 性能数据收集器
    /// </summary>
    public class ProfilerCollector
    {
        private readonly float[] _fpsHistory;
        private readonly float[] _memoryHistory;
        private readonly int _historySize;
        private int _historyIndex;
        
        private float _deltaTime;
        private int _frameCount;
        private float _fpsTimer;
        private float _currentFps;
        private float _minFps = float.MaxValue;
        private float _maxFps;
        private float _avgFps;
        private float _fpsSum;
        private int _fpsSampleCount;

        public float CurrentFPS => _currentFps;
        public float MinFPS => _minFps == float.MaxValue ? 0 : _minFps;
        public float MaxFPS => _maxFps;
        public float AvgFPS => _fpsSampleCount > 0 ? _fpsSum / _fpsSampleCount : 0;
        
        public float UsedMemoryMB => Profiler.GetTotalAllocatedMemoryLong() / 1024f / 1024f;
        public float ReservedMemoryMB => Profiler.GetTotalReservedMemoryLong() / 1024f / 1024f;
        public float MonoHeapMB => Profiler.GetMonoHeapSizeLong() / 1024f / 1024f;
        public float MonoUsedMB => Profiler.GetMonoUsedSizeLong() / 1024f / 1024f;
        
        public IReadOnlyList<float> FPSHistory => _fpsHistory;
        public IReadOnlyList<float> MemoryHistory => _memoryHistory;

        public ProfilerCollector(int historySize = 60)
        {
            _historySize = historySize;
            _fpsHistory = new float[historySize];
            _memoryHistory = new float[historySize];
        }

        public void Update()
        {
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
            _frameCount++;
            _fpsTimer += Time.unscaledDeltaTime;

            if (_fpsTimer >= 1f)
            {
                _currentFps = _frameCount / _fpsTimer;
                _frameCount = 0;
                _fpsTimer = 0;

                if (_currentFps < _minFps) _minFps = _currentFps;
                if (_currentFps > _maxFps) _maxFps = _currentFps;
                _fpsSum += _currentFps;
                _fpsSampleCount++;

                // 记录历史
                _fpsHistory[_historyIndex] = _currentFps;
                _memoryHistory[_historyIndex] = MonoUsedMB;
                _historyIndex = (_historyIndex + 1) % _historySize;
            }
        }

        public void Reset()
        {
            _minFps = float.MaxValue;
            _maxFps = 0;
            _fpsSum = 0;
            _fpsSampleCount = 0;
            _historyIndex = 0;
            for (int i = 0; i < _historySize; i++)
            {
                _fpsHistory[i] = 0;
                _memoryHistory[i] = 0;
            }
        }

        public ProfileSnapshot TakeSnapshot()
        {
            return new ProfileSnapshot
            {
                Timestamp = System.DateTime.Now,
                FPS = _currentFps,
                MinFPS = MinFPS,
                MaxFPS = _maxFps,
                AvgFPS = AvgFPS,
                UsedMemoryMB = UsedMemoryMB,
                ReservedMemoryMB = ReservedMemoryMB,
                MonoHeapMB = MonoHeapMB,
                MonoUsedMB = MonoUsedMB
            };
        }
    }

    public struct ProfileSnapshot
    {
        public System.DateTime Timestamp;
        public float FPS;
        public float MinFPS;
        public float MaxFPS;
        public float AvgFPS;
        public float UsedMemoryMB;
        public float ReservedMemoryMB;
        public float MonoHeapMB;
        public float MonoUsedMB;

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] FPS: {FPS:F1} (Min:{MinFPS:F1} Max:{MaxFPS:F1} Avg:{AvgFPS:F1}) | Memory: {MonoUsedMB:F1}MB/{MonoHeapMB:F1}MB";
        }
    }
}
