using System;
using System.Collections.Generic;

namespace GameDeveloperKit.StoryEditor.Media
{
    internal sealed class ThumbnailSessionCache
    {
        private readonly Dictionary<string, byte[]> m_Data = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public bool TryGet(string url, out byte[] data)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                data = null;
                return false;
            }

            return m_Data.TryGetValue(url, out data);
        }

        public void Set(string url, byte[] data)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("Thumbnail URL cannot be empty.", nameof(url));
            }

            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Thumbnail data cannot be empty.", nameof(data));
            }

            m_Data[url] = (byte[])data.Clone();
        }
    }
}
