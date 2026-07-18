using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.File
{
    /// <summary>
    /// 虚拟文件流，用于在包文件指定偏移位置读取和写入数据。
    /// </summary>
    public sealed class VFSteaming
    {
        private FileStream m_Stream;
        private readonly SemaphoreSlim m_IoGate = new SemaphoreSlim(1, 1);

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
            m_Stream = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read | FileShare.Delete);
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
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (size < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            await m_IoGate.WaitAsync();
            try
            {
                var stream = m_Stream ?? throw new ObjectDisposedException(nameof(VFSteaming));
                var buffer = new byte[size];
                stream.Seek(offset, SeekOrigin.Begin);
                var totalBytesRead = 0;
                while (totalBytesRead < size)
                {
                    var bytesRead = await stream.ReadAsync(buffer, totalBytesRead, size - totalBytesRead);
                    if (bytesRead == 0)
                    {
                        throw new EndOfStreamException(
                            $"Unable to read {size} bytes from '{Path}' at offset {offset}. Read {totalBytesRead} bytes before reaching the end of the stream.");
                    }

                    totalBytesRead += bytesRead;
                }

                return buffer;
            }
            finally
            {
                m_IoGate.Release();
            }
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
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            await m_IoGate.WaitAsync();
            try
            {
                var stream = m_Stream ?? throw new ObjectDisposedException(nameof(VFSteaming));
                stream.Seek(offset, SeekOrigin.Begin);
                await stream.WriteAsync(data, 0, data.Length);
                stream.Flush(true);
            }
            finally
            {
                m_IoGate.Release();
            }
        }

        /// <summary>
        /// 释放虚拟文件流。
        /// </summary>
        public void Dispose()
        {
            m_Stream?.Dispose();
            m_Stream = null;
        }

        internal async UniTask<(long Size, uint Crc32)> WriteAsync(long offset, Stream source)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!source.CanRead)
            {
                throw new ArgumentException("Source stream must be readable.", nameof(source));
            }

            await m_IoGate.WaitAsync();
            try
            {
                var stream = m_Stream ?? throw new ObjectDisposedException(nameof(VFSteaming));
                var buffer = new byte[81920];
                var size = 0L;
                var crc = Crc32Utility.InitialValue;
                stream.Seek(offset, SeekOrigin.Begin);
                while (true)
                {
                    var read = await source.ReadAsync(buffer, 0, buffer.Length);
                    if (read == 0)
                    {
                        break;
                    }

                    await stream.WriteAsync(buffer, 0, read);
                    crc = Crc32Utility.Append(crc, buffer, 0, read);
                    size += read;
                }

                stream.Flush(true);
                return (size, Crc32Utility.Complete(crc));
            }
            finally
            {
                m_IoGate.Release();
            }
        }

        internal async UniTask DisposeAsync()
        {
            await m_IoGate.WaitAsync();
            try
            {
                m_Stream?.Dispose();
                m_Stream = null;
            }
            finally
            {
                m_IoGate.Release();
            }
        }
    }
}
