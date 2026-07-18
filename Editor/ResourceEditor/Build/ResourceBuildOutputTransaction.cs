using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using IOFile = System.IO.File;

namespace GameDeveloperKit.ResourceEditor
{
    internal sealed class ResourceBuildOutputTransaction : IDisposable
    {
        internal const string JournalPath = "Library/GameDeveloperKit/ResourceBuild/active-transaction.json";
        private const string PreparedState = "prepared";
        private const string CommittedState = "committed";
        private const string StagingSuffix = ".gdk-staging";
        private const string BackupSuffix = ".gdk-backup";

        private readonly List<OutputEntry> m_Entries = new List<OutputEntry>();
        private bool m_Committed;
        private bool m_RecoveryRequired;

        private ResourceBuildOutputTransaction()
        {
        }

        public static ResourceBuildOutputTransaction Begin()
        {
            RecoverPending();
            return new ResourceBuildOutputTransaction();
        }

        public static string GetDirectoryStagingPath(string targetPath)
        {
            return GetStagingPath(targetPath);
        }

        internal static string GetStagingPath(string targetPath)
        {
            return CreateSiblingPath(NormalizePath(targetPath), StagingSuffix);
        }

        internal static string GetBackupPath(string targetPath)
        {
            return CreateSiblingPath(NormalizePath(targetPath), BackupSuffix);
        }

        public string StageDirectory(string targetPath, bool preserveExisting)
        {
            var entry = GetOrAddEntry(targetPath, true);
            RecoverOrphan(entry);
            if (preserveExisting && Directory.Exists(entry.TargetPath))
            {
                CopyDirectory(entry.TargetPath, entry.StagingPath);
            }
            else
            {
                Directory.CreateDirectory(entry.StagingPath);
            }

            return entry.StagingPath;
        }

        public string StageFile(string targetPath)
        {
            var entry = GetOrAddEntry(targetPath, false);
            RecoverOrphan(entry);
            Directory.CreateDirectory(Path.GetDirectoryName(entry.StagingPath) ?? ".");
            return entry.StagingPath;
        }

        public string ResolveTargetPath(string stagedPath)
        {
            var normalizedPath = NormalizePath(stagedPath);
            foreach (var entry in m_Entries.OrderByDescending(candidate => candidate.StagingPath.Length))
            {
                if (entry.IsDirectory)
                {
                    var prefix = entry.StagingPath.TrimEnd('/') + "/";
                    if (normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.TargetPath.TrimEnd('/') + "/" + normalizedPath.Substring(prefix.Length);
                    }
                }
                else if (string.Equals(normalizedPath, entry.StagingPath, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.TargetPath;
                }
            }

            throw new InvalidOperationException($"Path is not owned by the resource build transaction: {stagedPath}");
        }

        public void Commit()
        {
            if (m_Committed)
            {
                throw new InvalidOperationException("Resource build output transaction is already committed.");
            }

            ValidateEntries();
            var journal = new TransactionJournal
            {
                State = PreparedState,
                Entries = m_Entries.Select(entry => entry.ToRecord()).ToList()
            };
            WriteJournal(journal);

            try
            {
                foreach (var entry in m_Entries)
                {
                    if (entry.HadTarget)
                    {
                        Move(entry.TargetPath, entry.BackupPath, entry.IsDirectory);
                    }

                    Move(entry.StagingPath, entry.TargetPath, entry.IsDirectory);
                }

                journal.State = CommittedState;
                WriteJournal(journal);
                m_Committed = true;
                CleanupCommitted(journal);
            }
            catch (Exception exception)
            {
                try
                {
                    RollbackPrepared(journal);
                    DeleteJournal();
                }
                catch (Exception rollbackException)
                {
                    m_RecoveryRequired = true;
                    throw new AggregateException(
                        "Resource build output commit failed and rollback could not complete. Recovery journal was preserved.",
                        exception,
                        rollbackException);
                }

                throw;
            }
        }

        public void Dispose()
        {
            if (m_Committed || m_RecoveryRequired)
            {
                return;
            }

            foreach (var entry in m_Entries)
            {
                Delete(entry.StagingPath, entry.IsDirectory);
            }
        }

        internal static void RecoverPending()
        {
            var journalPath = Path.GetFullPath(JournalPath);
            if (IOFile.Exists(journalPath) is false)
            {
                return;
            }

            TransactionJournal journal;
            try
            {
                journal = JsonConvert.DeserializeObject<TransactionJournal>(IOFile.ReadAllText(journalPath));
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Resource build recovery journal is invalid: {journalPath}", exception);
            }

            ValidateJournal(journal, journalPath);
            if (string.Equals(journal.State, PreparedState, StringComparison.Ordinal))
            {
                RollbackPrepared(journal);
            }
            else if (string.Equals(journal.State, CommittedState, StringComparison.Ordinal))
            {
                CompleteCommitted(journal);
            }
            else
            {
                throw new InvalidOperationException($"Resource build recovery journal has unknown state: {journal.State}");
            }

            DeleteJournal();
        }

        private OutputEntry GetOrAddEntry(string targetPath, bool isDirectory)
        {
            var normalizedTarget = NormalizePath(targetPath);
            if (normalizedTarget.EndsWith(StagingSuffix, StringComparison.OrdinalIgnoreCase) ||
                normalizedTarget.EndsWith(BackupSuffix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Resource build target uses a reserved transaction suffix: {normalizedTarget}");
            }

            var existing = m_Entries.FirstOrDefault(entry =>
                string.Equals(entry.TargetPath, normalizedTarget, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                if (existing.IsDirectory != isDirectory)
                {
                    throw new InvalidOperationException($"Resource build target kind changed within one transaction: {normalizedTarget}");
                }

                return existing;
            }

            var entry = new OutputEntry(normalizedTarget, isDirectory);
            var overlap = m_Entries.FirstOrDefault(candidate => TargetsOverlap(candidate, entry));
            if (overlap != null)
            {
                throw new InvalidOperationException(
                    $"Resource build transaction targets overlap: {overlap.TargetPath}, {entry.TargetPath}");
            }

            m_Entries.Add(entry);
            return entry;
        }

        private static void RecoverOrphan(OutputEntry entry)
        {
            if (Exists(entry.BackupPath, entry.IsDirectory))
            {
                if (Exists(entry.TargetPath, entry.IsDirectory))
                {
                    Delete(entry.BackupPath, entry.IsDirectory);
                }
                else
                {
                    Move(entry.BackupPath, entry.TargetPath, entry.IsDirectory);
                }
            }

            Delete(entry.StagingPath, entry.IsDirectory);
        }

        private void ValidateEntries()
        {
            if (m_Entries.Count == 0)
            {
                throw new InvalidOperationException("Resource build output transaction has no outputs.");
            }

            foreach (var entry in m_Entries)
            {
                entry.HadTarget = Exists(entry.TargetPath, entry.IsDirectory);
                if (Exists(entry.StagingPath, entry.IsDirectory) is false)
                {
                    throw new InvalidOperationException($"Resource build staging output is missing: {entry.StagingPath}");
                }

                if (Exists(entry.BackupPath, entry.IsDirectory))
                {
                    throw new InvalidOperationException($"Resource build backup path is occupied: {entry.BackupPath}");
                }

                if ((entry.IsDirectory && IOFile.Exists(entry.TargetPath)) ||
                    (entry.IsDirectory is false && Directory.Exists(entry.TargetPath)))
                {
                    throw new InvalidOperationException($"Resource build output has the wrong filesystem kind: {entry.TargetPath}");
                }
            }

            for (var i = 0; i < m_Entries.Count; i++)
            {
                for (var j = i + 1; j < m_Entries.Count; j++)
                {
                    if (TargetsOverlap(m_Entries[i], m_Entries[j]))
                    {
                        throw new InvalidOperationException(
                            $"Resource build transaction targets overlap: {m_Entries[i].TargetPath}, {m_Entries[j].TargetPath}");
                    }
                }
            }
        }

        private static void RollbackPrepared(TransactionJournal journal)
        {
            foreach (var entry in journal.Entries.AsEnumerable().Reverse())
            {
                if (entry.HadTarget)
                {
                    if (Exists(entry.BackupPath, entry.IsDirectory))
                    {
                        Delete(entry.TargetPath, entry.IsDirectory);
                        Move(entry.BackupPath, entry.TargetPath, entry.IsDirectory);
                    }
                }
                else if (Exists(entry.StagingPath, entry.IsDirectory) is false &&
                         Exists(entry.TargetPath, entry.IsDirectory))
                {
                    Delete(entry.TargetPath, entry.IsDirectory);
                }

                Delete(entry.StagingPath, entry.IsDirectory);
                Delete(entry.BackupPath, entry.IsDirectory);
            }
        }

        private static void CompleteCommitted(TransactionJournal journal)
        {
            foreach (var entry in journal.Entries)
            {
                if (Exists(entry.TargetPath, entry.IsDirectory) is false)
                {
                    if (Exists(entry.StagingPath, entry.IsDirectory))
                    {
                        Move(entry.StagingPath, entry.TargetPath, entry.IsDirectory);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Committed resource build output is missing: {entry.TargetPath}");
                    }
                }

                Delete(entry.StagingPath, entry.IsDirectory);
                Delete(entry.BackupPath, entry.IsDirectory);
            }
        }

        private static void CleanupCommitted(TransactionJournal journal)
        {
            try
            {
                CompleteCommitted(journal);
                DeleteJournal();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Resource build committed, but backup cleanup is pending recovery: {exception.Message}");
            }
        }

        private static void ValidateJournal(TransactionJournal journal, string journalPath)
        {
            if (journal?.Entries == null || journal.Entries.Count == 0)
            {
                throw new InvalidOperationException($"Resource build recovery journal has no outputs: {journalPath}");
            }

            foreach (var entry in journal.Entries)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.TargetPath) ||
                    string.IsNullOrWhiteSpace(entry.StagingPath) ||
                    string.IsNullOrWhiteSpace(entry.BackupPath))
                {
                    throw new InvalidOperationException($"Resource build recovery journal contains an invalid output: {journalPath}");
                }
                var targetPath = NormalizePath(entry.TargetPath);
                if (string.Equals(NormalizePath(entry.StagingPath), GetStagingPath(targetPath), StringComparison.OrdinalIgnoreCase) is false ||
                    string.Equals(NormalizePath(entry.BackupPath), GetBackupPath(targetPath), StringComparison.OrdinalIgnoreCase) is false)
                {
                    throw new InvalidOperationException($"Resource build recovery journal contains mismatched output paths: {journalPath}");
                }
            }
        }

        private static bool TargetsOverlap(OutputEntry first, OutputEntry second)
        {
            return first.IsDirectory && IsPathInside(first.TargetPath, second.TargetPath) ||
                   second.IsDirectory && IsPathInside(second.TargetPath, first.TargetPath);
        }

        private static bool IsPathInside(string directoryPath, string candidatePath)
        {
            var prefix = directoryPath.TrimEnd('/') + "/";
            return candidatePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static void WriteJournal(TransactionJournal journal)
        {
            var journalPath = Path.GetFullPath(JournalPath);
            var tempPath = journalPath + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(journalPath) ?? ".");
            var bytes = new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(journal, Formatting.Indented));
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            if (IOFile.Exists(journalPath))
            {
                IOFile.Replace(tempPath, journalPath, null);
            }
            else
            {
                IOFile.Move(tempPath, journalPath);
            }
        }

        private static void DeleteJournal()
        {
            var journalPath = Path.GetFullPath(JournalPath);
            if (IOFile.Exists(journalPath))
            {
                IOFile.Delete(journalPath);
            }

            var tempPath = journalPath + ".tmp";
            if (IOFile.Exists(tempPath))
            {
                IOFile.Delete(tempPath);
            }
        }

        private static bool Exists(string path, bool isDirectory)
        {
            return isDirectory ? Directory.Exists(path) : IOFile.Exists(path);
        }

        private static void Move(string source, string destination, bool isDirectory)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? ".");
            if (isDirectory)
            {
                Directory.Move(source, destination);
            }
            else
            {
                IOFile.Move(source, destination);
            }
        }

        private static void Delete(string path, bool isDirectory)
        {
            if (isDirectory)
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            else if (IOFile.Exists(path))
            {
                IOFile.Delete(path);
            }
        }

        private static void CopyDirectory(string sourceDirectory, string targetDirectory)
        {
            Directory.CreateDirectory(targetDirectory);
            foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = directory.Substring(sourceDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
            }

            foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = file.Substring(sourceDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var targetPath = Path.Combine(targetDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? targetDirectory);
                IOFile.Copy(file, targetPath, true);
            }
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Resource build output path cannot be empty.", nameof(path));
            }

            return Path.GetFullPath(path).Replace('\\', '/');
        }

        private static string CreateSiblingPath(string targetPath, string suffix)
        {
            var directory = Path.GetDirectoryName(targetPath)?.Replace('\\', '/') ?? ".";
            var name = Path.GetFileName(targetPath);
            return Path.Combine(directory, "." + name + suffix).Replace('\\', '/');
        }

        [Serializable]
        private sealed class TransactionJournal
        {
            public string State;
            public List<OutputRecord> Entries = new List<OutputRecord>();
        }

        [Serializable]
        private sealed class OutputRecord
        {
            public string TargetPath;
            public string StagingPath;
            public string BackupPath;
            public bool IsDirectory;
            public bool HadTarget;
        }

        private sealed class OutputEntry
        {
            public OutputEntry(string targetPath, bool isDirectory)
            {
                TargetPath = targetPath;
                StagingPath = GetStagingPath(targetPath);
                BackupPath = GetBackupPath(targetPath);
                IsDirectory = isDirectory;
            }

            public string TargetPath { get; }

            public string StagingPath { get; }

            public string BackupPath { get; }

            public bool IsDirectory { get; }

            public bool HadTarget { get; set; }

            public OutputRecord ToRecord()
            {
                return new OutputRecord
                {
                    TargetPath = TargetPath,
                    StagingPath = StagingPath,
                    BackupPath = BackupPath,
                    IsDirectory = IsDirectory,
                    HadTarget = HadTarget
                };
            }
        }
    }
}
