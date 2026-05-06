using System;
using System.Collections.Generic;

namespace GameDeveloperKit
{
    public static class ReferencePool
    {
        private static readonly Dictionary<Type, ReferenceCollection> s_ReferenceCollections = new Dictionary<Type, ReferenceCollection>();
        private static bool s_EnableStrictCheck;

        public static bool EnableStrictCheck
        {
            get => s_EnableStrictCheck;
            set => s_EnableStrictCheck = value;
        }

        public static int Count => s_ReferenceCollections.Count;

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

        public static T Acquire<T>() where T : class, IReference, new()
        {
            return GetReferenceCollection(typeof(T)).Acquire<T>();
        }

        public static IReference Acquire(Type referenceType)
        {
            InternalCheckReferenceType(referenceType);
            return GetReferenceCollection(referenceType).Acquire();
        }

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

        public static void Add<T>(int count) where T : class, IReference, new()
        {
            GetReferenceCollection(typeof(T)).Add<T>(count);
        }

        public static void Add(Type referenceType, int count)
        {
            InternalCheckReferenceType(referenceType);
            GetReferenceCollection(referenceType).Add(count);
        }

        public static void Remove<T>(int count) where T : class, IReference
        {
            GetReferenceCollection(typeof(T)).Remove(count);
        }

        public static void Remove(Type referenceType, int count)
        {
            InternalCheckReferenceType(referenceType);
            GetReferenceCollection(referenceType).Remove(count);
        }

        public static void RemoveAll<T>() where T : class, IReference
        {
            GetReferenceCollection(typeof(T)).RemoveAll();
        }

        public static void RemoveAll(Type referenceType)
        {
            InternalCheckReferenceType(referenceType);
            GetReferenceCollection(referenceType).RemoveAll();
        }

        public static PoolInfo GetPoolInfo<T>() where T : class, IReference
        {
            return GetPoolInfo(typeof(T));
        }

        public static PoolInfo GetPoolInfo(Type referenceType)
        {
            InternalCheckReferenceType(referenceType);
            var collection = GetReferenceCollection(referenceType);
            return new PoolInfo(
                referenceType,
                collection.UnusedReferenceCount,
                collection.UsingReferenceCount,
                collection.AcquireReferenceCount,
                collection.ReleaseReferenceCount,
                collection.AddReferenceCount,
                collection.RemoveReferenceCount
            );
        }

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

        public readonly struct PoolInfo
        {
            public readonly Type Type;
            public readonly int UnusedCount;
            public readonly int UsingCount;
            public readonly int AcquireCount;
            public readonly int ReleaseCount;
            public readonly int AddCount;
            public readonly int RemoveCount;

            internal PoolInfo(Type type, int unusedCount, int usingCount, int acquireCount, int releaseCount, int addCount, int removeCount)
            {
                Type = type;
                UnusedCount = unusedCount;
                UsingCount = usingCount;
                AcquireCount = acquireCount;
                ReleaseCount = releaseCount;
                AddCount = addCount;
                RemoveCount = removeCount;
            }
        }

        private sealed class ReferenceCollection
        {
            private readonly Queue<IReference> m_References = new Queue<IReference>();
            private readonly Type m_ReferenceType;
            private int m_UsingReferenceCount;
            private int m_AcquireReferenceCount;
            private int m_ReleaseReferenceCount;
            private int m_AddReferenceCount;
            private int m_RemoveReferenceCount;

            public ReferenceCollection(Type referenceType)
            {
                m_ReferenceType = referenceType;
            }

            public int UnusedReferenceCount => m_References.Count;

            public int UsingReferenceCount => m_UsingReferenceCount;

            public int AcquireReferenceCount => m_AcquireReferenceCount;

            public int ReleaseReferenceCount => m_ReleaseReferenceCount;

            public int AddReferenceCount => m_AddReferenceCount;

            public int RemoveReferenceCount => m_RemoveReferenceCount;

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
