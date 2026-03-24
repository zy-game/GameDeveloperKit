using System.Threading;
using Cysharp.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System;

namespace GameDeveloperKit.Runtime
{
    public sealed class HostResourceRuntime : OfflineResourceRuntime
    {
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
            context.LastUpdateReport = new ResourceUpdateReport
            {
                State = ResourceUpdateState.Checking
            };

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
                    context.LastUpdateReport = new ResourceUpdateReport
                    {
                        State = ResourceUpdateState.Failed,
                        ErrorMessage = manifestDownloadResult.ErrorMessage
                    };
                    throw new IOException($"Failed to download package manifest for '{context.PackageName}': {manifestDownloadResult.ErrorMessage}");
                }

                return;
            }

            var localManifest = ResourceManifestUtility.LoadFromFile(localManifestPath);
            var remoteManifest = ResourceManifestUtility.LoadFromFile(remoteManifestTempPath);
            var comparison = ResourceManifestUtility.Compare(localManifest, remoteManifest);
            context.LastUpdateReport = new ResourceUpdateReport
            {
                State = ResourceUpdateState.Checking,
                LocalManifestVersion = comparison.LocalVersion,
                RemoteManifestVersion = comparison.RemoteVersion
            };

            if (comparison.IsChanged)
            {
                context.LastUpdateReport.State = ResourceUpdateState.Downloading;
                var requests = BuildDownloadRequests(context, comparison.AddedOrModifiedEntries);
                var downloadBatchResult = await Game.Download.DownloadBatchAsync(requests, cancellationToken);
                if (downloadBatchResult.Status != DownloadStatus.Succeeded)
                {
                    context.LastUpdateReport.State = ResourceUpdateState.Failed;
                    context.LastUpdateReport.ErrorMessage = downloadBatchResult.ErrorMessage;
                    throw new IOException($"Failed to download hot-update files for '{context.PackageName}': {downloadBatchResult.ErrorMessage}");
                }

                context.LastUpdateReport.State = ResourceUpdateState.Verifying;
                VerifyDownloads(requests);

                context.LastUpdateReport.State = ResourceUpdateState.Applying;
                RemoveDeletedFiles(context, comparison.RemovedEntries);
                context.LastUpdateReport.DownloadedFileCount = requests.Count;
                context.LastUpdateReport.DownloadedBytes = downloadBatchResult.DownloadedBytes;
                context.LastUpdateReport.RemovedFileCount = comparison.RemovedEntries?.Count ?? 0;
            }

            File.Copy(remoteManifestTempPath, localManifestPath, true);
            File.Delete(remoteManifestTempPath);
            context.LastUpdateReport.State = ResourceUpdateState.Completed;
            context.LastUpdateReport.IsUpdated = comparison.IsChanged;
        }

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
                    ExpectedHash = entry.Hash
                });
            }

            return requests;
        }

        private static void VerifyDownloads(IReadOnlyList<DownloadRequest> requests)
        {
            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                if (!Game.Download.VerifyFile(request.SavePath, request.ExpectedHash))
                {
                    throw new IOException($"Downloaded file verification failed: {request.SavePath}");
                }
            }
        }

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
    }
}
