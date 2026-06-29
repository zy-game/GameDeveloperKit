using System;

namespace GameDeveloperKit
{
    /// <summary>
    /// 模块依赖元数据，用于声明模块启动前置依赖。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class ModuleDependencyAttribute : DependencyAttribute
    {
        /// <summary>
        /// 初始化模块依赖元数据。
        /// </summary>
        /// <param name="moduleType">依赖模块类型。</param>
        /// <exception cref="GameException">依赖类型不是模块类型时抛出。</exception>
        public ModuleDependencyAttribute(Type moduleType) : base(moduleType)
        {
            if (!typeof(IGameModule).IsAssignableFrom(moduleType))
            {
                throw new GameException($"Module dependency type '{moduleType.FullName}' must implement IGameModule.");
            }
        }
    }
}
