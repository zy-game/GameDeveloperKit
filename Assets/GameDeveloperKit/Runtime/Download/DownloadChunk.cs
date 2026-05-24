namespace GameDeveloperKit.Download
{
    internal sealed class DownloadChunk
    {
        public int Index;
        public long Start;
        public long End;
        public string PartPath;
        public DownloadStatus Status;

        public long Size => End - Start + 1;
    }
}
