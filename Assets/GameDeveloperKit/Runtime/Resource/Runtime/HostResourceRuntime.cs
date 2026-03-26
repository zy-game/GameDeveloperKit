using System.Threading;
using Cysharp.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 主机资源运行时，支持热更新和远程资源下载。
    /// </summary>
    public sealed class HostResourceRuntime : OfflineResourceRuntime
    {
        /// <summary>
        /// 异步确保资源包准备就绪，包括检查和下载更新。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        /// <exception cref="IOException">当下载或验证资源失败时抛出。</exception>
        public override async UniTask EnsurePackageReadyAsync(ResourcePackageContext context, CancellationToken cancellationToken = default)
        {
            await base.EnsurePackageReadyAsync(context, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(context.ManifestRelativePath))
            {
                return;
            }

            var localManifestPath = context.ResolvePersistentPath(context.ManifestRelativePath);
            var remoteManifestTempPath = $"{localManifestPath}.remote";
            context.ResetUpdateReport(ResourceUpdateState.Checking, FrameworkOperationStage.Preparing, message: "Checking local and remote manifests.");

            var localManifestBackupPath = File.Exists(localManifestPath) ? $"{localManifestPath}.backup" : null;
            if (!string.IsNullOrWhiteSpace(localManifestBackupPath))
            {
                EnsureParentDirectory(localManifestBackupPath);
                File.Copy(localManifestPath, localManifestBackupPath, true);
            }

            var backupPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                CleanupRecoveryFiles(context);

                var manifestRequest = new DownloadRequest
                {
                    Urls = new[] { context.ResolveRemoteUrl(context.ManifestRelativePath) },
                    SavePath = remoteManifestTempPath
                };

                var manifestDownloadResult = await Game.Download.DownloadAsync(manifestRequest, cancellationToken);
                if (manifestDownloadResult.Status != DownloadStatus.Succeeded)
                {
                    if (!File.Exists(localManifestPath))
                    {
                        context.ResetUpdateReport(ResourceUpdateState.Failed, FrameworkOperationStage.Failed, message: manifestDownloadResult.ErrorMessage);
                        context.LastUpdateReport.ErrorMessage = manifestDownloadResult.ErrorMessage;
                        context.LastUpdateReport.Error = manifestDownloadResult.Error;
                        throw new IOException($"Failed to download package manifest for '{context.PackageName}': {manifestDownloadResult.ErrorMessage}");
                    }

                    return;
                }

                var localManifest = ResourceManifestUtility.LoadFromFile(localManifestPath);
                var remoteManifest = ResourceManifestUtility.LoadFromFile(remoteManifestTempPath);
                var comparison = ResourceManifestUtility.Compare(localManifest, remoteManifest);
                context.ResetUpdateReport(ResourceUpdateState.Checking, FrameworkOperationStage.Preparing, comparison.LocalVersion, comparison.RemoteVersion, "Manifest comparison completed.");

                if (comparison.IsChanged)
                {
                    context.TransitionUpdateState(ResourceUpdateState.Downloading, FrameworkOperationStage.Downloading, "Downloading changed resource files.");
                    var requests = BuildDownloadRequests(context, comparison.AddedOrModifiedEntries);
                    BackupAffectedFiles(requests, backupPaths);
                    BackupAffectedFiles(context, comparison.RemovedEntries, backupPaths);

                    var downloadBatchResult = await Game.Download.DownloadBatchAsync(requests, cancellationToken);
                    if (downloadBatchResult.Status != DownloadStatus.Succeeded)
                    {
                        context.TransitionUpdateState(ResourceUpdateState.Failed, FrameworkOperationStage.Failed, downloadBatchResult.ErrorMessage);
                        context.LastUpdateReport.ErrorMessage = downloadBatchResult.ErrorMessage;
                        context.LastUpdateReport.FailureKind = string.IsNullOrWhiteSpace(downloadBatchResult.FailureKind) ? "DownloadFailed" : downloadBatchResult.FailureKind;
                        context.LastUpdateReport.Error = downloadBatchResult.Results != null && downloadBatchResult.Results.Count > 0
                            ? downloadBatchResult.Results[downloadBatchResult.Results.Count - 1].Error
                            : FrameworkError.Create("ResourceDownloadFailed", downloadBatchResult.ErrorMessage, FrameworkFailureCategory.Resource, true, context.PackageName);
                        throw new IOException($"Failed to download hot-update files for '{context.PackageName}': {downloadBatchResult.ErrorMessage}");
                    }

                    context.TransitionUpdateState(ResourceUpdateState.Verifying, FrameworkOperationStage.Verifying, "Verifying downloaded resource files.");
                    VerifyDownloads(requests);

                    context.TransitionUpdateState(ResourceUpdateState.Applying, FrameworkOperationStage.Applying, "Applying downloaded resource files.");
                    RemoveDeletedFiles(context, comparison.RemovedEntries);
                    context.LastUpdateReport.DownloadedFileCount = downloadBatchResult.SucceededCount;
                    context.LastUpdateReport.DownloadedBytes = downloadBatchResult.DownloadedBytes;
                    context.LastUpdateReport.RemovedFileCount = comparison.RemovedEntries?.Count ?? 0;
                }

                EnsureParentDirectory(localManifestPath);
                File.Copy(remoteManifestTempPath, localManifestPath, true);
                DeleteFileIfExists(remoteManifestTempPath);
                CleanupBackupFiles(backupPaths);
                DeleteFileIfExists(localManifestBackupPath);
                context.TransitionUpdateState(ResourceUpdateState.Completed, FrameworkOperationStage.Completed, comparison.IsChanged ? "Resource update completed." : "Resource manifest already up to date.");
                context.LastUpdateReport.IsUpdated = comparison.IsChanged;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                var rollbackResult = Rollback(context, localManifestPath, localManifestBackupPath, backupPaths);
                context.TransitionUpdateState(rollbackResult.IsRolledBack ? ResourceUpdateState.RollingBack : ResourceUpdateState.Failed, rollbackResult.IsRolledBack ? FrameworkOperationStage.Applying : FrameworkOperationStage.Failed, rollbackResult.RecoveryMessage);
                context.LastUpdateReport.RolledBackFileCount = rollbackResult.RemovedFileCount;
                context.LastUpdateReport.RolledBackBytes = rollbackResult.RolledBackBytes;
                context.LastUpdateReport.RecoveryMessage = rollbackResult.RecoveryMessage;
                context.LastUpdateReport.ErrorMessage = exception.Message;
                context.LastUpdateReport.Error = FrameworkError.FromException("ResourceUpdateFailed", exception, FrameworkFailureCategory.Resource, true, context.PackageName);
                context.LastUpdateReport.FailureKind = ResolveFailureKind(exception);

                if (rollbackResult.IsRolledBack)
                {
                    context.TransitionUpdateState(ResourceUpdateState.Failed, FrameworkOperationStage.Failed, exception.Message, context.LastUpdateReport.Error);
                }

                throw;
            }
            finally
            {
                DeleteFileIfExists(remoteManifestTempPath);
                DeleteFileIfExists(localManifestBackupPath);
                CleanupBackupFiles(backupPaths);
            }
        }

        /// <summary>
        /// 根据资源模式解析路径。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="path">要解析的路径。</param>
        /// <returns>解析后的路径。</returns>
        protected override string ResolvePathByMode(ResourcePackageContext context, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(path))
            {
                return path;
            }

            var localPath = context.ResolvePersistentPath(path);
            if (File.Exists(localPath))
            {
                return localPath;
            }

            return context.ResolveRemoteUrl(path);
        }

        /// <summary>
        /// 构建下载请求列表。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="entries">资源清单条目列表。</param>
        /// <returns>下载请求列表。</returns>
        private static IReadOnlyList<DownloadRequest> BuildDownloadRequests(ResourcePackageContext context, IReadOnlyList<ResourceManifestEntry> entries)
        {
            var requests = new List<DownloadRequest>();
            if (entries == null)
            {
                return requests;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.FullPath))
                {
                    continue;
                }

                requests.Add(new DownloadRequest
                {
                    Urls = new[] { context.ResolveRemoteUrl(entry.FullPath) },
                    SavePath = context.ResolvePersistentPath(entry.FullPath),
                    ExpectedHash = entry.Hash,
                    ExpectedSizeBytes = entry.SizeBytes
                });
            }

            return requests;
        }

        /// <summary>
        /// 验证所有下载的文件。
        /// </summary>
        /// <param name="requests">下载请求列表。</param>
        /// <exception cref="IOException">当文件验证失败时抛出。</exception>
        private static void VerifyDownloads(IReadOnlyList<DownloadRequest> requests)
        {
            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                if (!VerifyDownload(request))
                {
                    throw new IOException($"Downloaded file verification failed: {request.SavePath}");
                }
            }
        }

        /// <summary>
        /// 删除已从清单中移除的文件。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="removedEntries">已移除的清单条目列表。</param>
        private static void RemoveDeletedFiles(ResourcePackageContext context, IReadOnlyList<ResourceManifestEntry> removedEntries)
        {
            if (removedEntries == null)
            {
                return;
            }

            for (var i = 0; i < removedEntries.Count; i++)
            {
                var entry = removedEntries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.FullPath))
                {
                    continue;
                }

                var localPath = context.ResolvePersistentPath(entry.FullPath);
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                }
            }
        }

        /// <summary>
        /// 验证下载的文件。
        /// </summary>
        /// <param name="request">下载请求。</param>
        /// <returns>如果验证成功返回true，否则返回false。</returns>
        private static bool VerifyDownload(DownloadRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.SavePath) || !File.Exists(request.SavePath))
            {
                return false;
            }

            if (request.ExpectedSizeBytes > 0)
            {
                var fileInfo = new FileInfo(request.SavePath);
                if (fileInfo.Length != request.ExpectedSizeBytes)
                {
                    return false;
                }
            }

            return Game.Download.VerifyFile(request.SavePath, request.ExpectedHash);
        }

        /// <summary>
        /// 备份受下载请求影响的文件。
        /// </summary>
        /// <param name="requests">下载请求列表。</param>
        /// <param name="backupPaths">备份路径字典。</param>
        private static void BackupAffectedFiles(IReadOnlyList<DownloadRequest> requests, IDictionary<string, string> backupPaths)
        {
            if (requests == null)
            {
                return;
            }

            for (var i = 0; i < requests.Count; i++)
            {
                BackupFile(requests[i]?.SavePath, backupPaths);
            }
        }

        /// <summary>
        /// 备份受清单条目影响的文件。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="entries">资源清单条目列表。</param>
        /// <param name="backupPaths">备份路径字典。</param>
        private static void BackupAffectedFiles(ResourcePackageContext context, IReadOnlyList<ResourceManifestEntry> entries, IDictionary<string, string> backupPaths)
        {
            if (context == null || entries == null)
            {
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.FullPath))
                {
                    continue;
                }

                BackupFile(context.ResolvePersistentPath(entry.FullPath), backupPaths);
            }
        }

        /// <summary>
        /// 备份单个文件。
        /// </summary>
        /// <param name="targetPath">目标文件路径。</param>
        /// <param name="backupPaths">备份路径字典。</param>
        private static void BackupFile(string targetPath, IDictionary<string, string> backupPaths)
        {
            if (string.IsNullOrWhiteSpace(targetPath) || backupPaths == null || backupPaths.ContainsKey(targetPath) || !File.Exists(targetPath))
            {
                return;
            }

            var backupPath = $"{targetPath}.rollback";
            EnsureParentDirectory(backupPath);
            File.Copy(targetPath, backupPath, true);
            backupPaths[targetPath] = backupPath;
        }

        /// <summary>
        /// 回滚资源更新到之前的状态。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="localManifestPath">本地清单路径。</param>
        /// <param name="localManifestBackupPath">本地清单备份路径。</param>
        /// <param name="backupPaths">备份路径字典。</param>
        /// <returns>资源更新结果。</returns>
        private static ResourceUpdateResult Rollback(ResourcePackageContext context, string localManifestPath, string localManifestBackupPath, IDictionary<string, string> backupPaths)
        {
            var result = new ResourceUpdateResult();
            result.Stage = FrameworkOperationStage.Applying;

            if (!string.IsNullOrWhiteSpace(localManifestBackupPath) && File.Exists(localManifestBackupPath))
            {
                EnsureParentDirectory(localManifestPath);
                File.Copy(localManifestBackupPath, localManifestPath, true);
                result.IsRolledBack = true;
            }

            if (backupPaths != null)
            {
                foreach (var pair in backupPaths)
                {
                    if (!File.Exists(pair.Value))
                    {
                        continue;
                    }

                    EnsureParentDirectory(pair.Key);
                    var backupInfo = new FileInfo(pair.Value);
                    File.Copy(pair.Value, pair.Key, true);
                    result.IsRolledBack = true;
                    result.RemovedFileCount++;
                    result.RolledBackBytes += backupInfo.Exists ? backupInfo.Length : 0;
                }
            }

            result.RecoveryMessage = result.IsRolledBack
                ? $"Recovered package '{context?.PackageName}' to the previous local version."
                : $"No local backup was available for package '{context?.PackageName}'.";
            return result;
        }

        /// <summary>
        /// 清理恢复文件（.rollback和.remote文件）。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        private static void CleanupRecoveryFiles(ResourcePackageContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.PersistentRoot) || !Directory.Exists(context.PersistentRoot))
            {
                return;
            }

            var rollbackFiles = Directory.GetFiles(context.PersistentRoot, "*.rollback", SearchOption.AllDirectories);
            for (var i = 0; i < rollbackFiles.Length; i++)
            {
                DeleteFileIfExists(rollbackFiles[i]);
            }

            var remoteFiles = Directory.GetFiles(context.PersistentRoot, "*.remote", SearchOption.AllDirectories);
            for (var i = 0; i < remoteFiles.Length; i++)
            {
                DeleteFileIfExists(remoteFiles[i]);
            }
        }

        /// <summary>
        /// 清理备份文件。
        /// </summary>
        /// <param name="backupPaths">备份路径字典。</param>
        private static void CleanupBackupFiles(IDictionary<string, string> backupPaths)
        {
            if (backupPaths == null)
            {
                return;
            }

            foreach (var pair in backupPaths)
            {
                DeleteFileIfExists(pair.Value);
            }
        }

        /// <summary>
        /// 解析异常的失败类型。
        /// </summary>
        /// <param name="exception">异常对象。</param>
        /// <returns>失败类型字符串。</returns>
        private static string ResolveFailureKind(Exception exception)
        {
            if (exception is IOException)
            {
                return "FileIO";
            }

            if (exception is FrameworkException frameworkException && frameworkException.Error != null)
            {
                return frameworkException.Error.Code;
            }

            return exception?.GetType().Name ?? "Unknown";
        }

        /// <summary>
        /// 确保父目录存在。
        /// </summary>
        /// <param name="path">文件路径。</param>
        private static void EnsureParentDirectory(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// 如果文件存在则删除。
        /// </summary>
        /// <param name="path">文件路径。</param>
        private static void DeleteFileIfExists(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
