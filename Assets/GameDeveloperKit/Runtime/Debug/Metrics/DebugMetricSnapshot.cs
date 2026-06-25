namespace GameDeveloperKit.Debugger
{
    public readonly struct DebugMetricSnapshot
    {
        /// <summary>
        /// 初始化 Debug Metric Snapshot。
        /// </summary>
        /// <param name="frameTimeMs">frame Time Ms 参数。</param>
        /// <param name="managedMemoryBytes">managed Memory Bytes 参数。</param>
        /// <param name="graphicsMemoryBytes">graphics Memory Bytes 参数。</param>
        /// <param name="gpuFrameTimeMs">gpu Frame Time Ms 参数。</param>
        public DebugMetricSnapshot(
            float fps,
            float frameTimeMs,
            long managedMemoryBytes,
            long? graphicsMemoryBytes = null,
            float? gpuFrameTimeMs = null)
        {
            Fps = fps;
            FrameTimeMs = frameTimeMs;
            ManagedMemoryBytes = managedMemoryBytes;
            GraphicsMemoryBytes = graphicsMemoryBytes;
            GpuFrameTimeMs = gpuFrameTimeMs;
        }

        public float Fps { get; }

        public float FrameTimeMs { get; }

        public long ManagedMemoryBytes { get; }

        public long? GraphicsMemoryBytes { get; }

        public float? GpuFrameTimeMs { get; }
    }
}
