namespace GameDeveloperKit
{
    public enum StorageType
    {
        Packed,
        Standalone
    }

    public class VFSMeta
    {
        public string FilePath;
        public long Offset;
        public long Size;
        public uint Crc32;
        public bool Usegd;
        public string Version;
        public long Timestamp;
        public string BundlePath;
        public StorageType Storage;

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

        public void Unused()
        {
            FilePath = null;
            Size = 0;
            Crc32 = 0;
            Version = null;
            Timestamp = 0;
            Usegd = false;
        }
    }
}
