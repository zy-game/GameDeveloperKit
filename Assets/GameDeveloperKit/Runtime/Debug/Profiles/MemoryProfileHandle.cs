using System;
using UnityEngine;

namespace GameDeveloperKit.Logger
{
    public sealed class MemoryProfileHandle : ProfileHandle
    {
        private const int SampleCapacity = 64;
        private static readonly Color BarColor = new Color(0.2f, 0.75f, 1f, 1f);

        private readonly DebugSettings m_Settings;
        private readonly long[] m_ManagedMemorySamples = new long[SampleCapacity];

        private float m_Elapsed;
        private int m_SampleStart;
        private int m_SampleCount;

        public MemoryProfileHandle() : this(new DebugSettings())
        {
        }

        internal MemoryProfileHandle(DebugSettings settings)
        {
            m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public override string Name => "Memory";

        public DebugMetricSnapshot Metrics { get; private set; }

        internal int SamplesCount => m_SampleCount;

        internal int SamplesCapacity => SampleCapacity;

        public void Reset()
        {
            m_Elapsed = 0f;
            m_SampleStart = 0;
            m_SampleCount = 0;
            Metrics = default;
        }

        public void Sample(float deltaTime)
        {
            if (!m_Settings.MetricsEnabled)
            {
                return;
            }

            m_Elapsed += deltaTime;
            if (m_Elapsed < m_Settings.MetricSampleInterval)
            {
                return;
            }

            var frameTimeMs = deltaTime * 1000f;
            var fps = deltaTime > 0f ? 1f / deltaTime : 0f;
            Metrics = new DebugMetricSnapshot(
                fps,
                frameTimeMs,
                GC.GetTotalMemory(false));
            AddSample(Metrics.ManagedMemoryBytes);
            m_Elapsed = 0f;
        }

        protected internal override void Draw()
        {
            GUILayout.Label($"FPS: {Metrics.Fps:0.0}");
            GUILayout.Label($"Frame Time: {Metrics.FrameTimeMs:0.00}ms");
            GUILayout.Label($"Managed Memory: {FormatBytes(Metrics.ManagedMemoryBytes)}");
            GUILayout.Label(Metrics.GraphicsMemoryBytes.HasValue
                ? $"Graphics Memory: {FormatBytes(Metrics.GraphicsMemoryBytes.Value)}"
                : "Graphics Memory: unavailable");
            GUILayout.Label(Metrics.GpuFrameTimeMs.HasValue
                ? $"GPU Frame Time: {Metrics.GpuFrameTimeMs.Value:0.00}ms"
                : "GPU Frame Time: unavailable");

            var rect = GUILayoutUtility.GetRect(320f, 96f, GUILayout.ExpandWidth(true));
            DrawBars(rect);
        }

        private void AddSample(long managedMemoryBytes)
        {
            if (m_SampleCount < SampleCapacity)
            {
                m_ManagedMemorySamples[m_SampleCount++] = managedMemoryBytes;
                return;
            }

            m_ManagedMemorySamples[m_SampleStart] = managedMemoryBytes;
            m_SampleStart = (m_SampleStart + 1) % SampleCapacity;
        }

        private void DrawBars(Rect rect)
        {
            GUI.Box(rect, GUIContent.none);
            if (m_SampleCount == 0)
            {
                GUI.Label(rect, "Collecting memory samples...");
                return;
            }

            var min = long.MaxValue;
            var max = long.MinValue;
            for (var i = 0; i < m_SampleCount; i++)
            {
                var value = GetSample(i);
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }

            GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, rect.width - 8f, 20f), $"Managed {FormatBytes(min)} - {FormatBytes(max)}");
            var chartRect = new Rect(rect.x + 4f, rect.y + 24f, rect.width - 8f, rect.height - 28f);
            DrawBarSamples(chartRect, min, max);
        }

        private long GetSample(int index)
        {
            var sampleIndex = (m_SampleStart + index) % SampleCapacity;
            return m_ManagedMemorySamples[sampleIndex];
        }

        private void DrawBarSamples(Rect rect, long min, long max)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var previousColor = GUI.color;
            GUI.color = BarColor;
            var stride = rect.width / m_SampleCount;
            var gap = stride >= 4f ? 2f : 0f;
            var barWidth = Mathf.Max(1f, stride - gap);
            var range = max - min;
            for (var i = 0; i < m_SampleCount; i++)
            {
                var normalized = range > 0L ? (GetSample(i) - min) / (float)range : 1f;
                var barHeight = Mathf.Max(1f, rect.height * Mathf.Clamp01(normalized));
                var x = rect.x + i * stride;
                var y = rect.yMax - barHeight;
                GUI.DrawTexture(new Rect(x, y, Mathf.Min(barWidth, rect.xMax - x), barHeight), Texture2D.whiteTexture);
            }

            GUI.color = previousColor;
        }

        private static string FormatBytes(long bytes)
        {
            return $"{bytes / 1024f / 1024f:0.0}MB";
        }
    }
}
