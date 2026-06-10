using System;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.File
{
    /// <summary>
    /// 虚拟文件流，用于在包文件指定偏移位置读取和写入数据。
    /// </summary>
    public sealed class VFSteaming
    {
        /// <summary>
        /// 存储 Stream。
        /// </summary>
        private FileStream m_Stream;

        /// <summary>
        /// 虚拟文件流对应的包文件路径。
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// 初始化虚拟文件流。
        /// </summary>
        /// <param name="path">包文件路径。</param>
        /// <exception cref="ArgumentException">包文件路径为空时抛出。</exception>
        public VFSteaming(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));
            }

            Path = path;
            m_Stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        /// <summary>
        /// 从指定偏移位置读取指定长度的数据。
        /// </summary>
        /// <param name="offset">读取起始偏移。</param>
        /// <param name="size">读取字节数。</param>
        /// <returns>读取到的数据。</returns>
        /// <exception cref="ObjectDisposedException">虚拟文件流已经释放时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException">偏移或长度为负数时抛出。</exception>
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

        /// <summary>
        /// 将数据写入指定偏移位置。
        /// </summary>
        /// <param name="offset">写入起始偏移。</param>
        /// <param name="data">写入数据。</param>
        /// <returns>写入任务。</returns>
        /// <exception cref="ObjectDisposedException">虚拟文件流已经释放时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException">偏移为负数或数据为空时抛出。</exception>
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

        /// <summary>
        /// 释放虚拟文件流。
        /// </summary>
        public void Dispose()
        {
            m_Stream?.Dispose();
            m_Stream = null;
        }
    }
}
