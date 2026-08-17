using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.File
{
    internal static class WebGLPersistentFileSystem
    {
        private const float SyncTimeoutSeconds = 30f;
        private static readonly SemaphoreSlim s_SyncGate = new SemaphoreSlim(1, 1);

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int GDK_WebGLReplaceFile(string sourcePath, string destinationPath);

        [DllImport("__Internal")]
        private static extern int GDK_WebGLPopulatePersistentFileSystem(string rootPath);

        [DllImport("__Internal")]
        private static extern int GDK_WebGLBeginPersistentSync(string rootPath);

        [DllImport("__Internal")]
        private static extern int GDK_WebGLPollPersistentSync(int requestId);
#endif

        internal static void Populate(string rootPath)
        {
            ValidateRootPath(rootPath);
#if UNITY_WEBGL && !UNITY_EDITOR
            if (GDK_WebGLPopulatePersistentFileSystem(rootPath) != 0)
            {
                throw new IOException(
                    $"Unable to populate the WebGL persistent file system: '{rootPath}'.");
            }
#endif
        }

        internal static FileStream CreateAtomicWriteStream(string path)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.None);
#else
            return new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough);
#endif
        }

        internal static void Flush(FileStream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            stream.Flush();
#else
            stream.Flush(true);
#endif
        }

        internal static void ReplaceFile(string sourcePath, string destinationPath)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (GDK_WebGLReplaceFile(sourcePath, destinationPath) != 0)
            {
                throw new IOException(
                    $"WebGL virtual file replace failed: '{sourcePath}' -> '{destinationPath}'.");
            }
#else
            if (System.IO.File.Exists(destinationPath))
            {
                System.IO.File.Replace(sourcePath, destinationPath, null);
            }
            else
            {
                System.IO.File.Move(sourcePath, destinationPath);
            }
#endif
        }

        internal static async UniTask SyncAsync(string rootPath)
        {
            ValidateRootPath(rootPath);
#if UNITY_WEBGL && !UNITY_EDITOR
            await s_SyncGate.WaitAsync();
            try
            {
                var requestId = GDK_WebGLBeginPersistentSync(rootPath);
                if (requestId <= 0)
                {
                    throw new IOException("Unable to start WebGL persistent file system sync.");
                }

                var startedAt = Time.realtimeSinceStartup;
                while (true)
                {
                    var status = GDK_WebGLPollPersistentSync(requestId);
                    if (status == 1)
                    {
                        return;
                    }

                    if (status < 0)
                    {
                        throw new IOException("WebGL persistent file system sync failed.");
                    }

                    if (Time.realtimeSinceStartup - startedAt >= SyncTimeoutSeconds)
                    {
                        throw new TimeoutException(
                            $"WebGL persistent file system sync exceeded {SyncTimeoutSeconds} seconds.");
                    }

                    await UniTask.Yield();
                }
            }
            finally
            {
                s_SyncGate.Release();
            }
#else
            await UniTask.CompletedTask;
#endif
        }

        private static void ValidateRootPath(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("Persistent file-system root cannot be empty.", nameof(rootPath));
            }
        }
    }
}
