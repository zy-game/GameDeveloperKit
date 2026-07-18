using System;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace GameDeveloperKit.File
{
    public partial class FileModule
    {
        internal async UniTask<Stream> OpenPackagedReadAsync(string location)
        {
            ValidateSourceLocation(location, nameof(location));
            BeginOperation();
            Stream source = null;
            VfsReadStream lease = null;
            FileTemporaryHandle temporary = null;
            try
            {
                EnsureReady();
                var address = ResolvePackagedAddress(location);
                if (TryResolvePhysicalPath(address, out var physicalPath))
                {
                    if (!System.IO.File.Exists(physicalPath))
                    {
                        EndOperation();
                        return null;
                    }

                    source = new FileStream(
                        physicalPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read | FileShare.Delete);
                }
                else
                {
                    temporary = CreateTemporaryFile("packaged", location);
                    using (var request = UnityWebRequest.Get(address))
                    {
                        request.downloadHandler = new DownloadHandlerFile(temporary.NativePath);
                        await request.SendWebRequest();
                        if (request.result != UnityWebRequest.Result.Success)
                        {
                            await temporary.ReleaseAsync();
                            temporary = null;
                            EndOperation();
                            return null;
                        }
                    }

                    source = await temporary.OpenReadAsync();
                }

                ThrowIfPreparingShutdown();
                var stagedTemporary = temporary;
                temporary = null;
                lease = new VfsReadStream(
                    source,
                    0,
                    source.Length,
                    stagedTemporary == null
                        ? OnReadStreamDisposed
                        : stream => OnPackagedTemporaryStreamDisposed(stream, stagedTemporary));
                source = null;
                m_ReadStreams.Add(lease);
                return lease;
            }
            catch (Exception operationException)
            {
                Exception cleanupException = null;
                try
                {
                    source?.Dispose();
                }
                catch (Exception exception)
                {
                    cleanupException = exception;
                }

                try
                {
                    if (temporary != null)
                    {
                        await temporary.ReleaseAsync();
                    }
                }
                catch (Exception exception)
                {
                    cleanupException = cleanupException == null
                        ? exception
                        : new AggregateException(cleanupException, exception);
                }

                try
                {
                    if (lease != null)
                    {
                        lease.Dispose();
                    }
                    else
                    {
                        EndOperation();
                    }
                }
                catch (Exception exception)
                {
                    cleanupException = cleanupException == null
                        ? exception
                        : new AggregateException(cleanupException, exception);
                }

                if (cleanupException != null)
                {
                    throw new AggregateException(
                        "Packaged file open failed and cleanup also failed.",
                        operationException,
                        cleanupException);
                }

                throw;
            }
        }

        private void OnPackagedTemporaryStreamDisposed(
            VfsReadStream stream,
            FileTemporaryHandle temporary)
        {
            Exception cleanupException = null;
            try
            {
                temporary.ReleaseAsync().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                cleanupException = exception;
            }
            finally
            {
                OnReadStreamDisposed(stream);
            }

            if (cleanupException != null)
            {
                throw cleanupException;
            }
        }

        internal bool TryReadPackagedBytes(string location, out byte[] data)
        {
            ValidateSourceLocation(location, nameof(location));
            BeginOperation();
            try
            {
                EnsureReady();
                var address = ResolvePackagedAddress(location);
                if (!TryResolvePhysicalPath(address, out var physicalPath) ||
                    !System.IO.File.Exists(physicalPath))
                {
                    data = null;
                    return false;
                }

                data = System.IO.File.ReadAllBytes(physicalPath);
                return true;
            }
            finally
            {
                EndOperation();
            }
        }

        internal UniTask<Stream> OpenExternalReadAsync(string absolutePath)
        {
            ValidateSourceLocation(absolutePath, nameof(absolutePath));
            if (!Path.IsPathRooted(absolutePath))
            {
                throw new ArgumentException("External file path must be absolute.", nameof(absolutePath));
            }

            BeginOperation();
            Stream source = null;
            VfsReadStream lease = null;
            try
            {
                EnsureReady();
                source = new FileStream(
                    absolutePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read | FileShare.Delete);
                lease = new VfsReadStream(source, 0, source.Length, OnReadStreamDisposed);
                source = null;
                m_ReadStreams.Add(lease);
                return UniTask.FromResult<Stream>(lease);
            }
            catch
            {
                source?.Dispose();
                if (lease != null)
                {
                    lease.Dispose();
                }
                else
                {
                    EndOperation();
                }

                throw;
            }
        }

        internal bool ExternalFileExists(string absolutePath)
        {
            ValidateSourceLocation(absolutePath, nameof(absolutePath));
            if (!Path.IsPathRooted(absolutePath))
            {
                throw new ArgumentException("External file path must be absolute.", nameof(absolutePath));
            }

            BeginOperation();
            try
            {
                EnsureReady();
                return System.IO.File.Exists(absolutePath);
            }
            finally
            {
                EndOperation();
            }
        }

        private static string ResolvePackagedAddress(string location)
        {
            if (Path.IsPathRooted(location) || Uri.TryCreate(location, UriKind.Absolute, out _))
            {
                return location.Replace('\\', '/');
            }

            return Path.Combine(Application.streamingAssetsPath, location).Replace('\\', '/');
        }

        private static bool TryResolvePhysicalPath(string address, out string physicalPath)
        {
            if (Path.IsPathRooted(address))
            {
                physicalPath = address;
                return true;
            }

            if (Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                physicalPath = uri.LocalPath;
                return true;
            }

            physicalPath = null;
            return false;
        }

        private static void ValidateSourceLocation(string location, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("File source location cannot be empty.", parameterName);
            }
        }
    }
}
