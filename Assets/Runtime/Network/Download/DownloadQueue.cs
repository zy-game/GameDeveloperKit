using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 下载队列（无锁优先级队列）
    /// </summary>
    internal class DownloadQueue
    {
        private readonly ConcurrentDictionary<int, ConcurrentQueue<DownloadHandle>> _priorityQueues;
        private readonly ConcurrentDictionary<DownloadHandle, int> _handleToPriority;
        private int _totalCount;

        public DownloadQueue()
        {
            _priorityQueues = new ConcurrentDictionary<int, ConcurrentQueue<DownloadHandle>>();
            _handleToPriority = new ConcurrentDictionary<DownloadHandle, int>();
            _totalCount = 0;
        }

        /// <summary>
        /// 队列中的任务数量
        /// </summary>
        public int Count => Interlocked.CompareExchange(ref _totalCount, 0, 0);

        /// <summary>
        /// 将任务加入队列
        /// </summary>
        public void Enqueue(DownloadHandle handle, int priority)
        {
            var queue = _priorityQueues.GetOrAdd(priority, _ => new ConcurrentQueue<DownloadHandle>());
            queue.Enqueue(handle);
            _handleToPriority[handle] = priority;

            Interlocked.Increment(ref _totalCount);
            handle.Status = DownloadStatus.Queued;
        }

        /// <summary>
        /// 从队列中取出最高优先级的任务
        /// </summary>
        public DownloadHandle Dequeue()
        {
            var priorities = _priorityQueues.Keys.OrderByDescending(p => p).ToList();

            foreach (var priority in priorities)
            {
                if (_priorityQueues.TryGetValue(priority, out var queue))
                {
                    if (queue.TryDequeue(out var handle))
                    {
                        _handleToPriority.TryRemove(handle, out _);
                        Interlocked.Decrement(ref _totalCount);
                        return handle;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 从队列中移除指定任务
        /// </summary>
        public bool Remove(DownloadHandle handle)
        {
            if (_handleToPriority.TryRemove(handle, out var priority))
            {
                Interlocked.Decrement(ref _totalCount);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取所有任务
        /// </summary>
        public List<DownloadHandle> GetAll()
        {
            return _handleToPriority.Keys.ToList();
        }
    }
}
