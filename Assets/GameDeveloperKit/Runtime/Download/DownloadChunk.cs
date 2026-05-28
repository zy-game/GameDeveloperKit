using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Download
{
    /// <summary>
    /// 下载分块信息
    /// </summary>
    internal sealed class DownloadChunk
    {
        /// <summary>
        /// 分块索引
        /// </summary>
        public int Index;
        /// <summary>
        /// 分块起始位置
        /// </summary>
        public long Start;
        /// <summary>
        /// 分块结束位置
        /// </summary>
        public long End;
        /// <summary>
        /// 分块路径
        /// </summary>
        public string PartPath;
        /// <summary>
        /// 下载状态
        /// </summary>
        public OperationStatus Status;
        /// <summary>
        /// 分块大小
        /// </summary>
        public long Size => End - Start + 1;
    }
}
