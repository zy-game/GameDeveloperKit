using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GameDeveloperKit.File
{
    internal sealed class VfsReadStream : Stream
    {
        private readonly long m_Offset;
        private readonly long m_Length;
        private readonly Action<VfsReadStream> m_OnDispose;
        private readonly SemaphoreSlim m_IoGate = new SemaphoreSlim(1, 1);
        private Stream m_Stream;
        private long m_Position;
        private bool m_Disposed;

        internal VfsReadStream(
            string path,
            long offset,
            long length,
            Action<VfsReadStream> onDispose)
            : this(OpenFile(path), offset, length, onDispose)
        {
        }

        internal VfsReadStream(
            Stream stream,
            long offset,
            long length,
            Action<VfsReadStream> onDispose)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new ArgumentException("Read stream must be readable and seekable.", nameof(stream));
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            m_Offset = offset;
            m_Length = length;
            m_OnDispose = onDispose;
            m_Stream = stream;
        }

        public override bool CanRead => !m_Disposed;

        public override bool CanSeek => !m_Disposed;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                ThrowIfDisposed();
                return m_Length;
            }
        }

        public override long Position
        {
            get
            {
                m_IoGate.Wait();
                try
                {
                    ThrowIfDisposed();
                    return m_Position;
                }
                finally
                {
                    m_IoGate.Release();
                }
            }
            set => Seek(value, SeekOrigin.Begin);
        }

        public override void Flush()
        {
            ThrowIfDisposed();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBuffer(buffer, offset, count);
            m_IoGate.Wait();
            try
            {
                var stream = GetStream();
                var remaining = m_Length - m_Position;
                if (remaining <= 0)
                {
                    return 0;
                }

                var readCount = (int)Math.Min(count, remaining);
                stream.Seek(m_Offset + m_Position, SeekOrigin.Begin);
                var read = stream.Read(buffer, offset, readCount);
                m_Position += read;
                return read;
            }
            finally
            {
                m_IoGate.Release();
            }
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            ValidateBuffer(buffer, offset, count);
            await m_IoGate.WaitAsync(cancellationToken);
            try
            {
                var stream = GetStream();
                var remaining = m_Length - m_Position;
                if (remaining <= 0)
                {
                    return 0;
                }

                var readCount = (int)Math.Min(count, remaining);
                stream.Seek(m_Offset + m_Position, SeekOrigin.Begin);
                var read = await stream.ReadAsync(buffer, offset, readCount, cancellationToken).ConfigureAwait(false);
                m_Position += read;
                return read;
            }
            finally
            {
                m_IoGate.Release();
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            m_IoGate.Wait();
            try
            {
                ThrowIfDisposed();
                var position = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => m_Position + offset,
                    SeekOrigin.End => m_Length + offset,
                    _ => throw new ArgumentOutOfRangeException(nameof(origin))
                };
                if (position < 0 || position > m_Length)
                {
                    throw new IOException("Attempted to seek outside the virtual file boundary.");
                }

                m_Position = position;
                return m_Position;
            }
            finally
            {
                m_IoGate.Release();
            }
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                base.Dispose(disposing);
                return;
            }

            var notifyDisposed = false;
            m_IoGate.Wait();
            try
            {
                if (!m_Disposed)
                {
                    m_Disposed = true;
                    m_Stream?.Dispose();
                    m_Stream = null;
                    notifyDisposed = true;
                }
            }
            finally
            {
                m_IoGate.Release();
            }

            if (notifyDisposed)
            {
                m_OnDispose?.Invoke(this);
            }

            base.Dispose(disposing);
        }

        private Stream GetStream()
        {
            ThrowIfDisposed();
            return m_Stream;
        }

        private void ThrowIfDisposed()
        {
            if (m_Disposed || m_Stream == null)
            {
                throw new ObjectDisposedException(nameof(VfsReadStream));
            }
        }

        private static void ValidateBuffer(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || count < 0 || buffer.Length - offset < count)
            {
                throw new ArgumentOutOfRangeException();
            }
        }

        private static FileStream OpenFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty.", nameof(path));
            }

            return new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
        }
    }
}
