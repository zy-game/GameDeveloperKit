using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Files
{
    public class VFSystem : IDisposable
    {
        public string SystemId { get; }
        public VFSystemType Type { get; private set; }

        private readonly string _rootPath;
        private readonly string _metadataPath;
        private readonly string _storagePath;

        private VFSMetadata _metadata;
        private Dictionary<string, List<VFSegment>> _nameIndex;
        private VFStreaming _storage;
        private ReaderWriterLockSlim _lock;
        private const int MAX_SMALL_FILES = 20;
        private const int MAX_SEGMENT_SIZE = 4 * 1024 * 1024;
        private const long MAX_STORAGE_SIZE = 64L * 1024L * 1024L;

        public VFSystem(string systemId, string rootPath)
        {
            SystemId = systemId;
            _rootPath = rootPath;
            _lock = new ReaderWriterLockSlim();
            _metadataPath = Path.Combine(rootPath, $"{systemId}.data");
            _storagePath = Path.Combine(rootPath, $"{systemId}.storage");
        }

        public void Initialize()
        {
            EnsureDirectory(_rootPath);

            if (File.Exists(_metadataPath))
            {
                var json = File.ReadAllText(_metadataPath);
                Game.Debug.Debug(json);
                _metadata = JsonUtility.FromJson<VFSMetadata>(json);
                Type = _metadata.type == "Standalone"
                    ? VFSystemType.Standalone
                    : VFSystemType.SmallFiles;
            }
            else
            {
                Type = VFSystemType.SmallFiles;
                _metadata = new VFSMetadata
                {
                    systemId = SystemId,
                    type = "SmallFiles",
                    segments = new List<VFSegment>(),
                    fileCount = 0,
                    totalSize = 0
                };
                SaveMetadata();
            }

            _storage = new VFStreaming(_storagePath);
            RebuildIndex();
        }

        public bool Contains(string name, string version = "")
        {
            if (!_nameIndex.TryGetValue(name, out var segments))
                return false;

            if (string.IsNullOrEmpty(version))
                return segments.Count > 0;

            return segments.Exists(s => s.version == version);
        }

        public bool CanWriteable(int dataSize)
        {
            if (Type == VFSystemType.Standalone)
                return _metadata.fileCount == 0;

            if (_metadata.fileCount >= MAX_SMALL_FILES)
                return false;

            if (dataSize > MAX_SEGMENT_SIZE)
                return false;

            if (_metadata.totalSize + dataSize > MAX_STORAGE_SIZE)
                return false;

            return true;
        }

        public async UniTask<RawHandle> ReadAsync(
            string name,
            string version,
            CancellationToken cancellationToken = default)
        {
            VFSegment segment = null;
            if (_nameIndex.TryGetValue(name, out var segments))
            {
                segment = string.IsNullOrEmpty(version)
                    ? segments[^1]
                    : segments.Find(s => s.version == version);
            }

            if (segment == null)
                return null;

            var bytes = await _storage.ReadAsync((int)segment.start, (int)segment.length, cancellationToken);
            if (bytes == null)
                return null;

            return new RawHandle(bytes, name);
        }

        public async UniTask<RawHandle> WriteAsync(
            string name,
            string version,
            byte[] bytes,
            CancellationToken cancellationToken = default)
        {
            Delete(name);

            var start = (int)_storage.Length();

            await _storage.WriteAsync(start, bytes, cancellationToken);

            var segment = new VFSegment
            {
                name = name,
                version = version,
                start = start,
                length = bytes.Length
            };

            _lock.EnterWriteLock();
            try
            {
                _metadata.segments.Add(segment);
                _metadata.fileCount++;
                _metadata.totalSize += bytes.Length;

                if (!_nameIndex.ContainsKey(name))
                    _nameIndex[name] = new List<VFSegment>();
                _nameIndex[name].Add(segment);
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            SaveMetadata();
            return new RawHandle(bytes, name);
        }

        public async UniTask AddFromFileAsync(
            string localFilePath,
            string name,
            string version,
            CancellationToken cancellationToken = default,
            Action<float> onProgress = null)
        {
            Throw.Asserts(File.Exists(localFilePath), new FileNotFoundException($"文件不存在: {localFilePath}", localFilePath));

            Delete(name);

            var fileInfo = new FileInfo(localFilePath);
            var start = (int)_storage.Length();

            await _storage.WriteFromFileAsync(localFilePath, start, fileInfo.Length, cancellationToken, onProgress);

            var segment = new VFSegment
            {
                name = name,
                version = version,
                start = start,
                length = fileInfo.Length
            };

            _lock.EnterWriteLock();
            try
            {
                _metadata.segments.Add(segment);
                _metadata.fileCount++;
                _metadata.totalSize += fileInfo.Length;

                if (!_nameIndex.ContainsKey(name))
                    _nameIndex[name] = new List<VFSegment>();
                _nameIndex[name].Add(segment);
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            SaveMetadata();
        }

        public void Delete(string name, string version = "")
        {
            _lock.EnterWriteLock();
            List<VFSegment> deleted;
            try
            {
                deleted = string.IsNullOrEmpty(version)
                    ? _metadata.segments.FindAll(s => s.name == name)
                    : _metadata.segments.FindAll(s => s.name == name && s.version == version);

                _metadata.segments.RemoveAll(s => deleted.Contains(s));

                if (_nameIndex.TryGetValue(name, out var segments))
                {
                    segments.RemoveAll(s => deleted.Contains(s));
                    if (segments.Count == 0)
                        _nameIndex.Remove(name);
                }

                foreach (var seg in deleted)
                {
                    _metadata.fileCount--;
                    _metadata.totalSize -= seg.length;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            SaveMetadata();
        }

        private void RebuildIndex()
        {
            _lock.EnterWriteLock();
            try
            {
                _nameIndex = new Dictionary<string, List<VFSegment>>();
                foreach (var seg in _metadata.segments)
                {
                    if (!_nameIndex.ContainsKey(seg.name))
                        _nameIndex[seg.name] = new List<VFSegment>();
                    _nameIndex[seg.name].Add(seg);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private void SaveMetadata()
        {
            _lock.EnterReadLock();
            string json;
            try
            {
                json = JsonUtility.ToJson(_metadata);
            }
            finally
            {
                _lock.ExitReadLock();
            }

            var tempPath = _metadataPath + ".tmp";
            try
            {
                File.WriteAllText(tempPath, json);
                if (File.Exists(_metadataPath))
                    File.Delete(_metadataPath);
                File.Move(tempPath, _metadataPath);
            }
            catch
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                throw;
            }
        }

        private void EnsureDirectory(string dir)
        {
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        /// <summary>
        /// 验证文件完整性：读取VFS文件计算hash，与传入的remoteHash对比
        /// </summary>
        /// <param name="name">文件名</param>
        /// <param name="version">版本</param>
        /// <param name="remoteHash">远端Manifest的hash，用于对比</param>
        /// <returns>hash是否匹配</returns>
        public async UniTask<bool> VerifyHashAsync(string name, string version, string remoteHash)
        {
            // 如果没有提供remoteHash，跳过校验
            if (string.IsNullOrEmpty(remoteHash))
            {
                return true;
            }

            VFSegment segment = null;
            _lock.EnterReadLock();
            try
            {
                if (_nameIndex.TryGetValue(name, out var segments))
                {
                    segment = string.IsNullOrEmpty(version)
                        ? segments[^1]
                        : segments.Find(s => s.version == version);
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            if (segment == null)
            {
                Game.Debug.Warning($"Segment not found: {name} v{version}");
                return false;
            }

            // 读取文件数据
            var bytes = await _storage.ReadAsync((int)segment.start, (int)segment.length);

            // 计算当前文件的hash
            var actualHash = ComputeHash(bytes);

            // 与传入的remoteHash对比
            var isValid = actualHash.Equals(remoteHash, StringComparison.OrdinalIgnoreCase);

            if (!isValid)
            {
                Game.Debug.Warning($"Hash mismatch for {name} v{version}. Expected: {remoteHash}, Actual: {actualHash}");
            }

            return isValid;
        }

        /// <summary>
        /// 计算数据的MD5 hash
        /// </summary>
        private string ComputeHash(byte[] data)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hash = md5.ComputeHash(data);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        public void Dispose()
        {
            _storage?.Dispose();
        }
    }
}
