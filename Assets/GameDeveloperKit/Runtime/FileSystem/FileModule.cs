using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.File
{
    /// <summary>
    /// 文件模块，基于虚拟文件系统清单管理持久化文件的写入、读取和删除。
    /// </summary>
    public class FileModule : GameModuleBase
    {
        private string m_RootPath;
        private readonly string m_RootPathOverride;
        private VfsManifest m_Manifest;
        private List<VFSteaming> m_Steamings = new List<VFSteaming>();

        public FileModule()
        {
        }

        internal FileModule(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("Root path cannot be empty.", nameof(rootPath));
            }

            m_RootPathOverride = rootPath;
        }

        internal string RootPath => m_RootPath;

        /// <summary>
        /// 启动文件模块，初始化虚拟文件系统根目录并加载清单。
        /// </summary>
        /// <returns>模块启动任务。</returns>
        public override async UniTask Startup()
        {
            m_RootPath = string.IsNullOrEmpty(m_RootPathOverride)
                ? Path.Combine(Application.persistentDataPath, "vfs")
                : m_RootPathOverride;

            if (!Directory.Exists(m_RootPath))
            {
                Directory.CreateDirectory(m_RootPath);
            }

            m_Manifest = await VfsManifest.LoadAsync(m_RootPath);
        }

        /// <summary>
        /// 关闭文件模块，保存清单并释放已打开的虚拟文件流。
        /// </summary>
        /// <returns>模块关闭任务。</returns>
        public override async UniTask Shutdown()
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

        public async UniTask<string> ReadAllStringAsync(string path)
        {
            return string.Empty;
        }

        public async UniTask MoveToAsync(string sourceFileName, string destFileName)
        {
            System.IO.File.Move(sourceFileName, destFileName);
        }

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

            var releasedBundlePath = await this.m_Manifest.Release(path);
            await DeleteBundleIfUnusedAsync(releasedBundlePath);
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

        /// <summary>
        /// 读取虚拟文件数据。
        /// </summary>
        /// <param name="path">虚拟文件路径。</param>
        /// <returns>文件数据；如果文件不存在或未启用，则返回null。</returns>
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

        /// <summary>
        /// 检查虚拟文件是否存在，并可选校验版本号。
        /// </summary>
        /// <param name="path">虚拟文件路径。</param>
        /// <param name="version">期望版本；为空时不校验版本。</param>
        /// <returns>如果文件存在且版本匹配，则返回true；否则返回false。</returns>
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

        /// <summary>
        /// 删除虚拟文件，并将对应清单条目标记为空闲。
        /// </summary>
        /// <param name="path">虚拟文件路径。</param>
        /// <returns>删除任务。</returns>
        public async UniTask DeleteAsync(string path)
        {
            if (!m_Manifest.TryGetEntry(path, out var entry))
            {
                return;
            }

            var releasedBundlePath = entry.Unused();
            await m_Manifest.SaveAsync();
            await DeleteBundleIfUnusedAsync(releasedBundlePath);
        }

        /// <summary>
        /// 尝试获取虚拟文件的元数据信息。
        /// </summary>
        /// <param name="path">虚拟文件路径。</param>
        /// <param name="entry">输出文件元数据。</param>
        /// <returns>如果找到清单条目，则返回true；否则返回false。</returns>
        public bool TryGetFileInfo(string path, out VFSMeta entry)
        {
            return m_Manifest.TryGetEntry(path, out entry);
        }

        /// <summary>
        /// 获取所有正在使用的虚拟文件元数据。
        /// </summary>
        /// <returns>正在使用的虚拟文件元数据集合。</returns>
        public IEnumerable<VFSMeta> ListFiles()
        {
            return m_Manifest.GetAllEntries().Where(e => e.Usegd);
        }

        private async UniTask DeleteBundleIfUnusedAsync(string bundlePath)
        {
            if (string.IsNullOrEmpty(bundlePath) || m_Manifest.HasUsedBundle(bundlePath))
            {
                return;
            }

            m_Manifest.ClearUnusedBundleEntries(bundlePath);
            await m_Manifest.SaveAsync();
            var fullPath = Path.Combine(m_RootPath, bundlePath);
            var steaming = m_Steamings.Find(s => s.Path == fullPath);
            if (steaming != null)
            {
                steaming.Dispose();
                m_Steamings.Remove(steaming);
            }

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }
    }
}
