using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Files
{
    public enum VFSystemType
    {
        SmallFiles,
        Standalone
    }

    [Serializable]
    public class VFSMetadata
    {
        public string systemId;
        public string type;
        public List<VFSegment> segments;
        public int fileCount;
        public long totalSize;
    }

    [Serializable]
    public class SystemListData
    {
        public List<string> systems;
    }
}
