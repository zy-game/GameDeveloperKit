using System;
using Massive;

namespace GameDeveloperKit.World
{
    /// <summary>
    /// World数据上下文
    /// 封装Massive ECS的数据层，提供实体和组件的基础操作
    /// </summary>
    public sealed class WorldContext
    {
        private readonly MassiveWorld _massiveWorld;

        public WorldContext()
        {
            _massiveWorld = new MassiveWorld();
        }

        #region Entity Operations

        /// <summary>
        /// 创建实体
        /// </summary>
        public int CreateEntity()
        {
            return _massiveWorld.Create();
        }

        /// <summary>
        /// 销毁实体
        /// </summary>
        public void DestroyEntity(int entityId)
        {
            _massiveWorld.Destroy(entityId);
        }

        /// <summary>
        /// 检查实体是否存活
        /// </summary>
        public bool IsAlive(int entityId)
        {
            return _massiveWorld.IsAlive(entityId);
        }

        #endregion

        #region Component Operations

        /// <summary>
        /// 设置组件（添加或更新）
        /// </summary>
        public void SetComponent<T>(int entityId, T component) where T : struct, IComponent
        {
            _massiveWorld.Set(entityId, component);
        }

        /// <summary>
        /// 获取组件引用
        /// </summary>
        public ref T GetComponent<T>(int entityId) where T : struct, IComponent
        {
            return ref _massiveWorld.Get<T>(entityId);
        }

        /// <summary>
        /// 检查是否拥有组件
        /// </summary>
        public bool HasComponent<T>(int entityId) where T : struct, IComponent
        {
            return _massiveWorld.Has<T>(entityId);
        }

        /// <summary>
        /// 移除组件
        /// </summary>
        public void RemoveComponent<T>(int entityId) where T : struct, IComponent
        {
            _massiveWorld.Remove<T>(entityId);
        }

        #endregion

        #region Query API

        /// <summary>
        /// 查询拥有指定组件的实体
        /// </summary>
        public void Include<T1>(IdActionRef<T1> action) where T1 : struct, IComponent
        {
            var query = _massiveWorld.Include<T1>();
            query.ForEach(action);
        }

        /// <summary>
        /// 查询拥有指定组件的实体
        /// </summary>
        public void Include<T1, T2>(IdActionRef<T1, T2> action)
            where T1 : struct, IComponent
            where T2 : struct, IComponent
        {
            var query = _massiveWorld.Include<T1, T2>();
            query.ForEach(action);
        }

        /// <summary>
        /// 查询拥有指定组件的实体
        /// </summary>
        public void Include<T1, T2, T3>(IdActionRef<T1, T2, T3> action)
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where T3 : struct, IComponent
        {
            var query = _massiveWorld.Include<T1, T2, T3>();
            query.ForEach(action);
        }

        /// <summary>
        /// 查询拥有指定组件的实体（4个组件）
        /// </summary>
        public void Include<T1, T2, T3, T4>(IdActionRef<T1, T2, T3, T4> action)
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where T3 : struct, IComponent
            where T4 : struct, IComponent
        {
            var set1 = _massiveWorld.DataSet<T1>();
            var set2 = _massiveWorld.DataSet<T2>();
            var set3 = _massiveWorld.DataSet<T3>();
            var set4 = _massiveWorld.DataSet<T4>();

            var filter = new Filter(
                new SparseSet[] { set1, set2, set3, set4 },
                System.Array.Empty<SparseSet>()
            );
            var query = _massiveWorld.Filter(filter);
            query.ForEach(action);
        }

        /// <summary>
        /// 查询不拥有指定组件的实体
        /// </summary>
        public void Excluded<T1>(IdActionRef<T1> action) where T1 : struct, IComponent
        {
            var query = _massiveWorld.Exclude<T1>();
            query.ForEach(action);
        }

        /// <summary>
        /// 查询不拥有指定组件的实体
        /// </summary>
        public void Excluded<T1, T2>(IdActionRef<T1, T2> action)
            where T1 : struct, IComponent
            where T2 : struct, IComponent
        {
            var query = _massiveWorld.Exclude<T1, T2>();
            query.ForEach(action);
        }

        /// <summary>
        /// 查询不拥有指定组件的实体
        /// </summary>
        public void Excluded<T1, T2, T3>(IdActionRef<T1, T2, T3> action)
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where T3 : struct, IComponent
        {
            var query = _massiveWorld.Exclude<T1, T2, T3>();
            query.ForEach(action);
        }

        /// <summary>
        /// 查询不拥有指定组件的实体（4个组件）
        /// </summary>
        public void Excluded<T1, T2, T3, T4>(IdActionRef<T1, T2, T3, T4> action)
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where T3 : struct, IComponent
            where T4 : struct, IComponent
        {
            var set1 = _massiveWorld.DataSet<T1>();
            var set2 = _massiveWorld.DataSet<T2>();
            var set3 = _massiveWorld.DataSet<T3>();
            var set4 = _massiveWorld.DataSet<T4>();

            var filter = new Filter(
                System.Array.Empty<SparseSet>(),
                new SparseSet[] { set1, set2, set3, set4 }
            );
            var query = _massiveWorld.Filter(filter);
            query.ForEach(action);
        }

        public Query Query(Filter filter) => _massiveWorld.Filter(filter);

        /// <summary>
        /// 使用Selector构建Filter
        /// </summary>
        public Query Query(IIncludeSelector includeSelector, IExcludeSelector excludeSelector)
        {
            var includeSets = includeSelector?.Select(_massiveWorld.Sets) ?? Array.Empty<SparseSet>();
            var excludeSets = excludeSelector?.Select(_massiveWorld.Sets) ?? Array.Empty<SparseSet>();
            return Query(new Filter(includeSets, excludeSets));
        }

        /// <summary>
        /// 获取Selector对应的组件类型数组
        /// </summary>
        public Type[] GetSelectorTypes(ISetSelector selector)
        {
            if (selector == null)
                return Array.Empty<Type>();

            var sets = selector.Select(_massiveWorld.Sets);
            var types = new Type[sets.Length];
            for (int i = 0; i < sets.Length; i++)
            {
                types[i] = sets[i].GetType().GenericTypeArguments.Length > 0
                    ? sets[i].GetType().GenericTypeArguments[0]
                    : typeof(object);
            }
            return types;
        }

        #endregion

        /// <summary>
        /// 清空所有数据
        /// </summary>
        public void Clear()
        {
            _massiveWorld.Clear();
        }
    }
}