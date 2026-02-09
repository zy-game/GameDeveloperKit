using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace GameDeveloperKit
{
    /// <summary>
    /// 引用池（线程安全版本）
    /// </summary>
    public static class ReferencePool
    {
        // 使用ConcurrentDictionary替代Dictionary + lock
        private static readonly ConcurrentDictionary<Type, Queue<IReference>> s_Pool = 
            new ConcurrentDictionary<Type, Queue<IReference>>();
        
        // 每个Queue使用独立的锁，减少锁竞争
        private static readonly ConcurrentDictionary<Type, object> s_QueueLocks = 
            new ConcurrentDictionary<Type, object>();
        
        private static int s_MainThreadId;
        private static bool s_Initialized;

        /// <summary>
        /// 初始化引用池（在Unity主线程调用）
        /// </summary>
        public static void Initialize()
        {
            if (s_Initialized) return;
            s_MainThreadId = Thread.CurrentThread.ManagedThreadId;
            s_Initialized = true;
        }

        /// <summary>
        /// 从引用池获取一个引用
        /// </summary>
        /// <typeparam name="T">引用的类型</typeparam>
        /// <returns>获取到的引用</returns>
        public static T Acquire<T>() where T : class, IReference, new()
        {
            if (!s_Initialized)
            {
                Game.Debug.Warning("ReferencePool not initialized. Auto-initializing...");
                Initialize();
            }
            
            var type = typeof(T);
            var queue = s_Pool.GetOrAdd(type, _ => new Queue<IReference>());
            var queueLock = s_QueueLocks.GetOrAdd(type, _ => new object());
            
            lock (queueLock)
            {
                if (queue.Count > 0)
                {
                    return (T)queue.Dequeue();
                }
            }
            
            return new T();
        }

        /// <summary>
        /// 将一个引用释放回引用池
        /// </summary>
        /// <param name="reference">要释放的引用</param>
        public static void Release(IReference reference)
        {
            if (reference == null) return;
            
            if (!s_Initialized)
            {
                Game.Debug.Warning("ReferencePool not initialized. Discarding reference.");
                return;
            }
            
            var type = reference.GetType();
            
            // 先清理，再入池
            try
            {
                reference.OnClearup();
            }
            catch (Exception ex)
            {
                Game.Debug.Error($"Error during reference cleanup: {ex}");
                return; // 清理失败的对象不入池
            }
            
            var queue = s_Pool.GetOrAdd(type, _ => new Queue<IReference>());
            var queueLock = s_QueueLocks.GetOrAdd(type, _ => new object());
            
            lock (queueLock)
            {
                // 限制池大小，防止内存泄漏
                if (queue.Count < 1000)
                {
                    queue.Enqueue(reference);
                }
            }
        }

        /// <summary>
        /// 清空指定类型的对象池
        /// </summary>
        public static void Clear<T>() where T : class, IReference
        {
            AssertMainThread(); // 清理操作必须在主线程
            
            var type = typeof(T);
            if (s_Pool.TryRemove(type, out _))
            {
                s_QueueLocks.TryRemove(type, out _);
            }
        }

        /// <summary>
        /// 清空所有对象池
        /// </summary>
        public static void ClearAll()
        {
            AssertMainThread(); // 清理操作必须在主线程
            
            s_Pool.Clear();
            s_QueueLocks.Clear();
        }

        /// <summary>
        /// 获取池统计信息
        /// </summary>
        public static int GetPoolCount<T>() where T : class, IReference
        {
            var type = typeof(T);
            if (s_Pool.TryGetValue(type, out var queue))
            {
                var queueLock = s_QueueLocks.GetOrAdd(type, _ => new object());
                lock (queueLock)
                {
                    return queue.Count;
                }
            }
            return 0;
        }

        private static void AssertMainThread()
        {
            if (!s_Initialized)
            {
                throw new InvalidOperationException("ReferencePool not initialized. Call ReferencePool.Initialize() first.");
            }
            
            if (Thread.CurrentThread.ManagedThreadId != s_MainThreadId)
            {
                throw new InvalidOperationException(
                    $"This operation must be called on Unity main thread (ID: {s_MainThreadId}). " +
                    $"Current thread ID: {Thread.CurrentThread.ManagedThreadId}");
            }
        }
    }
}