using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

namespace GameDeveloperKit
{
    public class VfsManifest
    {
        private List<VFSMeta> m_Entries = new List<VFSMeta>();
        private string m_RootPath;

        public int FileCount => m_Entries.Count;

        public static async UniTask<VfsManifest> LoadAsync(string rootPath)
        {
            var manifest = new VfsManifest { m_RootPath = rootPath };
            var manifestPath = Path.Combine(rootPath, VfsConstants.ManifestFileName);

            if (!File.Exists(manifestPath))
            {
                return manifest;
            }

            var json = await File.ReadAllTextAsync(manifestPath);
            var data = JsonConvert.DeserializeObject<List<VFSMeta>>(json);

            if (data != null)
            {
                manifest.m_Entries.AddRange(data);
            }

            await UniTask.SwitchToMainThread();
            return manifest;
        }

        public UniTask Release(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));
            }

            var entry = m_Entries.Find(e => e.FilePath == path);
            if (entry == null)
            {
                return UniTask.CompletedTask;
            }

            entry.Unused();
            return SaveAsync();
        }

        public async UniTask SaveAsync()
        {
            var data = m_Entries;
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            var manifestPath = Path.Combine(m_RootPath, VfsConstants.ManifestFileName);
            await File.WriteAllTextAsync(manifestPath, json);
            await UniTask.SwitchToMainThread();
        }

        public async UniTask<VFSMeta> GetUnused()
        {
            var unusedEntry = m_Entries.Find(e => !e.Usegd);
            if (unusedEntry != null)
            {
                return unusedEntry;
            }
            await CreateBundle();
            return m_Entries.Find(e => !e.Usegd);
        }

        public bool TryGetEntry(string virtualPath, out VFSMeta entry)
        {
            foreach (var e in m_Entries)
            {
                if (e.FilePath == virtualPath)
                {
                    entry = e;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        public UniTask CreateBundle()
        {
            string bundleName = Guid.NewGuid().ToString("N");
            for (int i = 0; i < VfsConstants.BundleFileCount; i++)
            {
                var entry = new VFSMeta
                {
                    FilePath = string.Empty,
                    Storage = StorageType.Packed,
                    Offset = i * VfsConstants.DefaultThreshold,
                    Crc32 = 0,
                    Version = string.Empty,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Usegd = false,
                    BundlePath = bundleName
                };
                m_Entries.Add(entry);
            }
            return SaveAsync();
        }

        public IEnumerable<VFSMeta> GetAllEntries()
        {
            return m_Entries;
        }
    }
}
