using System;
using System.Collections.Generic;

namespace GameDeveloperKit
{
    /// <summary>
    /// 引用池类，用于管理和复用实现了IReference接口的对象实例
    /// </summary>
    public static class ReferencePool
    {
        /// <summary>
        /// 存储 Reference Collections。
        /// </summary>
        private static readonly Dictionary<Type, ReferenceCollection> s_ReferenceCollections = new Dictionary<Type, ReferenceCollection>();
        /// <summary>
        /// 记录 Enable Strict Check 状态。
        /// </summary>
        private static bool s_EnableStrictCheck;

        public static bool EnableStrictCheck
        {
            get => s_EnableStrictCheck;
            set => s_EnableStrictCheck = value;
        }

        /// <summary>
        /// 引用类型数量，表示当前引用池中管理的不同类型的引用对象的数量
        /// </summary>
        public static int Count => s_ReferenceCollections.Count;

        /// <summary>
        /// 清空所有引用对象，释放引用池中管理的所有对象实例，并清除引用类型的记录
        /// </summary>
        public static void ClearAll()
        {
            lock (s_ReferenceCollections)
            {
                foreach (var referenceCollection in s_ReferenceCollections)
                {
                    referenceCollection.Value.RemoveAll();
                }

                s_ReferenceCollections.Clear();
            }
        }

        /// <summary>
        /// 获取引用对象，返回一个指定类型的引用对象实例。如果引用池中有可用的实例，则直接返回；否则，创建一个新的实例并返回
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        /// <returns>执行结果。</returns>
        public static T Acquire<T>() where T : class, IReference, new()
        {
            return GetReferenceCollection(typeof(T)).Acquire<T>();
        }

        /// <summary>
        /// 获取引用对象，返回一个指定类型的引用对象实例。如果引用池中有可用的实例，则直接返回；否则，创建一个新的实例并返回
        /// </summary>
        /// <param name="referenceType">reference Type 参数。</param>
        /// <returns>执行结果。</returns>
        public static IReference Acquire(Type referenceType)
        {
            InternalCheckReferenceType(referenceType);
            return GetReferenceCollection(referenceType).Acquire();
        }

        /// <summary>
        /// 释放引用对象，将一个引用对象实例返回到引用池中，以便后续的获取操作能够复用该实例
        /// </summary>
        /// <param name="reference">reference 参数。</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void Release(IReference reference)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            var referenceType = reference.GetType();
            InternalCheckReferenceType(referenceType);
            GetReferenceCollection(referenceType).Release(reference);
        }

        /// <summary>
        /// 添加引用对象，向引用池中添加指定数量的引用对象实例，以便后续的获取操作能够复用这些实例
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        /// <param name="count">count 参数。</param>
        public static void Add<T>(int count) where T : class, IReference, new()
        {
            GetReferenceCollection(typeof(T)).Add<T>(count);
        }

        /// <summary>
        /// 添加引用对象，向引用池中添加指定数量的引用对象实例，以便后续的获取操作能够复用这些实例
        /// </summary>
        /// <param name="referenceType">reference Type 参数。</param>
        /// <param name="count">count 参数。</param>
        public static void Add(Type referenceType, int count)
        {
            InternalCheckReferenceType(referenceType);
            GetReferenceCollection(referenceType).Add(count);
        }

        /// <summary>
        /// 移除引用对象，从引用池中移除指定数量的引用对象实例，以便释放资源并减少池中的对象数量
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        /// <param name="count">count 参数。</param>
        public static void Remove<T>(int count) where T : class, IReference
        {
            GetReferenceCollection(typeof(T)).Remove(count);
        }

        /// <summary>
        /// 移除引用对象，从引用池中移除指定数量的引用对象实例，以便释放资源并减少池中的对象数量
        /// </summary>
        /// <param name="referenceType">reference Type 参数。</param>
        /// <param name="count">count 参数。</param>
        public static void Remove(Type referenceType, int count)
        {
            InternalCheckReferenceType(referenceType);
            GetReferenceCollection(referenceType).Remove(count);
        }

        /// <summary>
        /// 移除所有引用对象，从引用池中移除所有指定类型的引用对象实例，以便释放资源并清空池中的对象数量
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public static void RemoveAll<T>() where T : class, IReference
        {
            GetReferenceCollection(typeof(T)).RemoveAll();
        }

        /// <summary>
        /// 移除所有引用对象，从引用池中移除所有指定类型的引用对象实例，以便释放资源并清空池中的对象数量
        /// </summary>
        /// <param name="referenceType">reference Type 参数。</param>
        public static void RemoveAll(Type referenceType)
        {
            InternalCheckReferenceType(referenceType);
            GetReferenceCollection(referenceType).RemoveAll();
        }

        /// <summary>
        /// 内部检查引用类型，验证传入的引用类型是否符合要求，包括是否为非抽象类、是否实现了IReference接口等
        /// </summary>
        /// <param name="referenceType">reference Type 参数。</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        private static void InternalCheckReferenceType(Type referenceType)
        {
            if (!s_EnableStrictCheck)
            {
                return;
            }

            if (referenceType == null)
            {
                throw new ArgumentNullException(nameof(referenceType));
            }

            if (!referenceType.IsClass || referenceType.IsAbstract)
            {
                throw new InvalidOperationException("Reference type must be a non-abstract class.");
            }

            if (!typeof(IReference).IsAssignableFrom(referenceType))
            {
                throw new InvalidOperationException($"Reference type '{referenceType.FullName}' is invalid.");
            }
        }

        /// <summary>
        /// 获取引用集合，返回一个指定类型的引用集合实例，用于管理和复用该类型的引用对象
        /// </summary>
        /// <param name="referenceType">reference Type 参数。</param>
        /// <returns>执行结果。</returns>
        /// <exception cref="ArgumentNullException"></exception>
        private static ReferenceCollection GetReferenceCollection(Type referenceType)
        {
            if (referenceType == null)
            {
                throw new ArgumentNullException(nameof(referenceType));
            }

            lock (s_ReferenceCollections)
            {
                if (!s_ReferenceCollections.TryGetValue(referenceType, out var referenceCollection))
                {
                    referenceCollection = new ReferenceCollection(referenceType);
                    s_ReferenceCollections.Add(referenceType, referenceCollection);
                }

                return referenceCollection;
            }
        }

        /// <summary>
        /// 引用集合类，用于管理和复用特定类型的引用对象实例
        /// </summary>
        private sealed class ReferenceCollection
        {
            /// <summary>
            /// 存储 References。
            /// </summary>
            private readonly Queue<IReference> m_References = new Queue<IReference>();
            /// <summary>
            /// 存储 Reference Type。
            /// </summary>
            private readonly Type m_ReferenceType;
            /// <summary>
            /// 存储 Using Reference Count。
            /// </summary>
            private int m_UsingReferenceCount;
            /// <summary>
            /// 存储 Acquire Reference Count。
            /// </summary>
            private int m_AcquireReferenceCount;
            /// <summary>
            /// 存储 Release Reference Count。
            /// </summary>
            private int m_ReleaseReferenceCount;
            /// <summary>
            /// 存储 Add Reference Count。
            /// </summary>
            private int m_AddReferenceCount;
            /// <summary>
            /// 存储 Remove Reference Count。
            /// </summary>
            private int m_RemoveReferenceCount;

            /// <summary>
            /// 初始化引用集合。
            /// </summary>
            /// <param name="referenceType">引用类型。</param>
            public ReferenceCollection(Type referenceType)
            {
                m_ReferenceType = referenceType;
            }

            /// <summary>
            /// 未使用的引用数量，表示当前引用集合中可供获取的引用对象实例的数量
            /// </summary>
            public int UnusedReferenceCount => m_References.Count;

            /// <summary>
            /// 正在使用的引用数量，表示当前引用集合中正在被获取和使用的引用对象实例的数量
            /// </summary>
            public int UsingReferenceCount => m_UsingReferenceCount;

            /// <summary>
            /// 获取引用数量，表示当前引用集合中被获取的引用对象实例的总数量
            /// </summary>
            public int AcquireReferenceCount => m_AcquireReferenceCount;

            /// <summary>
            /// 释放引用数量，表示当前引用集合中被释放的引用对象实例的总数量
            /// </summary>
            public int ReleaseReferenceCount => m_ReleaseReferenceCount;

            /// <summary>
            /// 添加引用数量，表示当前引用集合中被添加到池中的引用对象实例的总数量
            /// </summary>
            public int AddReferenceCount => m_AddReferenceCount;

            /// <summary>
            /// 移除引用数量，表示当前引用集合中被从池中移除的引用对象实例的总数量
            /// </summary>
            public int RemoveReferenceCount => m_RemoveReferenceCount;

            /// <summary>
            /// 获取引用对象，返回一个指定类型的引用对象实例
            /// </summary>
            /// <typeparam name="T">要获取的引用对象类型</typeparam>
            /// <returns>获取的引用对象</returns>
            /// <exception cref="InvalidOperationException">当对象类型无效时抛出异常</exception>
            public T Acquire<T>() where T : class, IReference, new()
            {
                if (typeof(T) != m_ReferenceType)
                {
                    throw new InvalidOperationException("Type is invalid.");
                }

                m_UsingReferenceCount++;
                m_AcquireReferenceCount++;
                lock (m_References)
                {
                    if (m_References.Count > 0)
                    {
                        return (T)m_References.Dequeue();
                    }
                }

                m_AddReferenceCount++;
                return new T();
            }

            /// <summary>
            /// 获取引用对象，返回一个指定类型的引用对象实例
            /// </summary>
            /// <returns>获取的引用对象</returns>
            public IReference Acquire()
            {
                m_UsingReferenceCount++;
                m_AcquireReferenceCount++;
                lock (m_References)
                {
                    if (m_References.Count > 0)
                    {
                        return m_References.Dequeue();
                    }
                }

                m_AddReferenceCount++;
                return (IReference)Activator.CreateInstance(m_ReferenceType);
            }

            /// <summary>
            /// 释放引用对象，将一个引用对象实例返回到引用集合中，以便后续的获取操作能够复用该实例
            /// </summary>
            /// <param name="reference">要释放的引用对象</param>
            /// <exception cref="InvalidOperationException">当对象类型无效时抛出异常</exception>
            public void Release(IReference reference)
            {
                reference.Release();

                bool strictCheck = s_EnableStrictCheck;
                lock (m_References)
                {
                    if (strictCheck && m_References.Contains(reference))
                    {
                        throw new InvalidOperationException("The reference has already been released.");
                    }

                    m_References.Enqueue(reference);
                }

                m_ReleaseReferenceCount++;
                m_UsingReferenceCount--;
            }

            /// <summary>
            /// 添加引用对象，向引用集合中添加指定数量的引用对象实例，以便后续的获取操作能够复用这些实例
            /// </summary>
            /// <typeparam name="T">要添加的引用对象类型</typeparam>
            /// <param name="count">要添加的引用对象数量</param>
            /// <exception cref="InvalidOperationException">当对象类型无效时抛出异常</exception>
            public void Add<T>(int count) where T : class, IReference, new()
            {
                if (typeof(T) != m_ReferenceType)
                {
                    throw new InvalidOperationException("Type is invalid.");
                }

                lock (m_References)
                {
                    m_AddReferenceCount += count;
                    while (count-- > 0)
                    {
                        m_References.Enqueue(new T());
                    }
                }
            }

            /// <summary>
            /// 添加引用对象，向引用集合中添加指定数量的引用对象实例，以便后续的获取操作能够复用这些实例
            /// </summary>
            /// <param name="count">要添加的引用对象数量</param>
            public void Add(int count)
            {
                lock (m_References)
                {
                    m_AddReferenceCount += count;
                    while (count-- > 0)
                    {
                        m_References.Enqueue((IReference)Activator.CreateInstance(m_ReferenceType));
                    }
                }
            }

            /// <summary>
            /// 移除引用对象，从引用集合中移除指定数量的引用对象实例，以便释放资源并减少集合中的对象数量
            /// </summary>
            /// <param name="count">要移除的引用对象数量</param>
            public void Remove(int count)
            {
                lock (m_References)
                {
                    if (count > m_References.Count)
                    {
                        count = m_References.Count;
                    }

                    m_RemoveReferenceCount += count;
                    while (count-- > 0)
                    {
                        m_References.Dequeue();
                    }
                }
            }

            /// <summary>
            /// 移除所有引用对象，从引用集合中移除所有指定类型的引用对象实例，以便释放资源并清空集合中的对象数量
            /// </summary>
            public void RemoveAll()
            {
                lock (m_References)
                {
                    m_RemoveReferenceCount += m_References.Count;
                    m_References.Clear();
                }
            }
        }
    }
}