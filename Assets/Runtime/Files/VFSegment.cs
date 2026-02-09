using System;

namespace GameDeveloperKit.Files
{
    [Serializable]
    public class VFSegment
    {
        public string name;
        public string version;
        public long start;
        public long length;
    }
}
