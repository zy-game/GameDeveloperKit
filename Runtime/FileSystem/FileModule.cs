using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.File
{
    /// <summary>
    /// 文件模块，基于虚拟文件系统清单管理持久化文件的写入、读取和删除。
    /// </summary>
    public partial class FileModule : GameModuleBase, IAsyncShutdownParticipant
    {
        private string m_RootPath;
        private readonly string m_RootPathOverride;
        private VfsManifest m_Manifest;
        private List<VFSteaming> m_Steamings = new List<VFSteaming>();
        private readonly List<VfsReadStream> m_ReadStreams = new List<VfsReadStream>();
        private readonly List<FileTemporaryHandle> m_TemporaryFiles = new List<FileTemporaryHandle>();
        private readonly SemaphoreSlim m_MutationGate = new SemaphoreSlim(1, 1);
        private string m_TemporaryRoot;
        private int m_ActiveOperations;
        private bool m_IsPreparingShutdown;
        private bool m_TeardownPrepared;
        private UniTaskCompletionSource m_OperationDrainCompletion;
        private UniTaskCompletionSource m_PrepareCompletion;

        /// <summary>
        /// 初始化 File Module。
        /// </summary>
        public FileModule()
        {
        }

        /// <summary>
        /// 初始化 File Module。
        /// </summary>
        /// <param name="rootPath">root Path 参数。</param>
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
        public override void Startup()
        {
            m_RootPath = string.IsNullOrEmpty(m_RootPathOverride)
                ? Path.Combine(Application.persistentDataPath, "vfs")
                : m_RootPathOverride;

            if (!Directory.Exists(m_RootPath))
            {
                Directory.CreateDirectory(m_RootPath);
            }

            m_Manifest = VfsManifest.Load(m_RootPath);
            CleanupUnreferencedBundleFiles();
            InitializeTemporaryFiles();
            m_ReadStreams.Clear();
            m_ActiveOperations = 0;
            m_IsPreparingShutdown = false;
            m_TeardownPrepared = false;
            m_OperationDrainCompletion = null;
            m_PrepareCompletion = null;
        }

        /// <summary>
        /// 关闭文件模块并释放已打开的虚拟文件流。
        /// </summary>
        public override void Shutdown()
        {
            if (m_ActiveOperations > 0 && !m_TeardownPrepared)
            {
                throw new GameException(
                    "FileModule has active I/O. Use App.Unregister<FileModule>() or App.Shutdown() so asynchronous teardown can complete.");
            }

            CloseReadStreams();
            foreach (var steaming in m_Steamings)
            {
                steaming.Dispose();
            }

            m_Steamings.Clear();
            m_TemporaryFiles.Clear();
            m_Manifest = null;
            m_TemporaryRoot = null;
            m_IsPreparingShutdown = false;
            m_TeardownPrepared = false;
            m_OperationDrainCompletion = null;
            m_PrepareCompletion = null;
        }

        /// <summary>
        /// 读取虚拟文件数据并解码为字符串。
        /// </summary>
        /// <param name="path">虚拟文件路径。</param>
        /// <returns>文件内容；如果文件不存在或未启用，则返回null。</returns>
        public async UniTask<string> ReadAllStringAsync(string path)
        {
            var data = await ReadAsync(path);
            return data == null ? null : Encoding.UTF8.GetString(data);
        }

        /// <summary>
        /// 将文件从源路径移动到目标路径，更新虚拟文件系统清单中的对应条目。
        /// </summary>
        /// <param name="sourcePath">源虚拟路径。</param>
        /// <param name="destinationPath">目标虚拟路径。</param>
        /// <returns>移动任务。</returns>
        public async UniTask MoveToAsync(string sourcePath, string destinationPath)
        {
            BeginOperation();
            try
            {
                ValidateVirtualPath(sourcePath, nameof(sourcePath));
                ValidateVirtualPath(destinationPath, nameof(destinationPath));
                EnsureReady();
                await m_MutationGate.WaitAsync();
                try
                {
                    EnsureReady();
                    var candidate = m_Manifest.Clone();
                    candidate.Rename(sourcePath, destinationPath);
                    await candidate.SaveAtomicAsync();
                    m_Manifest = candidate;
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

        /// <summary>
        /// 检查虚拟文件是否存在，并可选校验版本号。
        /// </summary>
        /// <param name="path">虚拟文件路径。</param>
        /// <param name="version">期望版本；为空时不校验版本。</param>
        /// <returns>如果文件存在且版本匹配，则返回true；否则返回false。</returns>
        public bool Exists(string path, string version = "")
        {
            ThrowIfPreparingShutdown();
            EnsureReady();
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
            BeginOperation();
            try
            {
                ValidateVirtualPath(path, nameof(path));
                EnsureReady();
                await m_MutationGate.WaitAsync();
                try
                {
                    EnsureReady();
                    if (!m_Manifest.TryGetEntry(path, out var currentEntry) || !currentEntry.Usegd)
                    {
                        return;
                    }

                    var candidate = m_Manifest.Clone();
                    candidate.TryGetEntry(path, out var candidateEntry);
                    var releasedBundlePath = candidate.ReleaseEntry(candidateEntry);
                    await candidate.SaveAtomicAsync();
                    m_Manifest = candidate;
                    await DeleteBundleAfterCommitAsync(releasedBundlePath, $"delete '{path}'");
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

        async UniTask IAsyncShutdownParticipant.PrepareShutdownAsync()
        {
            if (m_PrepareCompletion != null)
            {
                await m_PrepareCompletion.Task;
                return;
            }

            var prepareCompletion = new UniTaskCompletionSource();
            m_PrepareCompletion = prepareCompletion;
            m_IsPreparingShutdown = true;
            try
            {
                CloseReadStreams();
                await ReleaseTemporaryFilesAsync();
                if (m_ActiveOperations > 0)
                {
                    m_OperationDrainCompletion = new UniTaskCompletionSource();
                    if (m_ActiveOperations > 0)
                    {
                        await m_OperationDrainCompletion.Task;
                    }
                }

                m_OperationDrainCompletion = null;
                m_TeardownPrepared = true;
                prepareCompletion.TrySetResult();
            }
            catch (Exception exception)
            {
                m_OperationDrainCompletion = null;
                m_PrepareCompletion = null;
                m_IsPreparingShutdown = false;
                prepareCompletion.TrySetException(exception);
                await prepareCompletion.Task;
                throw;
            }
        }

        /// <summary>
        /// 尝试获取虚拟文件的元数据信息。
        /// </summary>
        /// <param name="path">虚拟文件路径。</param>
        /// <param name="entry">输出文件元数据。</param>
        /// <returns>如果找到清单条目，则返回true；否则返回false。</returns>
        public bool TryGetFileInfo(string path, out VFSMeta entry)
        {
            ThrowIfPreparingShutdown();
            EnsureReady();
            if (m_Manifest.TryGetEntry(path, out var currentEntry))
            {
                entry = currentEntry.Clone();
                return true;
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// 获取所有正在使用的虚拟文件元数据。
        /// </summary>
        /// <returns>正在使用的虚拟文件元数据集合。</returns>
        public IEnumerable<VFSMeta> ListFiles()
        {
            ThrowIfPreparingShutdown();
            EnsureReady();
            var entries = new List<VFSMeta>();
            foreach (var entry in m_Manifest.GetAllEntries())
            {
                if (entry.Usegd)
                {
                    entries.Add(entry.Clone());
                }
            }

            return entries;
        }

        private async UniTask DeleteBundleAfterCommitAsync(string bundlePath, string operation)
        {
            if (string.IsNullOrEmpty(bundlePath) || m_Manifest.HasUsedBundle(bundlePath))
            {
                return;
            }

            try
            {
                await RemoveBundleFileAsync(bundlePath);
            }
            catch (Exception exception)
            {
                throw new GameException(
                    $"VFS {operation} was committed, but obsolete bundle '{bundlePath}' cleanup failed. Cleanup will be retried on the next FileModule startup.",
                    exception);
            }
        }

        private VFSteaming GetOrCreateSteaming(string bundlePath)
        {
            var fullPath = Path.Combine(m_RootPath, bundlePath);
            var steaming = m_Steamings.Find(candidate => candidate.Path == fullPath);
            if (steaming != null)
            {
                return steaming;
            }

            steaming = new VFSteaming(fullPath);
            m_Steamings.Add(steaming);
            return steaming;
        }

        private async UniTask CleanupUncommittedBundleAsync(string bundlePath)
        {
            if (string.IsNullOrEmpty(bundlePath) || m_Manifest.ContainsBundle(bundlePath))
            {
                return;
            }

            await RemoveBundleFileAsync(bundlePath);
        }

        private async UniTask RemoveBundleFileAsync(string bundlePath)
        {
            var fullPath = Path.Combine(m_RootPath, bundlePath);
            var steaming = m_Steamings.Find(candidate => candidate.Path == fullPath);
            if (steaming != null)
            {
                await steaming.DisposeAsync();
                m_Steamings.Remove(steaming);
            }

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        /// <summary>
        /// 确保文件模块已启动。
        /// </summary>
        private void EnsureReady()
        {
            if (m_Manifest == null)
            {
                throw new GameException("FileModule is not started.");
            }
        }

        private void BeginOperation()
        {
            ThrowIfPreparingShutdown();

            m_ActiveOperations++;
        }

        private void EndOperation()
        {
            m_ActiveOperations--;
            if (m_ActiveOperations == 0)
            {
                m_OperationDrainCompletion?.TrySetResult();
            }
        }

        private void ThrowIfPreparingShutdown()
        {
            if (m_IsPreparingShutdown)
            {
                throw new GameException("FileModule is shutting down and cannot accept new I/O.");
            }
        }

        private void CleanupUnreferencedBundleFiles()
        {
            foreach (var filePath in Directory.GetFiles(m_RootPath))
            {
                var fileName = Path.GetFileName(filePath);
                if (fileName == VfsConstants.ManifestFileName || m_Manifest.ContainsBundle(fileName))
                {
                    continue;
                }

                System.IO.File.Delete(filePath);
            }
        }

        private static void ValidateVirtualPath(string path, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("VFS path cannot be empty.", parameterName);
            }
        }
    }
}
