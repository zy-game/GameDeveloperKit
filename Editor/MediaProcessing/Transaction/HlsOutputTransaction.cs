using System;
using System.IO;
using UnityEngine;

namespace GameDeveloperKit.MediaEditor
{
    internal interface IHlsDirectoryOperations
    {
        bool Exists(string path);
        void Create(string path);
        void Move(string source, string destination);
        void Delete(string path);
    }

    internal sealed class HlsOutputTransaction : IDisposable
    {
        public const string JobsRelativePath = "Library/GameDeveloperKit/MediaProcessing/Jobs";

        private readonly string m_TargetDirectory;
        private readonly string m_WorkingDirectory;
        private readonly string m_BackupDirectory;
        private readonly IHlsDirectoryOperations m_Directories;
        private bool m_Committed;
        private bool m_Disposed;
        private bool m_PreserveWorkingDirectory;

        public HlsOutputTransaction(
            string projectRoot,
            string targetDirectory,
            bool overwriteExisting)
            : this(
                targetDirectory,
                Path.Combine(
                    Path.GetFullPath(projectRoot ?? string.Empty),
                    JobsRelativePath,
                    Guid.NewGuid().ToString("N")),
                overwriteExisting,
                new HlsDirectoryOperations())
        {
        }

        internal HlsOutputTransaction(
            string targetDirectory,
            string workingDirectory,
            bool overwriteExisting,
            IHlsDirectoryOperations directories)
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                throw new ArgumentException("Target directory cannot be empty.", nameof(targetDirectory));
            }

            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                throw new ArgumentException("Working directory cannot be empty.", nameof(workingDirectory));
            }

            m_TargetDirectory = Path.GetFullPath(targetDirectory);
            m_WorkingDirectory = Path.GetFullPath(workingDirectory);
            m_BackupDirectory = Path.Combine(m_WorkingDirectory, "backup");
            m_Directories = directories ?? throw new ArgumentNullException(nameof(directories));
            if (m_Directories.Exists(m_TargetDirectory) && overwriteExisting is false)
            {
                throw new IOException($"HLS 输出目录已存在：{m_TargetDirectory}");
            }

            StagingDirectory = Path.Combine(m_WorkingDirectory, "package");
            m_Directories.Create(StagingDirectory);
        }

        public string StagingDirectory { get; }

        public void PrepareRenditionDirectories(HlsTranscodePlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            for (var i = 0; i < plan.Renditions.Count; i++)
            {
                m_Directories.Create(Path.Combine(StagingDirectory, plan.Renditions[i].Label));
            }
        }

        public void Commit()
        {
            if (m_Committed)
            {
                throw new InvalidOperationException("HLS output transaction was already committed.");
            }

            m_Directories.Create(Path.GetDirectoryName(m_TargetDirectory) ?? ".");
            var hadPrevious = m_Directories.Exists(m_TargetDirectory);
            if (hadPrevious)
            {
                m_Directories.Move(m_TargetDirectory, m_BackupDirectory);
            }

            try
            {
                m_Directories.Move(StagingDirectory, m_TargetDirectory);
            }
            catch
            {
                if (hadPrevious &&
                    m_Directories.Exists(m_BackupDirectory) &&
                    m_Directories.Exists(m_TargetDirectory) is false)
                {
                    try
                    {
                        m_Directories.Move(m_BackupDirectory, m_TargetDirectory);
                    }
                    catch
                    {
                        m_PreserveWorkingDirectory = true;
                        throw;
                    }
                }

                throw;
            }

            m_Committed = true;
            if (m_Directories.Exists(m_BackupDirectory))
            {
                try
                {
                    m_Directories.Delete(m_BackupDirectory);
                }
                catch (IOException exception)
                {
                    // The new package is already committed. Disposal retries cleanup.
                    Debug.LogWarning($"HLS 输出已提交，但备份清理将在释放时重试：{exception.Message}");
                }
                catch (UnauthorizedAccessException exception)
                {
                    // The new package is already committed. Disposal retries cleanup.
                    Debug.LogWarning($"HLS 输出已提交，但备份清理将在释放时重试：{exception.Message}");
                }
            }
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            if (m_PreserveWorkingDirectory is false && m_Directories.Exists(m_WorkingDirectory))
            {
                m_Directories.Delete(m_WorkingDirectory);
            }

            m_Disposed = true;
        }

        private sealed class HlsDirectoryOperations : IHlsDirectoryOperations
        {
            public bool Exists(string path)
            {
                return Directory.Exists(path);
            }

            public void Create(string path)
            {
                Directory.CreateDirectory(path);
            }

            public void Move(string source, string destination)
            {
                Directory.Move(source, destination);
            }

            public void Delete(string path)
            {
                Directory.Delete(path, true);
            }
        }
    }
}
