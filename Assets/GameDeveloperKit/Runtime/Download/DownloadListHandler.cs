using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Download
{
    /// <summary>
    /// 下载列表处理器
    /// </summary>
    public class DownloadListHandler : OperationHandle
    {
        /// <summary>
        /// 存储 Items。
        /// </summary>
        private List<DownloadHandler> m_Items = new List<DownloadHandler>();
        /// <summary>
        /// 存储 Urls。
        /// </summary>
        private List<string> m_Urls = new List<string>();
        /// <summary>
        /// 存储 Resolve Item。
        /// </summary>
        private Func<string, DownloadHandler> m_ResolveItem;
        /// <summary>
        /// 下载列表处理器，负责管理和协调多个下载项的下载过程，提供暂停、恢复和取消功能，并通过事件通知下载进度和完成状态。它内部维护一个下载项列表，并在下载过程中监控每个项的状态，以确保整个下载列表的正确执行和状态更新。
        /// </summary>
        public IReadOnlyList<DownloadHandler> Items => m_Items;
        /// <summary>
        /// 下载进度，表示当前下载列表的整体进度，通常以0到1之间的浮点数表示，其中0表示下载未开始，1表示下载完成。这个属性通过计算所有下载项的进度来得出，可以用于在用户界面上显示下载进度条或其他相关信息，以便用户了解下载的当前状态和剩余时间等信息。
        /// </summary>
        public float Progress
        {
            get
            {
                var count = Math.Max(m_Items.Count, m_Urls.Count);
                if (count == 0)
                {
                    return 1f;
                }

                var progress = 0f;
                foreach (var item in m_Items)
                {
                    progress += item.Progress;
                }
                return progress / count;
            }
        }
        /// <summary>
        /// 下载列表处理器的事件，ProgressChanged事件在下载进度发生变化时触发，Completed事件在下载完成时触发。通过订阅这些事件，外部可以实时获取下载进度的更新，并在下载完成时执行相应的操作，例如更新用户界面、通知用户或进行后续处理等。这些事件提供了一种机制，使得下载列表处理器能够与外部系统进行交互，并及时响应下载状态的变化。
        /// </summary>
        public event Action<DownloadListHandler> ProgressChanged;
        /// <summary>
        /// 下载完成事件，表示当下载列表中的所有下载项都完成时触发。通过订阅这个事件，外部可以在下载完成时执行特定的操作，例如更新用户界面、通知用户或进行后续处理等。这提供了一种机制，使得下载列表处理器能够与外部系统进行交互，并及时响应下载状态的变化，确保在下载完成后能够执行必要的逻辑。
        /// </summary>
        public event Action<DownloadListHandler> Completed;

        /// <summary>
        /// 初始化 Download List Handler。
        /// </summary>
        internal DownloadListHandler()
        {
        }
        /// <summary>
        /// 等待下载完成的方法，返回一个UniTask对象，允许调用者异步等待下载列表中的所有下载项完成。当下载完成时，UniTask将被标记为完成状态，调用者可以通过await关键字等待这个任务的完成，从而在下载完成后执行后续的逻辑。这提供了一种方便的方式来处理下载完成后的操作，而无需阻塞主线程或使用回调函数。
        /// </summary>
        /// <returns>执行结果。</returns>
        public new async UniTask WaitCompletionAsync()
        {
            try
            {
                await base.WaitCompletionAsync();
            }
            catch
            {
            }
        }
        /// <summary>
        /// 暂停下载的方法，允许调用者暂停下载列表中的所有下载项。当调用这个方法时，下载列表的状态将被设置为Paused，并且每个下载项也会被暂停。调用者可以在需要的时候调用Resume方法来恢复下载。这个方法提供了一种机制，使得用户能够在下载过程中有更多的控制权，例如在网络状况不佳时暂停下载，或者在需要节省带宽时暂时停止下载等。
        /// </summary>
        /// <returns>执行结果。</returns>
        public async UniTask Pause()
        {
            SetPause();
            await UniTask.CompletedTask;
        }

        /// <summary>
        /// 将下载列表操作设置为暂停状态。
        /// </summary>
        public override void SetPause()
        {
            foreach (var item in m_Items)
            {
                item.SetPause();
            }

            base.SetPause();
        }
        /// <summary>
        /// 恢复下载的方法，允许调用者恢复下载列表中的所有下载项。当调用这个方法时，下载列表的状态将被设置为Downloading，并且每个下载项也会被恢复。调用者可以在需要的时候调用Pause方法来暂停下载。这个方法提供了一种机制，使得用户能够在下载过程中有更多的控制权，例如在网络状况改善时恢复下载，或者在需要继续下载时重新开始等。
        /// </summary>
        /// <returns>执行结果。</returns>
        public async UniTask Resume()
        {
            SetResume();
            await UniTask.CompletedTask;
        }

        /// <summary>
        /// 恢复下载列表操作。
        /// </summary>
        public override void SetResume()
        {
            if (Status is not OperationStatus.Paused and not OperationStatus.Failed)
            {
                return;
            }

            if (Status is OperationStatus.Failed)
            {
                SetReset();
            }
            else
            {
                base.SetResume();
            }

            foreach (var item in m_Items)
            {
                item.SetResume();
            }

            if (m_Urls.Count > 0 && m_ResolveItem != null)
            {
                App.Operation.Execute(this, this, m_Urls, m_ResolveItem);
            }
            else
            {
                App.Operation.Execute(this, this, m_Items);
            }
        }
        /// <summary>
        /// 取消下载的方法，允许调用者取消下载列表中的所有下载项。当调用这个方法时，下载列表的状态将被设置为Canceled，并且每个下载项也会被取消。调用者可以在需要的时候调用Resume方法来恢复下载。这个方法提供了一种机制，使得用户能够在下载过程中有更多的控制权，例如在网络状况不佳时取消下载，或者在需要节省带宽时完全停止下载等。同时，取消下载后，相关的资源和临时文件也可以被清理，以释放系统资源。
        /// </summary>
        /// <returns>执行结果。</returns>
        public async UniTask Cancel()
        {
            SetCancel();
            await UniTask.CompletedTask;
        }

        /// <summary>
        /// 取消下载列表操作。
        /// </summary>
        public override void SetCancel()
        {
            if (Status == OperationStatus.Cancelled)
            {
                return;
            }

            base.SetCancel();

            foreach (var item in m_Items)
            {
                item.SetCancel();
            }
        }

        /// <summary>
        /// 执行下载列表操作句柄。
        /// </summary>
        /// <param name="args">操作参数。</param>
        public override void Execute(params object[] args)
        {
            Initialize(args);
            RunAsync().Forget();
        }

        /// <summary>
        /// 初始化 member。
        /// </summary>
        /// <param name="args">args 参数。</param>
        private void Initialize(params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                throw new ArgumentException("DownloadListHandler requires download handler items.", nameof(args));
            }

            foreach (var item in m_Items)
            {
                item.ProgressChanged -= OnItemProgressChanged;
                item.Completed -= OnItemCompleted;
                item.Failed -= OnItemCompleted;
                item.Canceled -= OnItemCompleted;
            }

            m_Items = new List<DownloadHandler>();
            m_Urls = new List<string>();
            m_ResolveItem = null;
            if (args[0] is IEnumerable<DownloadHandler> items)
            {
                m_Items.AddRange(items);
            }
            else if (args[0] is IEnumerable<string> urls &&
                     args.Length > 1 &&
                     args[1] is Func<string, DownloadHandler> resolveItem)
            {
                m_Urls.AddRange(urls);
                m_ResolveItem = resolveItem;
            }
            else
            {
                throw new ArgumentException("DownloadListHandler requires download handlers or url resolver.", nameof(args));
            }

            foreach (var item in m_Items)
            {
                SubscribeItem(item);
            }
        }
        /// <summary>
        /// 运行下载的方法，负责执行下载列表中的所有下载项，并监控它们的状态以更新下载列表的整体状态。当调用这个方法时，它会依次启动每个下载项，并等待它们完成。在下载过程中，如果检测到下载列表被取消或暂停，它会相应地处理这些状态，并在下载完成后更新状态为Completed，并触发相关事件通知外部系统。这个方法提供了一个核心的执行流程，确保下载列表能够正确地管理和协调多个下载项的下载过程。
        /// </summary>
        /// <returns>执行结果。</returns>
        private async UniTaskVoid RunAsync()
        {
            try
            {
                if (m_Urls.Count > 0)
                {
                    foreach (var url in m_Urls)
                    {
                        if (IsPausedOrCancelled())
                        {
                            return;
                        }

                        var item = m_ResolveItem(url);
                        if (!m_Items.Contains(item))
                        {
                            m_Items.Add(item);
                            SubscribeItem(item);
                        }

                        await item.WaitCompletionAsync();
                    }
                }
                else
                {
                    foreach (var item in m_Items)
                    {
                        if (IsPausedOrCancelled())
                        {
                            return;
                        }

                        StartItem(item);
                        await item.WaitCompletionAsync();
                    }
                }

                if (IsPausedOrCancelled())
                {
                    return;
                }

                if (HasFailedItems())
                {
                    ProgressChanged?.Invoke(this);
                    SetException(new GameException("One or more downloads failed."));
                    return;
                }

                ProgressChanged?.Invoke(this);
                Completed?.Invoke(this);
                SetResult();
            }
            catch (Exception exception)
            {
                if (Status == OperationStatus.Cancelled)
                {
                    SetCancel();
                    return;
                }

                SetException(exception);
            }
        }
        /// <summary>
        /// 下载项进度变化的事件处理方法，当下载列表中的某个下载项的进度发生变化时触发。这个方法会更新下载列表的整体进度，并通过ProgressChanged事件通知外部系统，以便用户界面或其他相关组件能够及时反映下载进度的变化。通过这个机制，用户可以实时了解下载的当前状态和剩余时间等信息，从而提供更好的用户体验。
        /// </summary>
        /// <param name="handler">handler 参数。</param>
        private void OnItemProgressChanged(DownloadHandler handler)
        {
            SetProgress(Progress);
            ProgressChanged?.Invoke(this);
        }
        /// <summary>
        /// 下载项完成的事件处理方法，当下载列表中的某个下载项完成、失败或被取消时触发。这个方法会检查下载列表的整体状态，并在所有下载项都完成后更新下载列表的状态为Completed，并通过Completed事件通知外部系统，以便用户界面或其他相关组件能够及时反映下载完成的状态。通过这个机制，用户可以在下载完成时执行相应的操作，例如更新用户界面、通知用户或进行后续处理等，从而提供更好的用户体验。
        /// </summary>
        /// <param name="handler">handler 参数。</param>
        private void OnItemCompleted(DownloadHandler handler)
        {
            ProgressChanged?.Invoke(this);
        }

        /// <summary>
        /// 执行 Subscribe Item。
        /// </summary>
        /// <param name="item">item 参数。</param>
        private void SubscribeItem(DownloadHandler item)
        {
            item.ProgressChanged += OnItemProgressChanged;
            item.Completed += OnItemCompleted;
            item.Failed += OnItemCompleted;
            item.Canceled += OnItemCompleted;
        }

        /// <summary>
        /// 执行 Start Item。
        /// </summary>
        /// <param name="item">item 参数。</param>
        private void StartItem(DownloadHandler item)
        {
            if (item.Status is not OperationStatus.None and not OperationStatus.Pending)
            {
                return;
            }

            if (!string.IsNullOrEmpty(item.Url) && !string.IsNullOrEmpty(item.TempPathRoot))
            {
                App.Operation.Execute(item.Url, item, item.Url, item.TempPathRoot);
                return;
            }

            App.Operation.Execute(item, item);
        }

        /// <summary>
        /// 执行 Is Paused Or Cancelled。
        /// </summary>
        /// <returns>条件满足时返回 true。</returns>
        private bool IsPausedOrCancelled()
        {
            if (Status == OperationStatus.Cancelled)
            {
                SetCancel();
                return true;
            }

            return Status == OperationStatus.Paused;
        }
        /// <summary>
        /// 查询是否存在 Failed Items。
        /// </summary>
        /// <returns>条件满足时返回 true。</returns>
        private bool HasFailedItems()
        {
            foreach (var item in m_Items)
            {
                if (item.Status is OperationStatus.Failed or OperationStatus.Cancelled)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
