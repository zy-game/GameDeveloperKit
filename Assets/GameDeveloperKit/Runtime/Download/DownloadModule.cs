using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using UnityEngine;

namespace GameDeveloperKit.Download
{
    /// <summary>
    /// 下载模块
    /// </summary>
    [ModuleDependency(typeof(OperationModule))]
    public class DownloadModule : GameModuleBase
    {
        /// <summary>
        /// 存储 Downloads。
        /// </summary>
        private readonly Dictionary<string, DownloadHandler> m_Downloads = new Dictionary<string, DownloadHandler>();
        /// <summary>
        /// 存储 Temp Root。
        /// </summary>
        private string m_TempRoot;

        /// <summary>
        /// 下载模块的启动流程，主要是设置临时文件夹路径并创建该文件夹，以便在下载过程中存储临时文件。
        /// </summary>
        public override void Startup()
        {
            m_TempRoot = Path.Combine(Application.temporaryCachePath, "downloads");
            Directory.CreateDirectory(m_TempRoot);
        }

        /// <summary>
        /// 下载模块的关闭流程，主要是取消所有正在进行的下载并清理下载列表。
        /// </summary>
        public override void Shutdown()
        {
            CancelAllImmediate();
            m_Downloads.Clear();
        }

        /// <summary>
        /// 下载文件的流程，首先会对输入的URL进行验证，确保其格式正确且符合要求。
        /// </summary>
        /// <param name="url">url 参数。</param>
        /// <returns>执行结果。</returns>
        public DownloadHandler DownloadAsync(string url)
        {
            ValidateUrl(url);
            return GetOrCreateHandler(url, true);
        }

        /// <summary>
        /// 下载多个文件的流程，首先会对输入的URL列表进行验证，确保每个URL的格式正确且符合要求。
        /// </summary>
        /// <param name="urls">urls 参数。</param>
        /// <returns>执行结果。</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public DownloadListHandler DownloadListAsync(params string[] urls)
        {
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

            return App.Operation.ExecuteWithKey<DownloadListHandler>(
                listUrls,
                listUrls,
                (Func<string, DownloadHandler>)(url => GetOrCreateHandler(url, true)));
        }

        /// <summary>
        /// 获取或创建下载处理器的流程，首先会检查是否已经存在一个下载处理器来管理指定的URL。如果存在，并且start参数为true，则会启动该处理器的下载过程；如果不存在，则会创建一个新的下载处理器并将其添加到下载列表中。如果start参数为true，则新创建的处理器也会立即开始下载。这个流程设计确保了对于同一URL的重复下载请求能够得到合理的处理，避免了资源浪费和潜在的冲突，同时也提供了一个统一的接口来管理和协调下载任务，无论是已经存在的还是新创建的。
        /// </summary>
        /// <param name="url">url 参数。</param>
        /// <param name="start">start 参数。</param>
        /// <returns>执行结果。</returns>
        private DownloadHandler GetOrCreateHandler(string url, bool start)
        {
            if (m_Downloads.TryGetValue(url, out var handler))
            {
                if (start && handler.Status is Operation.OperationStatus.None or Operation.OperationStatus.Pending)
                {
                    App.Operation.Execute(url, handler, url, m_TempRoot);
                }

                return handler;
            }

            handler = App.Operation.ExecuteWithKey<DownloadHandler>(url, url, m_TempRoot);
            m_Downloads.Add(url, handler);
            return handler;
        }

        /// <summary>
        /// 暂停下载的流程，首先会检查是否存在一个下载处理器来管理指定的URL。
        /// 如果存在，则调用该处理器的Pause方法来暂停下载过程；如果不存在，则直接返回一个已完成的任务。
        /// 这个流程设计确保了对于不存在的URL的暂停请求能够得到合理的处理，避免了潜在的错误，同时也提供了一个统一的接口来管理和协调下载任务的暂停操作，无论是已经存在的还是不存在的URL。
        /// </summary>
        /// <param name="url">url 参数。</param>
        /// <returns>执行结果。</returns>
        public UniTask Pause(string url)
        {
            if (m_Downloads.TryGetValue(url, out var handler))
            {
                handler.SetPause();
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 恢复下载的流程，首先会检查是否存在一个下载处理器来管理指定的URL，并且该处理器的状态必须是Paused或Failed。
        /// 如果满足条件，则调用该处理器的Resume方法来恢复下载过程；如果不满足条件，则直接返回一个已完成的任务。
        /// 这个流程设计确保了对于不存在的URL或状态不合适的URL的恢复请求能够得到合理的处理，避免了潜在的错误，同时也提供了一个统一的接口来管理和协调下载任务的恢复操作，无论是已经存在的还是不存在的URL。
        /// </summary>
        /// <param name="url">url 参数。</param>
        /// <returns>执行结果。</returns>
        public UniTask Resume(string url)
        {
            if (m_Downloads.TryGetValue(url, out var handler))
            {
                handler.SetResume();
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 取消下载的流程，首先会检查是否存在一个下载处理器来管理指定的URL。
        /// 如果存在，则调用该处理器的Cancel方法来取消下载过程，并将其从下载列表中移除；如果不存在，则直接返回一个已完成的任务。
        /// 这个流程设计确保了对于不存在的URL的取消请求能够得到合理的处理，避免了潜在的错误，同时也提供了一个统一的接口来管理和协调下载任务的取消操作，无论是已经存在的还是不存在的URL。
        /// </summary>
        /// <param name="url">url 参数。</param>
        /// <returns>执行结果。</returns>
        public async UniTask Cancel(string url)
        {
            if (!m_Downloads.TryGetValue(url, out var handler))
            {
                return;
            }

            handler.SetCancel();
            await UniTask.Yield();
            m_Downloads.Remove(url);
        }

        /// <summary>
        /// 取消所有下载的流程，首先会创建一个包含当前所有下载处理器的列表，然后依次调用每个处理器的Cancel方法来取消下载过程。
        /// 最后，清空下载列表以释放相关资源。
        /// 这个流程设计确保了在需要取消所有下载任务时能够得到合理的处理，避免了潜在的错误，同时也提供了一个统一的接口来管理和协调多个下载任务的取消操作，确保系统能够正确地处理所有相关的下载任务和资源。
        /// </summary>
        /// <returns>执行结果。</returns>
        public async UniTask CancelAll()
        {
            CancelAllImmediate();
            await UniTask.Yield();
        }

        /// <summary>
        /// 检查是否存在下载的流程，首先会检查输入的URL是否为null或空字符串，如果是，则直接返回false。
        /// 然后，会检查下载列表中是否存在一个下载处理器来管理指定的URL，如果存在，则返回true；如果不存在，则返回false。
        /// 这个流程设计确保了对于无效URL的检查能够得到合理的处理，避免了潜在的错误，同时也提供了一个统一的接口来查询下载任务的存在性，无论是已经存在的还是不存在的URL。
        /// </summary>
        /// <param name="url">url 参数。</param>
        /// <returns>执行结果。</returns>
        public bool HasDownload(string url)
        {
            return !string.IsNullOrEmpty(url) && m_Downloads.ContainsKey(url);
        }

        /// <summary>
        /// 获取下载处理器的流程，首先会检查输入的URL是否为null或空字符串，如果是，则直接返回null。
        /// 然后，会尝试从下载列表中获取一个下载处理器来管理指定的URL，如果存在，则返回该处理器；如果不存在，则返回null。
        /// 这个流程设计确保了对于无效URL的获取请求能够得到合理的处理，避免了潜在的错误，同时也提供了一个统一的接口来查询和获取下载任务的处理器，无论是已经存在的还是不存在的URL。
        /// </summary>
        /// <param name="url">url 参数。</param>
        /// <returns>执行结果。</returns>
        public DownloadHandler GetDownload(string url)
        {
            return !string.IsNullOrEmpty(url) && m_Downloads.TryGetValue(url, out var handler) ? handler : null;
        }

        /// <summary>
        /// 验证URL的流程，首先会检查输入的URL是否为null，如果是，则抛出ArgumentNullException异常。
        /// 然后，会检查URL是否为一个空字符串或仅包含空白字符，如果是，则抛出ArgumentException异常。
        /// 最后，会尝试将URL解析为一个绝对URI，并检查其方案是否为HTTP或HTTPS，如果不满足条件，则抛出ArgumentException异常。
        /// 这个流程设计确保了对于无效URL的验证能够得到合理的处理，避免了潜在的错误，同时也提供了一个统一的接口来验证下载任务的URL，确保其格式正确且符合要求。
        /// </summary>
        /// <param name="url">url 参数。</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
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

        /// <summary>
        /// 取消所有下载并同步清理下载列表。
        /// </summary>
        private void CancelAllImmediate()
        {
            var handlers = new List<DownloadHandler>(m_Downloads.Values);
            foreach (var handler in handlers)
            {
                handler.SetCancel();
            }

            m_Downloads.Clear();
        }
    }
}
