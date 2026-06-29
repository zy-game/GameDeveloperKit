namespace GameDeveloperKit.File
{
    /// <summary>
    /// 虚拟文件元数据，用于描述虚拟路径、包内偏移、大小和版本等信息。
    /// </summary>
    public class VFSMeta
    {
        /// <summary>
        /// 虚拟文件路径。
        /// </summary>
        public string FilePath;

        /// <summary>
        /// 文件在包内的起始偏移。
        /// </summary>
        public long Offset;

        /// <summary>
        /// 文件大小，单位为字节。
        /// </summary>
        public long Size;

        /// <summary>
        /// 文件CRC32校验值。
        /// </summary>
        public uint Crc32;

        /// <summary>
        /// 条目是否正在使用。
        /// </summary>
        public bool Usegd;

        /// <summary>
        /// 文件版本。
        /// </summary>
        public string Version;

        /// <summary>
        /// 文件写入时间戳。
        /// </summary>
        public long Timestamp;

        /// <summary>
        /// 文件所在的包路径。
        /// </summary>
        public string BundlePath;

        /// <summary>
        /// 文件存储类型。
        /// </summary>
        public StorageType Storage;

        /// <summary>
        /// 将清单条目标记为已使用，并写入文件元数据。
        /// </summary>
        /// <param name="path">虚拟文件路径。</param>
        /// <param name="size">文件大小。</param>
        /// <param name="crc32">CRC32校验值。</param>
        /// <param name="version">文件版本。</param>
        /// <param name="timestamp">写入时间戳。</param>
        /// <param name="storage">文件存储类型。</param>
        public void Used(string path, long size, uint crc32, string version, long timestamp, StorageType storage)
        {
            FilePath = path;
            Size = size;
            Crc32 = crc32;
            Version = version;
            Timestamp = timestamp;
            Usegd = true;
            Storage = storage;
        }

        /// <summary>
        /// 将清单条目标记为空闲，并清理文件元数据。
        /// </summary>
        /// <returns>释放前所在的包路径。</returns>
        public string Unused()
        {
            var bundlePath = BundlePath;
            FilePath = null;
            Size = 0;
            Crc32 = 0;
            Version = null;
            Timestamp = 0;
            Usegd = false;
            return bundlePath;
        }

        /// <summary>
        /// 清理条目关联的包路径。
        /// </summary>
        public void ClearBundlePath()
        {
            BundlePath = null;
        }
    }
}
