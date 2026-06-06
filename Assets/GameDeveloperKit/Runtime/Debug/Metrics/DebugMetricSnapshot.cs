namespace GameDeveloperKit.Logger
{
    public readonly struct DebugMetricSnapshot
    {
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
