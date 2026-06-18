using System;

namespace GameDeveloperKit
{
    /// <summary>
    /// 依赖元数据基类，用于声明类型依赖。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public abstract class DependencyAttribute : Attribute
    {
        /// <summary>
        /// 初始化依赖元数据。
        /// </summary>
        /// <param name="dependencyType">依赖类型。</param>
        /// <exception cref="ArgumentNullException">依赖类型为空时抛出。</exception>
        protected DependencyAttribute(Type dependencyType)
        {
            if (dependencyType == null)
            {
                throw new ArgumentNullException(nameof(dependencyType));
            }

            DependencyType = dependencyType;
        }

        /// <summary>
        /// 依赖类型。
        /// </summary>
        public Type DependencyType { get; }
    }
}
