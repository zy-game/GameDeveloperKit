using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.File;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Download
{
    [ModuleDependency(typeof(OperationModule))]
    [ModuleDependency(typeof(FileModule))]
    public class DownloadModule : GameModuleBase, IAsyncShutdownParticipant
    {
        private readonly Dictionary<string, DownloadHandler> m_Downloads = new Dictionary<string, DownloadHandler>();
        private readonly HashSet<DownloadListHandler> m_DownloadLists = new HashSet<DownloadListHandler>();
        private bool m_IsPreparingShutdown;
        private bool m_ShutdownPrepared;

        public override void Startup()
        {
            m_IsPreparingShutdown = false;
            m_ShutdownPrepared = false;
        }

        public override void Shutdown()
        {
            if (m_Downloads.Count > 0 || m_DownloadLists.Count > 0)
            {
                throw new GameException("DownloadModule has tracked downloads. Await async shutdown preparation before Shutdown.");
            }

            m_Downloads.Clear();
            m_DownloadLists.Clear();
            m_IsPreparingShutdown = false;
            m_ShutdownPrepared = false;
        }

        public DownloadHandler DownloadAsync(string url)
        {
            ThrowIfPreparingShutdown();
            ValidateUrl(url);
            return GetOrCreateHandler(url);
        }

        public DownloadListHandler DownloadListAsync(params string[] urls)
        {
            ThrowIfPreparingShutdown();
            if (urls == null)
            {
                throw new ArgumentNullException(nameof(urls));
            }

            var listUrls = new List<string>(urls.Length);
            foreach (var url in urls)
            {
                ValidateUrl(url);
                listUrls.Add(url);
            }

            var list = App.Operation.ExecuteWithKey<DownloadListHandler>(
                listUrls,
                listUrls,
                (Func<string, DownloadHandler>)GetOrCreateHandler);
            m_DownloadLists.Add(list);
            TrackDownloadListAsync(list).Forget(UnityEngine.Debug.LogException);
            return list;
        }

        /// <summary>
        /// 获取或创建当前 URL 的下载执行。失败或取消后的再次下载始终创建新句柄。
        /// </summary>
        private DownloadHandler GetOrCreateHandler(string url)
        {
            ThrowIfPreparingShutdown();
            if (m_Downloads.TryGetValue(url, out var handler))
            {
                if (handler.Status is not OperationStatus.Failed and not OperationStatus.Cancelled)
                {
                    return handler;
                }

                m_Downloads.Remove(url);
            }

            handler = App.Operation.ExecuteWithKey<DownloadHandler>(url, url, App.File);
            m_Downloads[url] = handler;
            return handler;
        }

        public UniTask Pause(string url)
        {
            if (m_Downloads.TryGetValue(url, out var handler))
            {
                handler.SetPause();
            }

            return UniTask.CompletedTask;
        }

        public UniTask Resume(string url)
        {
            if (m_Downloads.TryGetValue(url, out var handler))
            {
                handler.SetResume();
            }

            return UniTask.CompletedTask;
        }

        public async UniTask Cancel(string url)
        {
            if (!m_Downloads.TryGetValue(url, out var handler))
            {
                return;
            }

            await handler.Cancel();
            if (m_Downloads.TryGetValue(url, out var current) && ReferenceEquals(current, handler))
            {
                m_Downloads.Remove(url);
            }
        }

        public async UniTask CancelAll()
        {
            var lists = new List<DownloadListHandler>(m_DownloadLists);
            foreach (var list in lists)
            {
                await list.Cancel();
                m_DownloadLists.Remove(list);
            }

            var downloads = new List<KeyValuePair<string, DownloadHandler>>(m_Downloads);
            foreach (var pair in downloads)
            {
                await pair.Value.Cancel();
                if (m_Downloads.TryGetValue(pair.Key, out var current) && ReferenceEquals(current, pair.Value))
                {
                    m_Downloads.Remove(pair.Key);
                }
            }
        }

        public bool HasDownload(string url)
        {
            return !string.IsNullOrEmpty(url) && m_Downloads.ContainsKey(url);
        }

        public DownloadHandler GetDownload(string url)
        {
            return !string.IsNullOrEmpty(url) && m_Downloads.TryGetValue(url, out var handler) ? handler : null;
        }

        /// <summary>
        /// 释放终态下载结果及其 FileModule 临时文件。
        /// </summary>
        public async UniTask ReleaseAsync(DownloadHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!handler.IsDone)
            {
                throw new GameException("An active download cannot be released. Cancel it first.");
            }

            if (!m_Downloads.TryGetValue(handler.Url, out var current) || !ReferenceEquals(current, handler))
            {
                return;
            }

            await handler.WaitRunFinishedAsync();
            handler.ReleaseResult();
            m_Downloads.Remove(handler.Url);
        }

        private static void ValidateUrl(string url)
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("Url cannot be empty.", nameof(url));
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("Url must be an absolute HTTP or HTTPS url.", nameof(url));
            }
        }

        async UniTask IAsyncShutdownParticipant.PrepareShutdownAsync()
        {
            if (m_ShutdownPrepared)
            {
                return;
            }

            m_IsPreparingShutdown = true;
            await CancelAll();
            m_ShutdownPrepared = true;
        }

        private void ThrowIfPreparingShutdown()
        {
            if (m_IsPreparingShutdown)
            {
                throw new GameException("DownloadModule is preparing to shut down and cannot start new downloads.");
            }
        }

        private async UniTask TrackDownloadListAsync(DownloadListHandler list)
        {
            await list.WaitCompletionAsync();
            m_DownloadLists.Remove(list);
        }
    }
}
