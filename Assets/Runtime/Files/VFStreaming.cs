using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Files
{
    public class VFStreaming : IDisposable
    {
        private readonly string _filePath;
        private FileStream _stream;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private bool _disposed;

        public VFStreaming(string filePath)
        {
            _filePath = filePath;
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _stream = new FileStream(
                _filePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous);
        }

        public async UniTask<byte[]> ReadAsync(int start, int length, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (_stream == null)
            {
                return null;
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                var buffer = new byte[length];
                _stream.Seek(start, SeekOrigin.Begin);
                int read = 0;
                while (read < length)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int r = await _stream.ReadAsync(buffer, read, length - read, cancellationToken);
                    if (r == 0)
                    {
                        break;
                    }
                    read += r;
                }
                if (read != length)
                {
                    var actual = new byte[read];
                    System.Buffer.BlockCopy(buffer, 0, actual, 0, read);
                    return actual;
                }
                return buffer;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async UniTask WriteAsync(int start, byte[] bytes, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (_stream == null)
            {
                return;
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _stream.Seek(start, SeekOrigin.Begin);
                await _stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                await _stream.FlushAsync(cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async UniTask WriteFromFileAsync(
            string sourceFilePath,
            int start,
            long length,
            CancellationToken cancellationToken = default,
            System.Action<float> onProgress = null)
        {
            ThrowIfDisposed();
            Throw.Asserts(File.Exists(sourceFilePath), new FileNotFoundException("Source file not found.", sourceFilePath));

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                const int bufferSize = 64 * 1024;
                var buffer = new byte[bufferSize];
                long remaining = length;
                long totalLength = System.Math.Max(1, length);

                using (var sourceStream = new FileStream(
                           sourceFilePath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.Read,
                           bufferSize,
                           FileOptions.Asynchronous))
                {
                    _stream.Seek(start, SeekOrigin.Begin);

                    while (remaining > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        int readLength = (int)System.Math.Min(buffer.Length, remaining);
                        int read = await sourceStream.ReadAsync(buffer, 0, readLength, cancellationToken);
                        if (read <= 0)
                            break;

                        await _stream.WriteAsync(buffer, 0, read, cancellationToken);

                        remaining -= read;

                        onProgress?.Invoke((float)(totalLength - remaining) / totalLength);
                    }

                    await _stream.FlushAsync(cancellationToken);
                }

                onProgress?.Invoke(1f);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public UniTask CloseAsync()
        {
            Dispose();
            return UniTask.CompletedTask;
        }

        public long Length()
        {
            ThrowIfDisposed();
            return _stream?.Length ?? 0;
        }

        private void ThrowIfDisposed()
        {
            Throw.Asserts(!_disposed, new ObjectDisposedException(nameof(VFStreaming)));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stream?.Dispose();
            _stream = null;
            _semaphore?.Dispose();
        }
    }
}

