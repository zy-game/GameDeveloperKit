using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

namespace GameDeveloperKit.File
{
    /// <summary>
    /// 虚拟文件系统清单，负责保存虚拟路径到包文件位置的映射关系。
    /// </summary>
    public class VfsManifest
    {
        private List<VFSMeta> m_Entries = new List<VFSMeta>();
        private string m_RootPath;

        /// <summary>
        /// 清单条目数量。
        /// </summary>
        public int FileCount => m_Entries.Count;

        /// <summary>
        /// 从指定根目录异步加载虚拟文件系统清单。
        /// </summary>
        /// <param name="rootPath">虚拟文件系统根目录。</param>
        /// <returns>加载后的虚拟文件系统清单。</returns>
        public static async UniTask<VfsManifest> LoadAsync(string rootPath)
        {
            var manifest = new VfsManifest { m_RootPath = rootPath };
            var manifestPath = Path.Combine(rootPath, VfsConstants.ManifestFileName);

            if (!System.IO.File.Exists(manifestPath))
            {
                return manifest;
            }

            var json = await System.IO.File.ReadAllTextAsync(manifestPath);
            var data = JsonConvert.DeserializeObject<List<VFSMeta>>(json);

            if (data != null)
            {
                manifest.m_Entries.AddRange(data);
            }

            await UniTask.SwitchToMainThread();
            return manifest;
        }

        /// <summary>
        /// 释放指定虚拟路径对应的清单条目。
        /// </summary>
        /// <param name="path">虚拟文件路径。</param>
        /// <returns>被释放条目原先所在的包路径；未找到时返回 null。</returns>
        /// <exception cref="ArgumentException">虚拟文件路径为空时抛出。</exception>
        public async UniTask<string> Release(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));
            }

            var entry = m_Entries.Find(e => e.FilePath == path);
            if (entry == null)
            {
                return null;
            }

            var bundlePath = entry.Unused();
            await SaveAsync();
            return bundlePath;
        }

        /// <summary>
        /// 判断指定包路径是否仍被有效清单条目引用。
        /// </summary>
        /// <param name="bundlePath">包路径。</param>
        /// <returns>仍有有效条目引用时返回 true；否则返回 false。</returns>
        public bool HasUsedBundle(string bundlePath)
        {
            if (string.IsNullOrEmpty(bundlePath))
            {
                return false;
            }

            foreach (var entry in m_Entries)
            {
                if (entry.Usegd && entry.BundlePath == bundlePath)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 清理指定包路径下所有空闲条目的包路径引用。
        /// </summary>
        /// <param name="bundlePath">包路径。</param>
        public void ClearUnusedBundleEntries(string bundlePath)
        {
            if (string.IsNullOrEmpty(bundlePath))
            {
                return;
            }

            foreach (var entry in m_Entries)
            {
                if (!entry.Usegd && entry.BundlePath == bundlePath)
                {
                    entry.ClearBundlePath();
                }
            }
        }

        /// <summary>
        /// 保存清单到磁盘。
        /// </summary>
        /// <returns>保存任务。</returns>
        public async UniTask SaveAsync()
        {
            var data = m_Entries;
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            var manifestPath = Path.Combine(m_RootPath, VfsConstants.ManifestFileName);
            await System.IO.File.WriteAllTextAsync(manifestPath, json);
            await UniTask.SwitchToMainThread();
        }

        /// <summary>
        /// 获取一个空闲清单条目；没有空闲条目时会创建新的包条目。
        /// </summary>
        /// <returns>可用的清单条目。</returns>
        public async UniTask<VFSMeta> GetUnused()
        {
            var unusedEntry = m_Entries.Find(e => !e.Usegd && e.Storage == StorageType.Packed && !string.IsNullOrEmpty(e.BundlePath));
            if (unusedEntry != null)
            {
                return unusedEntry;
            }
            await CreateBundle();
            return m_Entries.Find(e => !e.Usegd && e.Storage == StorageType.Packed && !string.IsNullOrEmpty(e.BundlePath));
        }

        /// <summary>
        /// 创建独立文件清单条目。
        /// </summary>
        /// <returns>可用的独立文件清单条目。</returns>
        public VFSMeta CreateStandalone()
        {
            var entry = new VFSMeta
            {
                FilePath = string.Empty,
                Storage = StorageType.Standalone,
                Offset = 0,
                Crc32 = 0,
                Version = string.Empty,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Usegd = false,
                BundlePath = Guid.NewGuid().ToString("N")
            };
            m_Entries.Add(entry);
            return entry;
        }

        /// <summary>
        /// 尝试获取指定虚拟路径对应的清单条目。
        /// </summary>
        /// <param name="virtualPath">虚拟文件路径。</param>
        /// <param name="entry">输出清单条目。</param>
        /// <returns>如果找到清单条目，则返回true；否则返回false。</returns>
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

        /// <summary>
        /// 创建新的虚拟文件包清单条目。
        /// </summary>
        /// <returns>创建并保存清单的任务。</returns>
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

        /// <summary>
        /// 获取所有清单条目。
        /// </summary>
        /// <returns>所有虚拟文件元数据条目。</returns>
        public IEnumerable<VFSMeta> GetAllEntries()
        {
            return m_Entries;
        }
    }
}
