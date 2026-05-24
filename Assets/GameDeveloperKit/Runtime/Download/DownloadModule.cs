using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Download
{
    public class DownloadModule : GameModuleBase
    {
        private readonly Dictionary<string, DownloadHandler> m_Downloads = new Dictionary<string, DownloadHandler>();
        private string m_TempRoot;

        public override UniTask Startup()
        {
            m_TempRoot = Path.Combine(Application.temporaryCachePath, "downloads");
            Directory.CreateDirectory(m_TempRoot);
            return UniTask.CompletedTask;
        }

        public override async UniTask Shutdown()
        {
            await CancelAll();
            m_Downloads.Clear();
        }

        public DownloadHandler DownloadAsync(string url)
        {
            ValidateUrl(url);
            return GetOrCreateHandler(url, true);
        }

        public DownloadListHandler DownloadListAsync(params string[] urls)
        {
            if (urls == null)
            {
                throw new ArgumentNullException(nameof(urls));
            }

            var handlers = new List<DownloadHandler>(urls.Length);
            foreach (var url in urls)
            {
                ValidateUrl(url);
                handlers.Add(GetOrCreateHandler(url, false));
            }

            var listHandler = new DownloadListHandler(handlers);
            listHandler.Start();
            return listHandler;
        }

        private DownloadHandler GetOrCreateHandler(string url, bool start)
        {
            if (m_Downloads.TryGetValue(url, out var handler))
            {
                if (start)
                {
                    handler.Start();
                }

                return handler;
            }

            handler = new DownloadHandler(url, m_TempRoot);
            m_Downloads.Add(url, handler);
            if (start)
            {
                handler.Start();
            }

            return handler;
        }

        public UniTask Pause(string url)
        {
            return m_Downloads.TryGetValue(url, out var handler) ? handler.Pause() : UniTask.CompletedTask;
        }

        public UniTask Resume(string url)
        {
            return m_Downloads.TryGetValue(url, out var handler) ? handler.Resume() : UniTask.CompletedTask;
        }

        public async UniTask Cancel(string url)
        {
            if (!m_Downloads.TryGetValue(url, out var handler))
            {
                return;
            }

            await handler.Cancel();
            m_Downloads.Remove(url);
        }

        public async UniTask CancelAll()
        {
            var handlers = new List<DownloadHandler>(m_Downloads.Values);
            foreach (var handler in handlers)
            {
                await handler.Cancel();
            }

            m_Downloads.Clear();
        }

        public bool HasDownload(string url)
        {
            return !string.IsNullOrEmpty(url) && m_Downloads.ContainsKey(url);
        }

        public DownloadHandler GetDownload(string url)
        {
            return !string.IsNullOrEmpty(url) && m_Downloads.TryGetValue(url, out var handler) ? handler : null;
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
    }
}