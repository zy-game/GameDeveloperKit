using System;
using System.Collections.Generic;
using System.IO;
using IOFile = System.IO.File;

namespace GameDeveloperKit.ChannelBuild
{
    internal sealed class ChannelBuildFileMutationState
    {
        private const string BackupRoot = "Library/GameDeveloperKit/ChannelBuild/FileMutationState";

        private readonly List<FileState> m_Files = new List<FileState>();
        private readonly string m_BackupDirectory;
        private bool m_Captured;

        internal ChannelBuildFileMutationState(IReadOnlyList<string> targetPaths)
        {
            if (targetPaths == null)
            {
                throw new ArgumentNullException(nameof(targetPaths));
            }

            m_BackupDirectory = Path.GetFullPath(
                Path.Combine(BackupRoot, Guid.NewGuid().ToString("N")));
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < targetPaths.Count; i++)
            {
                AddFile(targetPaths[i], uniquePaths);
                AddFile(targetPaths[i] + ".meta", uniquePaths);
            }
        }

        internal void Capture()
        {
            if (m_Captured)
            {
                throw new InvalidOperationException("Packaged resource state is already captured.");
            }

            m_Captured = true;
            Directory.CreateDirectory(m_BackupDirectory);
            for (var i = 0; i < m_Files.Count; i++)
            {
                var file = m_Files[i];
                if (Directory.Exists(file.TargetPath))
                {
                    throw new InvalidOperationException("Packaged resource target must be a file.");
                }

                file.HadFile = IOFile.Exists(file.TargetPath);
                if (file.HadFile)
                {
                    IOFile.Copy(file.TargetPath, file.BackupPath, false);
                }
                file.Captured = true;
            }
        }

        internal int Restore()
        {
            if (m_Captured is false)
            {
                return 0;
            }

            var failureCount = 0;
            for (var i = m_Files.Count - 1; i >= 0; i--)
            {
                var file = m_Files[i];
                if (file.Captured is false)
                {
                    continue;
                }
                try
                {
                    if (file.HadFile)
                    {
                        if (IOFile.Exists(file.BackupPath) is false)
                        {
                            throw new FileNotFoundException("Packaged resource backup is missing.");
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(file.TargetPath) ?? ".");
                        IOFile.Copy(file.BackupPath, file.TargetPath, true);
                    }
                    else if (IOFile.Exists(file.TargetPath))
                    {
                        IOFile.Delete(file.TargetPath);
                    }
                }
                catch
                {
                    failureCount++;
                }
            }

            if (failureCount == 0)
            {
                try
                {
                    if (Directory.Exists(m_BackupDirectory))
                    {
                        Directory.Delete(m_BackupDirectory, true);
                    }
                    m_Captured = false;
                }
                catch
                {
                    failureCount++;
                }
            }

            return failureCount;
        }

        private void AddFile(string targetPath, ISet<string> uniquePaths)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                throw new ArgumentException("Packaged resource target cannot be empty.", nameof(targetPath));
            }

            var fullPath = Path.GetFullPath(targetPath);
            if (uniquePaths.Add(fullPath) is false)
            {
                throw new ArgumentException("Packaged resource target is duplicated.", nameof(targetPath));
            }

            var backupPath = Path.Combine(
                m_BackupDirectory,
                m_Files.Count.ToString("D4") + ".backup");
            m_Files.Add(new FileState(fullPath, backupPath));
        }

        private sealed class FileState
        {
            internal FileState(string targetPath, string backupPath)
            {
                TargetPath = targetPath;
                BackupPath = backupPath;
            }

            internal string TargetPath { get; }

            internal string BackupPath { get; }

            internal bool HadFile { get; set; }

            internal bool Captured { get; set; }
        }
    }
}
