using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.File
{
    public partial class FileModule
    {
        internal FileTemporaryHandle CreateTemporaryFile(string owner, string identity)
        {
            if (string.IsNullOrWhiteSpace(owner))
            {
                throw new ArgumentException("Owner cannot be empty.", nameof(owner));
            }

            if (string.IsNullOrWhiteSpace(identity))
            {
                throw new ArgumentException("Identity cannot be empty.", nameof(identity));
            }

            BeginOperation();
            try
            {
                EnsureReady();
                var nativePath = Path.Combine(m_TemporaryRoot, $"{Guid.NewGuid():N}.tmp");
                var handle = new FileTemporaryHandle(owner, identity, nativePath, OnTemporaryFileReleased);
                m_TemporaryFiles.Add(handle);
                return handle;
            }
            catch
            {
                EndOperation();
                throw;
            }
        }

        internal async UniTask ImportTemporaryAsync(
            FileTemporaryHandle source,
            string path,
            string version,
            Func<Stream, UniTask> verifier)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (verifier == null)
            {
                throw new ArgumentNullException(nameof(verifier));
            }

            using (var sourceStream = await source.OpenReadAsync())
            {
                await WriteInternalAsync(path, version, sourceStream, StorageType.Standalone, verifier);
            }
        }

        private void InitializeTemporaryFiles()
        {
            m_TemporaryRoot = string.IsNullOrEmpty(m_RootPathOverride)
                ? Path.Combine(Application.temporaryCachePath, "gdk-files")
                : Path.Combine(m_RootPath, ".temporary");
            Directory.CreateDirectory(m_TemporaryRoot);
            foreach (var path in Directory.GetFiles(m_TemporaryRoot, "*.tmp"))
            {
                System.IO.File.Delete(path);
            }

            m_TemporaryFiles.Clear();
        }

        private async UniTask ReleaseTemporaryFilesAsync()
        {
            List<Exception> exceptions = null;
            foreach (var handle in m_TemporaryFiles.ToArray())
            {
                try
                {
                    await handle.ReleaseAsync();
                }
                catch (Exception exception)
                {
                    exceptions ??= new List<Exception>();
                    exceptions.Add(exception);
                }
            }

            if (exceptions != null)
            {
                throw new AggregateException("One or more temporary files could not be released.", exceptions);
            }
        }

        private void OnTemporaryFileReleased(FileTemporaryHandle handle)
        {
            if (m_TemporaryFiles.Remove(handle))
            {
                EndOperation();
            }
        }
    }
}
