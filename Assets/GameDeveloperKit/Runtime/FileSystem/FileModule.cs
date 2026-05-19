using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit
{
    public class FileModule : IGameModule
    {
        private string m_RootPath;
        private VfsManifest m_Manifest;
        private List<VFSteaming> m_Steamings = new List<VFSteaming>();

        public async UniTask Startup()
        {
            m_RootPath = Path.Combine(Application.persistentDataPath, "vfs");
            if (!Directory.Exists(m_RootPath))
            {
                Directory.CreateDirectory(m_RootPath);
            }

            m_Manifest = await VfsManifest.LoadAsync(m_RootPath);
        }

        public async UniTask Shutdown()
        {
            if (m_Manifest != null)
            {
                await m_Manifest.SaveAsync();
            }

            foreach (var steaming in m_Steamings)
            {
                steaming.Dispose();
            }
            m_Steamings.Clear();
        }

        public async UniTask WriteAsync(string path, string version, byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            await this.m_Manifest.Release(path);
            var crc32 = Crc32Utility.Compute(data);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            VFSMeta entry = await this.m_Manifest.GetUnused();
            if (entry == null)
            {
                throw new GameException("No unused entry available in the manifest.");
            }
            var storageType = data.Length > VfsConstants.DefaultThreshold ? StorageType.Packed : StorageType.Standalone;
            entry.Used(path, data.Length, crc32, version, timestamp, storageType);
            var bundlePath = Path.Combine(this.m_RootPath, entry.BundlePath);
            var steaming = m_Steamings.Find(s => s.Path == bundlePath);
            if (steaming == null)
            {
                steaming = new VFSteaming(bundlePath);
                m_Steamings.Add(steaming);
            }
            await steaming.WriteAsync(entry.Offset, data);
            await m_Manifest.SaveAsync();
        }

        public async UniTask<byte[]> ReadAsync(string path)
        {
            if (!m_Manifest.TryGetEntry(path, out var entry) || !entry.Usegd)
            {
                return null;
            }

            var steaming = m_Steamings.Find(s => s.Path == Path.Combine(this.m_RootPath, entry.BundlePath));
            if (steaming == null)
            {
                steaming = new VFSteaming(Path.Combine(this.m_RootPath, entry.BundlePath));
                m_Steamings.Add(steaming);
            }

            return await steaming.ReadAsync(entry.Offset, (int)entry.Size);
        }

        public bool Exists(string path, string version = "")
        {
            if (!m_Manifest.TryGetEntry(path, out var entry) || !entry.Usegd)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(version) && entry.Version != version)
            {
                return false;
            }

            return true;
        }

        public async UniTask DeleteAsync(string path)
        {
            if (!m_Manifest.TryGetEntry(path, out var entry))
            {
                return;
            }

            entry.Unused();
            await m_Manifest.SaveAsync();
        }

        public bool TryGetFileInfo(string path, out VFSMeta entry)
        {
            return m_Manifest.TryGetEntry(path, out entry);
        }

        public IEnumerable<VFSMeta> ListFiles()
        {
            return m_Manifest.GetAllEntries().Where(e => e.Usegd);
        }

        public void Release()
        {
            m_Manifest = null;
        }
    }
}
