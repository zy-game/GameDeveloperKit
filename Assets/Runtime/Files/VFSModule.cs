using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Log;
using UnityEngine;
using ZLinq;

namespace GameDeveloperKit.Files
{
    public interface IFileManager : IModule
    {
        UniTask<RawHandle> ReadHandleAsync(string name, CancellationToken cancellationToken = default);
        UniTask<RawHandle> WriteHandleAsync(string name, byte[] bytes, CancellationToken cancellationToken = default);
        UniTask<RawHandle> WriteHandleAsync(string name, string version, byte[] bytes, CancellationToken cancellationToken = default);
        UniTask Add(string localPath, string name, string version, CancellationToken cancellationToken = default, Action<float> onProgress = null);
        bool Exists(string name);
        bool Exists(string name, string version);
        UniTask<bool> VerifyHashAsync(string name, string version, string remoteHash);
        void UnloadHandle(RawHandle handle);
        void Delete(string name, string version = "");
    }

    public class VFSModule : IModule, IFileManager
    {
        private readonly List<VFSystem> _systems = new List<VFSystem>();
        private readonly LRUCache<string, RawHandle> _cachedHandles = new LRUCache<string, RawHandle>(100);
        private string _vfsRootPath;
        private string _vfsIndexPath;

        private const int MAX_SEGMENT_SIZE = 4 * 1024 * 1024;

        public void OnStartup()
        {
            _vfsRootPath = Path.Combine(Application.persistentDataPath, "vfs");
            _vfsIndexPath = Path.Combine(_vfsRootPath, "vfs.data");

            EnsureDirectory(_vfsRootPath);

            var systemIds = LoadSystemList();
            foreach (var systemId in systemIds)
            {
                var system = new VFSystem(systemId, _vfsRootPath);
                system.Initialize();
                _systems.Add(system);
            }
            
            // 注册调试面板
            if (Game.Debug is LoggerModule loggerModule)
            {
                loggerModule.RegisterPanel(new FileDebugPanel());
            }
        }

        public void OnUpdate(float elapseSeconds)
        {
        }

        public void OnClearup()
        {
            _cachedHandles.Clear();
            foreach (var system in _systems)
                system?.Dispose();
            _systems.Clear();
        }

        public async UniTask<RawHandle> ReadHandleAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            if (_cachedHandles.TryGet(name, out var cached))
            {
                cached.Retain();
                return cached;
            }

            foreach (var system in _systems)
            {
                if (system.Contains(name))
                {
                    var handle = await system.ReadAsync(name, "", cancellationToken);
                    if (handle != null)
                    {
                        CacheHandle(name, handle);
                        return handle;
                    }
                }
            }

            return null;
        }

        public async UniTask<RawHandle> WriteHandleAsync(
            string name,
            byte[] bytes,
            CancellationToken cancellationToken = default)
        {
            return await WriteHandleAsync(name, "0.0.0", bytes, cancellationToken);
        }

        public async UniTask<RawHandle> WriteHandleAsync(
            string name,
            string version,
            byte[] bytes,
            CancellationToken cancellationToken = default)
        {
            Throw.Asserts(!string.IsNullOrEmpty(name), "文件名不能为空");
            Throw.Asserts(!string.IsNullOrEmpty(version), "版本号不能为空");
            Throw.Asserts(bytes is { Length: > 0 }, "bytes 不能为空");

            Delete(name);
            RemoveCachedHandle(name);

            VFSystem targetSystem = null;
            foreach (var system in _systems)
            {
                if (system.CanWriteable(bytes.Length))
                {
                    targetSystem = system;
                    break;
                }
            }

            if (targetSystem == null)
            {
                targetSystem = CreateNewSystem(bytes.Length);
                _systems.Add(targetSystem);
                SaveSystemList();
            }

            var handle = await targetSystem.WriteAsync(name, version, bytes, cancellationToken);
            if (handle != null)
            {
                CacheHandle(name, handle);
            }

            return handle;
        }

        public async UniTask Add(
            string localPath,
            string name,
            string version,
            CancellationToken cancellationToken = default,
            Action<float> onProgress = null)
        {
            Throw.Asserts(!string.IsNullOrEmpty(localPath), "文件路径不能为空");
            Throw.Asserts(!string.IsNullOrEmpty(name), "文件名不能为空");
            Throw.Asserts(!string.IsNullOrEmpty(version), "版本号不能为空");
            Throw.Asserts(File.Exists(localPath), new FileNotFoundException($"文件不存在: {localPath}", localPath));

            Delete(name);
            RemoveCachedHandle(name);

            var fileInfo = new FileInfo(localPath);

            VFSystem targetSystem = null;
            foreach (var system in _systems)
            {
                if (system.CanWriteable((int)fileInfo.Length))
                {
                    targetSystem = system;
                    break;
                }
            }

            if (targetSystem == null)
            {
                targetSystem = CreateNewSystem((int)fileInfo.Length);
                _systems.Add(targetSystem);
                SaveSystemList();
            }

            await targetSystem.AddFromFileAsync(localPath, name, version, cancellationToken, onProgress);

            try
            {
                File.Delete(localPath);
            }
            catch
            {
                // ignored
            }
        }

        public bool Exists(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            foreach (var system in _systems)
            {
                if (system.Contains(name))
                    return true;
            }

            return false;
        }

        public bool Exists(string name, string version)
        {
            foreach (var system in _systems)
            {
                if (system.Contains(name, version))
                    return true;
            }

            return false;
        }

        public async UniTask<bool> VerifyHashAsync(string name, string version, string remoteHash)
        {
            foreach (var system in _systems)
            {
                if (system.Contains(name, version))
                {
                    return await system.VerifyHashAsync(name, version, remoteHash);
                }
            }

            Game.Debug.Warning($"File not found in VFS: {name} v{version}");
            return false;
        }

        public void UnloadHandle(RawHandle handle)
        {
            if (handle == null)
                return;

            handle.Release();
            if (handle.ReferenceCount > 0)
                return;

            if (_cachedHandles.TryGet(handle.Name, out var cached) && cached == handle)
            {
                _cachedHandles.Remove(handle.Name);
            }

            handle.OnClearup();
        }

        public void Delete(string name, string version = "")
        {
            if (string.IsNullOrEmpty(name))
                return;

            foreach (var system in _systems)
            {
                if (system.Contains(name, version))
                {
                    system.Delete(name, version);
                }
            }

            RemoveCachedHandle(name);
        }

        private VFSystem CreateNewSystem(int dataSize)
        {
            var systemId = Guid.NewGuid().ToString("N");
            var system = new VFSystem(systemId, _vfsRootPath);
            system.Initialize();
            return system;
        }

        private void SaveSystemList()
        {
            var systemIds = new List<string>(_systems.Count);
            foreach (var system in _systems.AsValueEnumerable())
            {
                systemIds.Add(system.SystemId);
            }
            var data = new SystemListData { systems = systemIds };
            var json = JsonUtility.ToJson(data);

            var tempPath = _vfsIndexPath + ".tmp";
            try
            {
                File.WriteAllText(tempPath, json);
                if (File.Exists(_vfsIndexPath))
                    File.Delete(_vfsIndexPath);
                File.Move(tempPath, _vfsIndexPath);
            }
            catch
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                throw;
            }
        }

        private List<string> LoadSystemList()
        {
            if (!File.Exists(_vfsIndexPath))
                return new List<string>();

            var json = File.ReadAllText(_vfsIndexPath);
            var data = JsonUtility.FromJson<SystemListData>(json);
            return data.systems ?? new List<string>();
        }

        private void CacheHandle(string name, RawHandle handle)
        {
            if (handle == null)
                return;
            
            handle.Retain();
            var evicted = _cachedHandles.Put(name, handle);
            if (evicted != null)
            {
                evicted.OnClearup();
            }
        }

        private void RemoveCachedHandle(string name)
        {
            if (_cachedHandles.TryGet(name, out var old))
            {
                old.OnClearup();
                _cachedHandles.Remove(name);
            }
        }

        private void EnsureDirectory(string dir)
        {
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}