using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.File
{
    internal sealed class FileTemporaryHandle
    {
        private readonly string m_NativePath;
        private readonly Action<FileTemporaryHandle> m_OnReleased;
        private readonly SemaphoreSlim m_IoGate = new SemaphoreSlim(1, 1);
        private bool m_Released;

        internal FileTemporaryHandle(
            string owner,
            string identity,
            string nativePath,
            Action<FileTemporaryHandle> onReleased)
        {
            Owner = owner;
            Identity = identity;
            m_NativePath = nativePath;
            m_OnReleased = onReleased;
        }

        internal string Owner { get; }

        internal string Identity { get; }

        internal string NativePath
        {
            get
            {
                m_IoGate.Wait();
                try
                {
                    ThrowIfReleased();
                    return m_NativePath;
                }
                finally
                {
                    m_IoGate.Release();
                }
            }
        }

        internal bool Exists
        {
            get
            {
                m_IoGate.Wait();
                try
                {
                    ThrowIfReleased();
                    return System.IO.File.Exists(m_NativePath);
                }
                finally
                {
                    m_IoGate.Release();
                }
            }
        }

        internal long Length
        {
            get
            {
                m_IoGate.Wait();
                try
                {
                    ThrowIfReleased();
                    return System.IO.File.Exists(m_NativePath)
                        ? new FileInfo(m_NativePath).Length
                        : 0;
                }
                finally
                {
                    m_IoGate.Release();
                }
            }
        }

        internal UniTask<Stream> OpenReadAsync()
        {
            m_IoGate.Wait();
            try
            {
                ThrowIfReleased();
                Stream stream = new FileStream(
                    m_NativePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read | FileShare.Delete);
                return UniTask.FromResult(stream);
            }
            finally
            {
                m_IoGate.Release();
            }
        }

        internal UniTask<Stream> OpenWriteAsync(bool append)
        {
            m_IoGate.Wait();
            try
            {
                ThrowIfReleased();
                Stream stream = new FileStream(
                    m_NativePath,
                    append ? FileMode.Append : FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read);
                return UniTask.FromResult(stream);
            }
            finally
            {
                m_IoGate.Release();
            }
        }

        internal async UniTask MergeFromAsync(
            IReadOnlyList<(FileTemporaryHandle Handle, long Length)> parts,
            CancellationToken cancellationToken)
        {
            if (parts == null)
            {
                throw new ArgumentNullException(nameof(parts));
            }

            await m_IoGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfReleased();
                using (var output = new FileStream(
                           m_NativePath,
                           FileMode.Create,
                           FileAccess.Write,
                           FileShare.Read))
                {
                    var buffer = new byte[81920];
                    foreach (var part in parts)
                    {
                        if (part.Handle == null)
                        {
                            throw new ArgumentException("A temporary part cannot be null.", nameof(parts));
                        }

                        if (ReferenceEquals(part.Handle, this))
                        {
                            throw new ArgumentException("A temporary file cannot merge itself.", nameof(parts));
                        }

                        if (part.Length < 0)
                        {
                            throw new ArgumentOutOfRangeException(nameof(parts), "A temporary part length cannot be negative.");
                        }

                        using (var input = await part.Handle.OpenReadAsync())
                        {
                            var remaining = part.Length;
                            while (remaining > 0)
                            {
                                var read = await input.ReadAsync(
                                        buffer,
                                        0,
                                        (int)Math.Min(buffer.Length, remaining),
                                        cancellationToken)
                                    .ConfigureAwait(false);
                                if (read == 0)
                                {
                                    throw new EndOfStreamException(
                                        $"Temporary part '{part.Handle.Identity}' ended before {part.Length} bytes were read.");
                                }

                                await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                                remaining -= read;
                            }

                            if (input.ReadByte() != -1)
                            {
                                throw new InvalidDataException(
                                    $"Temporary part '{part.Handle.Identity}' exceeded its expected length of {part.Length} bytes.");
                            }
                        }
                    }

                    output.Flush(true);
                }

                cancellationToken.ThrowIfCancellationRequested();
                foreach (var part in parts)
                {
                    await part.Handle.DeleteAsync();
                }
            }
            finally
            {
                m_IoGate.Release();
            }
        }

        internal UniTask DeleteAsync()
        {
            m_IoGate.Wait();
            try
            {
                ThrowIfReleased();
                if (System.IO.File.Exists(m_NativePath))
                {
                    System.IO.File.Delete(m_NativePath);
                }

                return UniTask.CompletedTask;
            }
            finally
            {
                m_IoGate.Release();
            }
        }

        internal UniTask ReleaseAsync()
        {
            m_IoGate.Wait();
            try
            {
                if (m_Released)
                {
                    return UniTask.CompletedTask;
                }

                if (System.IO.File.Exists(m_NativePath))
                {
                    System.IO.File.Delete(m_NativePath);
                }

                m_Released = true;
            }
            finally
            {
                m_IoGate.Release();
            }

            m_OnReleased?.Invoke(this);
            return UniTask.CompletedTask;
        }

        private void ThrowIfReleased()
        {
            if (m_Released)
            {
                throw new ObjectDisposedException(nameof(FileTemporaryHandle));
            }
        }
    }
}
