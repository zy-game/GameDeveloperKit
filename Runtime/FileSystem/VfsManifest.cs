using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

namespace GameDeveloperKit.File
{
    internal sealed class VfsManifest
    {
        private const string TempFilePrefix = ".vfs_manifest.";
        private const string TempFileSuffix = ".tmp";

        private readonly List<VFSMeta> m_Entries;
        private readonly string m_RootPath;

        private VfsManifest(string rootPath, List<VFSMeta> entries)
        {
            m_RootPath = rootPath;
            m_Entries = entries;
        }

        internal static VfsManifest Load(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("Root path cannot be empty.", nameof(rootPath));
            }

            CleanupStaleTempFiles(rootPath);
            var manifestPath = Path.Combine(rootPath, VfsConstants.ManifestFileName);
            if (!System.IO.File.Exists(manifestPath))
            {
                return new VfsManifest(rootPath, new List<VFSMeta>());
            }

            List<VFSMeta> entries;
            try
            {
                var json = System.IO.File.ReadAllText(manifestPath);
                entries = JsonConvert.DeserializeObject<List<VFSMeta>>(json) ?? new List<VFSMeta>();
                ValidateEntries(rootPath, entries);
            }
            catch (GameException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new GameException($"VFS manifest is invalid: {manifestPath}", exception);
            }

            return new VfsManifest(rootPath, entries);
        }

        internal VfsManifest Clone()
        {
            var entries = new List<VFSMeta>(m_Entries.Count);
            foreach (var entry in m_Entries)
            {
                entries.Add(entry.Clone());
            }

            return new VfsManifest(m_RootPath, entries);
        }

        internal VFSMeta AllocatePackedEntry(VFSMeta excludedEntry)
        {
            foreach (var entry in m_Entries)
            {
                if (!ReferenceEquals(entry, excludedEntry) &&
                    !entry.Usegd &&
                    entry.Storage == StorageType.Packed &&
                    !string.IsNullOrEmpty(entry.BundlePath))
                {
                    return entry;
                }
            }

            var bundleName = Guid.NewGuid().ToString("N");
            VFSMeta firstEntry = null;
            for (var index = 0; index < VfsConstants.BundleFileCount; index++)
            {
                var entry = new VFSMeta
                {
                    FilePath = string.Empty,
                    Storage = StorageType.Packed,
                    Offset = index * VfsConstants.DefaultThreshold,
                    Crc32 = 0,
                    Version = string.Empty,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Usegd = false,
                    BundlePath = bundleName
                };
                m_Entries.Add(entry);
                firstEntry = firstEntry ?? entry;
            }

            return firstEntry;
        }

        internal VFSMeta AllocateStandaloneEntry()
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

        internal string ReleaseEntry(VFSMeta entry)
        {
            if (entry == null)
            {
                return null;
            }

            var bundlePath = entry.Unused();
            if (!HasUsedBundle(bundlePath))
            {
                m_Entries.RemoveAll(candidate => candidate.BundlePath == bundlePath);
            }

            return bundlePath;
        }

        internal void Rename(string sourcePath, string destinationPath)
        {
            if (!TryGetEntry(sourcePath, out var sourceEntry) || !sourceEntry.Usegd)
            {
                throw new FileNotFoundException($"VFS source path '{sourcePath}' does not exist.", sourcePath);
            }

            if (TryGetEntry(destinationPath, out var destinationEntry) && destinationEntry.Usegd)
            {
                throw new IOException($"VFS destination path '{destinationPath}' already exists.");
            }

            sourceEntry.FilePath = destinationPath;
        }

        internal bool TryGetEntry(string virtualPath, out VFSMeta entry)
        {
            foreach (var candidate in m_Entries)
            {
                if (candidate.FilePath == virtualPath)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        internal bool HasUsedBundle(string bundlePath)
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

        internal bool ContainsBundle(string bundlePath)
        {
            foreach (var entry in m_Entries)
            {
                if (entry.BundlePath == bundlePath)
                {
                    return true;
                }
            }

            return false;
        }

        internal IEnumerable<VFSMeta> GetAllEntries()
        {
            return m_Entries;
        }

        internal async UniTask SaveAtomicAsync()
        {
            var manifestPath = Path.Combine(m_RootPath, VfsConstants.ManifestFileName);
            var tempPath = Path.Combine(
                m_RootPath,
                $"{TempFilePrefix}{Guid.NewGuid():N}{TempFileSuffix}");
            var json = JsonConvert.SerializeObject(m_Entries, Formatting.Indented);
            var bytes = new UTF8Encoding(false).GetBytes(json);

            try
            {
                using (var stream = new FileStream(
                           tempPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           4096,
                           FileOptions.WriteThrough))
                {
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (System.IO.File.Exists(manifestPath))
                {
                    System.IO.File.Replace(tempPath, manifestPath, null);
                }
                else
                {
                    System.IO.File.Move(tempPath, manifestPath);
                }
            }
            catch (Exception commitException)
            {
                try
                {
                    if (System.IO.File.Exists(tempPath))
                    {
                        System.IO.File.Delete(tempPath);
                    }
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(
                        "VFS manifest commit failed and temporary manifest cleanup also failed.",
                        commitException,
                        cleanupException);
                }

                throw;
            }
        }

        private static void CleanupStaleTempFiles(string rootPath)
        {
            if (!Directory.Exists(rootPath))
            {
                return;
            }

            foreach (var tempPath in Directory.GetFiles(rootPath, $"{TempFilePrefix}*{TempFileSuffix}"))
            {
                System.IO.File.Delete(tempPath);
            }
        }

        private static void ValidateEntries(string rootPath, IReadOnlyList<VFSMeta> entries)
        {
            var virtualPaths = new HashSet<string>(StringComparer.Ordinal);
            var slots = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null)
                {
                    throw InvalidEntry(index, "entry is null");
                }

                if (!Enum.IsDefined(typeof(StorageType), entry.Storage))
                {
                    throw InvalidEntry(index, $"Storage is not valid: {entry.Storage}");
                }

                if (!Guid.TryParseExact(entry.BundlePath, "N", out var bundleId) ||
                    !string.Equals(bundleId.ToString("N"), entry.BundlePath, StringComparison.Ordinal))
                {
                    throw InvalidEntry(index, $"BundlePath must be a canonical lowercase GUID: {entry.BundlePath}");
                }

                if (entry.Offset < 0 || entry.Size < 0)
                {
                    throw InvalidEntry(index, $"Offset and Size must be non-negative: {entry.Offset}/{entry.Size}");
                }

                if (entry.Storage == StorageType.Packed)
                {
                    if (entry.Offset % VfsConstants.DefaultThreshold != 0 ||
                        entry.Offset / VfsConstants.DefaultThreshold >= VfsConstants.BundleFileCount ||
                        entry.Size > VfsConstants.DefaultThreshold)
                    {
                        throw InvalidEntry(index, $"Packed slot is outside the configured bundle layout: {entry.Offset}/{entry.Size}");
                    }
                }
                else if (entry.Offset != 0)
                {
                    throw InvalidEntry(index, $"Standalone entry offset must be zero: {entry.Offset}");
                }

                var slotIdentity = $"{entry.BundlePath}:{entry.Offset}";
                if (!slots.Add(slotIdentity))
                {
                    throw InvalidEntry(index, $"duplicate bundle slot: {slotIdentity}");
                }

                if (!entry.Usegd)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.FilePath) || !virtualPaths.Add(entry.FilePath))
                {
                    throw InvalidEntry(index, $"used FilePath is empty or duplicated: {entry.FilePath}");
                }

                var bundlePath = Path.Combine(rootPath, entry.BundlePath);
                if (!System.IO.File.Exists(bundlePath))
                {
                    throw InvalidEntry(index, $"bundle file does not exist: {entry.BundlePath}");
                }

                var fileLength = new FileInfo(bundlePath).Length;
                if (entry.Offset > fileLength || entry.Size > fileLength - entry.Offset)
                {
                    throw InvalidEntry(
                        index,
                        $"entry range {entry.Offset}+{entry.Size} exceeds bundle length {fileLength}: {entry.BundlePath}");
                }
            }
        }

        private static GameException InvalidEntry(int index, string message)
        {
            return new GameException($"VFS manifest entry [{index}] is invalid: {message}");
        }
    }
}
