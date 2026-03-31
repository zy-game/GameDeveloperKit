using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 定义具有生命周期管理的游戏框架模块接口。
    /// </summary>
    /// <remarks>
    /// 扩展自 IGameFrameworkModule，添加了模块初始化状态和异步初始化/关闭方法。
    /// 所有需要异步初始化和清理的框架模块都应实现此接口。
    /// </remarks>
    public interface IGameFrameworkLifecycleModule : IGameFrameworkModule
    {
        /// <summary>
        /// 获取模块的当前状态。
        /// </summary>
        /// <remarks>
        /// 用于标识模块是否已经完成初始化。
        /// 可用于避免重复初始化。
        /// </remarks>
        bool IsInitialized { get; }

        /// <summary>
        /// 异步初始化模块。
        /// </summary>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>表示异步初始化操作的任务。</returns>
        /// <remarks>
        /// 此方法执行模块的初始化逻辑，包括创建内部状态、加载资源和注册服务等。
        /// 初始化完成后应将该值设置为 true。
        /// 如果初始化失败，应保持该值为 false。
        /// </remarks>
        UniTask InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步关闭模块。
        /// </summary>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>表示异步关闭操作的任务。</returns>
        /// <remarks>
        /// 此方法执行模块的清理逻辑，包括释放资源、取消挂起的操作和保存状态等。
        /// 关闭完成后应将该值设置为 false。
        /// 即使关闭过程中出现异常，也应该尽可能释放资源。
        /// </remarks>
        UniTask ShutdownAsync(CancellationToken cancellationToken = default);
    }
}

