using System;
using System.IO;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.File
{
    public partial class FileModule
    {
        /// <summary>
        /// 写入虚拟文件，并记录文件版本、校验值和存储位置。
        /// </summary>
        /// <param name="path">虚拟文件路径。</param>
        /// <param name="version">文件版本。</param>
        /// <param name="data">文件数据。</param>
        /// <returns>写入任务。</returns>
        /// <exception cref="ArgumentNullException">文件数据为空时抛出。</exception>
        /// <exception cref="GameException">没有可用的清单条目时抛出。</exception>
        public async UniTask WriteAsync(string path, string version, byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            using (var source = new MemoryStream(data, false))
            {
                var storageType = data.Length < VfsConstants.DefaultThreshold
                    ? StorageType.Packed
                    : StorageType.Standalone;
                await WriteInternalAsync(path, version, source, storageType, null);
            }
        }

        /// <summary>
        /// 从可读流写入虚拟文件。
        /// </summary>
        /// <param name="path">虚拟文件路径。</param>
        /// <param name="version">文件版本。</param>
        /// <param name="source">源数据流。</param>
        /// <returns>写入任务。</returns>
        public UniTask WriteAsync(string path, string version, Stream source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!source.CanRead)
            {
                throw new ArgumentException("Source stream must be readable.", nameof(source));
            }

            return WriteInternalAsync(path, version, source, StorageType.Standalone, null);
        }

        /// <summary>
        /// 读取虚拟文件数据。
        /// </summary>
        /// <param name="path">虚拟文件路径。</param>
        /// <returns>文件数据；如果文件不存在或未启用，则返回null。</returns>
        public async UniTask<byte[]> ReadAsync(string path)
        {
            using (var stream = await OpenReadAsync(path))
            {
                if (stream == null)
                {
                    return null;
                }

                if (stream.Length > int.MaxValue)
                {
                    throw new GameException($"Virtual file is too large for byte[] read. Use OpenReadAsync: {path}");
                }

                var data = new byte[(int)stream.Length];
                var offset = 0;
                while (offset < data.Length)
                {
                    var read = await stream.ReadAsync(data, offset, data.Length - offset);
                    if (read == 0)
                    {
                        throw new EndOfStreamException($"Virtual file ended early: {path}");
                    }

                    offset += read;
                }

                return data;
            }
        }

        /// <summary>
        /// 打开受虚拟文件边界限制的只读流。
        /// </summary>
        /// <param name="path">虚拟文件路径。</param>
        /// <param name="version">期望版本；为空时不校验版本。</param>
        /// <returns>调用方负责释放的只读流；文件不存在或版本不匹配时返回null。</returns>
        public async UniTask<Stream> OpenReadAsync(string path, string version = "")
        {
            ValidateVirtualPath(path, nameof(path));
            BeginOperation();
            VfsReadStream stream = null;
            try
            {
                EnsureReady();
                if (!m_Manifest.TryGetEntry(path, out var currentEntry) ||
                    !currentEntry.Usegd ||
                    (!string.IsNullOrEmpty(version) && currentEntry.Version != version))
                {
                    EndOperation();
                    return null;
                }

                var entry = currentEntry.Clone();
                var fullPath = Path.Combine(m_RootPath, entry.BundlePath);
                stream = new VfsReadStream(fullPath, entry.Offset, entry.Size, OnReadStreamDisposed);
                m_ReadStreams.Add(stream);
                await ValidateReadStreamAsync(path, stream, entry.Crc32);
                stream.Position = 0;
                return stream;
            }
            catch
            {
                if (stream != null)
                {
                    stream.Dispose();
                }
                else
                {
                    EndOperation();
                }

                throw;
            }
        }

        private async UniTask WriteInternalAsync(
            string path,
            string version,
            Stream source,
            StorageType storageType,
            Func<Stream, UniTask> verifier)
        {
            BeginOperation();
            try
            {
                ValidateVirtualPath(path, nameof(path));
                EnsureReady();
                await m_MutationGate.WaitAsync();
                try
                {
                    EnsureReady();
                    var candidate = m_Manifest.Clone();
                    candidate.TryGetEntry(path, out var previousEntry);
                    var entry = storageType == StorageType.Packed
                        ? candidate.AllocatePackedEntry(previousEntry)
                        : candidate.AllocateStandaloneEntry();
                    var newBundlePath = entry.BundlePath;
                    string releasedBundlePath = null;

                    try
                    {
                        var steaming = GetOrCreateSteaming(newBundlePath);
                        var result = await steaming.WriteAsync(entry.Offset, source);
                        if (storageType == StorageType.Packed && result.Size >= VfsConstants.DefaultThreshold)
                        {
                            throw new GameException(
                                $"Packed VFS entry exceeded the {VfsConstants.DefaultThreshold} byte limit: {path}");
                        }

                        entry.Used(
                            path,
                            result.Size,
                            result.Crc32,
                            version,
                            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            storageType);
                        if (verifier != null)
                        {
                            var fullPath = Path.Combine(m_RootPath, newBundlePath);
                            using (var verifyStream = new VfsReadStream(
                                       fullPath,
                                       entry.Offset,
                                       entry.Size,
                                       null))
                            {
                                await verifier(verifyStream);
                            }
                        }

                        releasedBundlePath = candidate.ReleaseEntry(previousEntry);
                        await candidate.SaveAtomicAsync();
                    }
                    catch (Exception commitException)
                    {
                        try
                        {
                            await CleanupUncommittedBundleAsync(newBundlePath);
                        }
                        catch (Exception cleanupException)
                        {
                            throw new AggregateException(
                                $"VFS write for '{path}' failed and uncommitted bundle cleanup also failed.",
                                commitException,
                                cleanupException);
                        }

                        throw;
                    }

                    m_Manifest = candidate;
                    await DeleteBundleAfterCommitAsync(releasedBundlePath, $"write '{path}'");
                }
                finally
                {
                    m_MutationGate.Release();
                }
            }
            finally
            {
                EndOperation();
            }
        }

        private static async UniTask ValidateReadStreamAsync(
            string path,
            VfsReadStream stream,
            uint expectedCrc32)
        {
            var buffer = new byte[81920];
            var crc = Crc32Utility.InitialValue;
            var totalRead = 0L;
            while (totalRead < stream.Length)
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    throw new EndOfStreamException(
                        $"Virtual file '{path}' ended after {totalRead} of {stream.Length} bytes.");
                }

                crc = Crc32Utility.Append(crc, buffer, 0, read);
                totalRead += read;
            }

            var actualCrc32 = Crc32Utility.Complete(crc);
            if (actualCrc32 != expectedCrc32)
            {
                throw new GameException($"File checksum mismatch: {path}");
            }
        }

        private void OnReadStreamDisposed(VfsReadStream stream)
        {
            if (m_ReadStreams.Remove(stream))
            {
                EndOperation();
            }
        }

        private void CloseReadStreams()
        {
            foreach (var stream in m_ReadStreams.ToArray())
            {
                stream.Dispose();
            }

            m_ReadStreams.Clear();
        }
    }
}
