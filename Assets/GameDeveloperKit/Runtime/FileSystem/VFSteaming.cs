using System;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.File
{
    public sealed class VFSteaming
    {
        private FileStream m_Stream;

        public string Path { get; }

        public VFSteaming(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));
            }

            Path = path;
            m_Stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        public async UniTask<byte[]> ReadAsync(long offset, int size)
        {
            if (m_Stream == null)
            {
                throw new ObjectDisposedException(nameof(VFSteaming));
            }

            if (offset < 0 || size < 0)
            {
                throw new ArgumentOutOfRangeException("Offset and size must be non-negative.");
            }

            var buffer = new byte[size];
            m_Stream.Seek(offset, SeekOrigin.Begin);
            await m_Stream.ReadAsync(buffer, 0, size);
            return buffer;
        }

        public async UniTask WriteAsync(long offset, byte[] data)
        {
            if (m_Stream == null)
            {
                throw new ObjectDisposedException(nameof(VFSteaming));
            }

            if (offset < 0 || data == null)
            {
                throw new ArgumentOutOfRangeException("Offset must be non-negative and data cannot be null.");
            }

            m_Stream.Seek(offset, SeekOrigin.Begin);
            await m_Stream.WriteAsync(data, 0, data.Length);
        }

        public void Dispose()
        {
            m_Stream?.Dispose();
            m_Stream = null;
        }
    }
}