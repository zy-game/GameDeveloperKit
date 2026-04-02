using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 事件模块，提供事件的注册、触发和管理功能
    /// </summary>
    public sealed partial class EventModule : IGameFrameworkModule
    {
        private readonly Dictionary<EventRegistrationKey, List<IEventHandle>> _handlers = new();
        private readonly Dictionary<EventRegistrationKey, List<IAsyncEventHandle>> _asyncHandlers = new();
        private bool _bindingsInitialized;
        private bool _diagnosticsRegistered;
        private long _raiseCount;
        private long _handlerInvocationCount;
        private long _handlerFailureCount;
        private long _totalHandlerDurationMilliseconds;
        private string _lastEventName;
        private string _lastHandlerType;
        private string _lastError;

        /// <summary>
        /// 初始化事件模块并扫描事件绑定提供程序
        /// </summary>
        public EventModule()
        {
        }

        /// <summary>
        /// 获取已注册事件的数量
        /// </summary>
        public int EventCount => _handlers.Count + _asyncHandlers.Count;

        /// <summary>
        /// 获取或设置是否启用调试模式
        /// </summary>
        public bool DebugEnabled { get; set; } = true;

        /// <summary>
        /// 获取或设置在异步处理程序异常时是否继续执行
        /// </summary>
        public bool ContinueOnAsyncHandlerException { get; set; } = true;

        /// <summary>
        /// 注册事件处理程序
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="handler">事件处理程序</param>
        public void Register(string eventName, IEventHandle handler)
        {
            RegisterInternal(EventRegistrationKey.From(eventName), handler);
        }

        /// <summary>
        /// 注册事件处理程序
        /// </summary>
        /// <param name="eventId">事件ID</param>
        /// <param name="handler">事件处理程序</param>
        public void Register(int eventId, IEventHandle handler)
        {
            RegisterInternal(EventRegistrationKey.From(eventId), handler);
        }

        /// <summary>
        /// 注册事件处理程序
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="eventKey">事件键</param>
        /// <param name="handler">事件处理程序</param>
        public void Register<TEnum>(TEnum eventKey, IEventHandle handler)
            where TEnum : struct, Enum
        {
            RegisterInternal(EventRegistrationKey.From(eventKey), handler);
        }

        /// <summary>
        /// 注册事件处理程序
        /// </summary>
        /// <typeparam name="THandler">处理程序类型</typeparam>
        /// <param name="eventName">事件名称</param>
        public void Register<THandler>(string eventName)
            where THandler : class, IEventHandle, new()
        {
            RegisterByTypeInternal<THandler>(EventRegistrationKey.From(eventName));
        }

        /// <summary>
        /// 注册事件处理程序
        /// </summary>
        /// <typeparam name="THandler">处理程序类型</typeparam>
        /// <param name="eventId">事件ID</param>
        public void Register<THandler>(int eventId)
            where THandler : class, IEventHandle, new()
        {
            RegisterByTypeInternal<THandler>(EventRegistrationKey.From(eventId));
        }

        /// <summary>
        /// 注册事件处理程序
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <typeparam name="THandler">处理程序类型</typeparam>
        /// <param name="eventKey">事件键</param>
        public void Register<TEnum, THandler>(TEnum eventKey)
            where TEnum : struct, Enum
            where THandler : class, IEventHandle, new()
        {
            RegisterByTypeInternal<THandler>(EventRegistrationKey.From(eventKey));
        }

        /// <summary>
        /// 注册异步事件处理程序
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="handler">异步事件处理程序</param>
        public void RegisterAsync(string eventName, IAsyncEventHandle handler)
        {
            RegisterAsyncInternal(EventRegistrationKey.From(eventName), handler);
        }

        /// <summary>
        /// 注册异步事件处理程序
        /// </summary>
        /// <param name="eventId">事件ID</param>
        /// <param name="handler">异步事件处理程序</param>
        public void RegisterAsync(int eventId, IAsyncEventHandle handler)
        {
            RegisterAsyncInternal(EventRegistrationKey.From(eventId), handler);
        }

        /// <summary>
        /// 注册异步事件处理程序
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="eventKey">事件键</param>
        /// <param name="handler">异步事件处理程序</param>
        public void RegisterAsync<TEnum>(TEnum eventKey, IAsyncEventHandle handler)
            where TEnum : struct, Enum
        {
            RegisterAsyncInternal(EventRegistrationKey.From(eventKey), handler);
        }

        /// <summary>
        /// 注册异步事件处理程序
        /// </summary>
        /// <typeparam name="THandler">处理程序类型</typeparam>
        /// <param name="eventName">事件名称</param>
        public void RegisterAsync<THandler>(string eventName)
            where THandler : class, IAsyncEventHandle, new()
        {
            RegisterAsyncByTypeInternal<THandler>(EventRegistrationKey.From(eventName));
        }

        /// <summary>
        /// 注册异步事件处理程序
        /// </summary>
        /// <typeparam name="THandler">处理程序类型</typeparam>
        /// <param name="eventId">事件ID</param>
        public void RegisterAsync<THandler>(int eventId)
            where THandler : class, IAsyncEventHandle, new()
        {
            RegisterAsyncByTypeInternal<THandler>(EventRegistrationKey.From(eventId));
        }

        /// <summary>
        /// 注册异步事件处理程序
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <typeparam name="THandler">处理程序类型</typeparam>
        /// <param name="eventKey">事件键</param>
        public void RegisterAsync<TEnum, THandler>(TEnum eventKey)
            where TEnum : struct, Enum
            where THandler : class, IAsyncEventHandle, new()
        {
            RegisterAsyncByTypeInternal<THandler>(EventRegistrationKey.From(eventKey));
        }

        /// <summary>
        /// 注销事件处理程序
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="handler">事件处理程序</param>
        /// <returns>如果注销成功返回true，否则返回false</returns>
        public bool Unregister(string eventName, IEventHandle handler)
        {
            return UnregisterInternal(EventRegistrationKey.From(eventName), handler);
        }

        /// <summary>
        /// 注销事件处理程序
        /// </summary>
        /// <param name="eventId">事件ID</param>
        /// <param name="handler">事件处理程序</param>
        /// <returns>如果注销成功返回true，否则返回false</returns>
        public bool Unregister(int eventId, IEventHandle handler)
        {
            return UnregisterInternal(EventRegistrationKey.From(eventId), handler);
        }

        /// <summary>
        /// 注销事件处理程序
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="eventKey">事件键</param>
        /// <param name="handler">事件处理程序</param>
        /// <returns>如果注销成功返回true，否则返回false</returns>
        public bool Unregister<TEnum>(TEnum eventKey, IEventHandle handler)
            where TEnum : struct, Enum
        {
            return UnregisterInternal(EventRegistrationKey.From(eventKey), handler);
        }

        /// <summary>
        /// 注销事件处理程序
        /// </summary>
        /// <typeparam name="THandler">处理程序类型</typeparam>
        /// <param name="eventName">事件名称</param>
        /// <returns>如果注销成功返回true，否则返回false</returns>
        public bool Unregister<THandler>(string eventName)
            where THandler : class, IEventHandle
        {
            return UnregisterByTypeInternal<THandler>(EventRegistrationKey.From(eventName));
        }

        /// <summary>
        /// 注销事件处理程序
        /// </summary>
        /// <typeparam name="THandler">处理程序类型</typeparam>
        /// <param name="eventId">事件ID</param>
        /// <returns>如果注销成功返回true，否则返回false</returns>
        public bool Unregister<THandler>(int eventId)
            where THandler : class, IEventHandle
        {
            return UnregisterByTypeInternal<THandler>(EventRegistrationKey.From(eventId));
        }

        /// <summary>
        /// 注销事件处理程序
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <typeparam name="THandler">处理程序类型</typeparam>
        /// <param name="eventKey">事件键</param>
        /// <returns>如果注销成功返回true，否则返回false</returns>
        public bool Unregister<TEnum, THandler>(TEnum eventKey)
            where TEnum : struct, Enum
            where THandler : class, IEventHandle
        {
            return UnregisterByTypeInternal<THandler>(EventRegistrationKey.From(eventKey));
        }

        /// <summary>
        /// 注销异步事件处理程序
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="handler">异步事件处理程序</param>
        /// <returns>如果注销成功返回true，否则返回false</returns>
        public bool UnregisterAsync(string eventName, IAsyncEventHandle handler)
        {
            return UnregisterAsyncInternal(EventRegistrationKey.From(eventName), handler);
        }

        /// <summary>
        /// 注销异步事件处理程序
        /// </summary>
        /// <param name="eventId">事件ID</param>
        /// <param name="handler">异步事件处理程序</param>
        /// <returns>如果注销成功返回true，否则返回false</returns>
        public bool UnregisterAsync(int eventId, IAsyncEventHandle handler)
        {
            return UnregisterAsyncInternal(EventRegistrationKey.From(eventId), handler);
        }

        /// <summary>
        /// 注销异步事件处理程序
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="eventKey">事件键</param>
        /// <param name="handler">异步事件处理程序</param>
        /// <returns>如果注销成功返回true，否则返回false</returns>
        public bool UnregisterAsync<TEnum>(TEnum eventKey, IAsyncEventHandle handler)
            where TEnum : struct, Enum
        {
            return UnregisterAsyncInternal(EventRegistrationKey.From(eventKey), handler);
        }

        /// <summary>
        /// 注销异步事件处理程序
        /// </summary>
        /// <typeparam name="THandler">处理程序类型</typeparam>
        /// <param name="eventName">事件名称</param>
        /// <returns>如果注销成功返回true，否则返回false</returns>
        public bool UnregisterAsync<THandler>(string eventName)
            where THandler : class, IAsyncEventHandle
        {
            return UnregisterAsyncByTypeInternal<THandler>(EventRegistrationKey.From(eventName));
        }

        /// <summary>
        /// 注销异步事件处理程序
        /// </summary>
        /// <typeparam name="THandler">处理程序类型</typeparam>
        /// <param name="eventId">事件ID</param>
        /// <returns>如果注销成功返回true，否则返回false</returns>
        public bool UnregisterAsync<THandler>(int eventId)
            where THandler : class, IAsyncEventHandle
        {
            return UnregisterAsyncByTypeInternal<THandler>(EventRegistrationKey.From(eventId));
        }

        /// <summary>
        /// 注销异步事件处理程序
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <typeparam name="THandler">处理程序类型</typeparam>
        /// <param name="eventKey">事件键</param>
        /// <returns>如果注销成功返回true，否则返回false</returns>
        public bool UnregisterAsync<TEnum, THandler>(TEnum eventKey)
            where TEnum : struct, Enum
            where THandler : class, IAsyncEventHandle
        {
            return UnregisterAsyncByTypeInternal<THandler>(EventRegistrationKey.From(eventKey));
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="args">事件参数</param>
        public void Raise(string eventName, object sender = null, params object[] args)
        {
            RaiseInternal(EventRegistrationKey.From(eventName), sender, CancellationToken.None, args);
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="sender">事件发送者</param>
        public void Raise(string eventName, object sender = null)
        {
            RaiseInternal(EventRegistrationKey.From(eventName), sender, CancellationToken.None);
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        /// <typeparam name="TArg0">第一个参数类型</typeparam>
        /// <param name="eventName">事件名称</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="arg0">第一个事件参数</param>
        public void Raise<TArg0>(string eventName, object sender, TArg0 arg0)
        {
            RaiseInternal(EventRegistrationKey.From(eventName), sender, CancellationToken.None, arg0);
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        /// <typeparam name="TArg0">第一个参数类型</typeparam>
        /// <typeparam name="TArg1">第二个参数类型</typeparam>
        /// <param name="eventName">事件名称</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="arg0">第一个事件参数</param>
        /// <param name="arg1">第二个事件参数</param>
        public void Raise<TArg0, TArg1>(string eventName, object sender, TArg0 arg0, TArg1 arg1)
        {
            RaiseInternal(EventRegistrationKey.From(eventName), sender, CancellationToken.None, arg0, arg1);
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        /// <param name="eventId">事件ID</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="args">事件参数</param>
        public void Raise(int eventId, object sender = null, params object[] args)
        {
            RaiseInternal(EventRegistrationKey.From(eventId), sender, CancellationToken.None, args);
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        /// <param name="eventId">事件ID</param>
        /// <param name="sender">事件发送者</param>
        public void Raise(int eventId, object sender = null)
        {
            RaiseInternal(EventRegistrationKey.From(eventId), sender, CancellationToken.None);
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        /// <typeparam name="TArg0">第一个参数类型</typeparam>
        /// <param name="eventId">事件ID</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="arg0">第一个事件参数</param>
        public void Raise<TArg0>(int eventId, object sender, TArg0 arg0)
        {
            RaiseInternal(EventRegistrationKey.From(eventId), sender, CancellationToken.None, arg0);
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        /// <typeparam name="TArg0">第一个参数类型</typeparam>
        /// <typeparam name="TArg1">第二个参数类型</typeparam>
        /// <param name="eventId">事件ID</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="arg0">第一个事件参数</param>
        /// <param name="arg1">第二个事件参数</param>
        public void Raise<TArg0, TArg1>(int eventId, object sender, TArg0 arg0, TArg1 arg1)
        {
            RaiseInternal(EventRegistrationKey.From(eventId), sender, CancellationToken.None, arg0, arg1);
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="eventKey">事件键</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="args">事件参数</param>
        public void Raise<TEnum>(TEnum eventKey, object sender = null, params object[] args)
            where TEnum : struct, Enum
        {
            RaiseInternal(EventRegistrationKey.From(eventKey), sender, CancellationToken.None, args);
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="eventKey">事件键</param>
        /// <param name="sender">事件发送者</param>
        public void Raise<TEnum>(TEnum eventKey, object sender = null)
            where TEnum : struct, Enum
        {
            RaiseInternal(EventRegistrationKey.From(eventKey), sender, CancellationToken.None);
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <typeparam name="TArg0">第一个参数类型</typeparam>
        /// <param name="eventKey">事件键</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="arg0">第一个事件参数</param>
        public void Raise<TEnum, TArg0>(TEnum eventKey, object sender, TArg0 arg0)
            where TEnum : struct, Enum
        {
            RaiseInternal(EventRegistrationKey.From(eventKey), sender, CancellationToken.None, arg0);
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <typeparam name="TArg0">第一个参数类型</typeparam>
        /// <typeparam name="TArg1">第二个参数类型</typeparam>
        /// <param name="eventKey">事件键</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="arg0">第一个事件参数</param>
        /// <param name="arg1">第二个事件参数</param>
        public void Raise<TEnum, TArg0, TArg1>(TEnum eventKey, object sender, TArg0 arg0, TArg1 arg1)
            where TEnum : struct, Enum
        {
            RaiseInternal(EventRegistrationKey.From(eventKey), sender, CancellationToken.None, arg0, arg1);
        }

        /// <summary>
        /// 异步触发事件
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="args">事件参数</param>
        /// <returns>异步任务</returns>
        public UniTask RaiseAsync(string eventName, object sender = null, params object[] args)
        {
            return RaiseAsyncInternal(EventRegistrationKey.From(eventName), sender, CancellationToken.None, args);
        }

        /// <summary>
        /// 异步触发事件
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="sender">事件发送者</param>
        /// <returns>异步任务</returns>
        public UniTask RaiseAsync(string eventName, object sender = null)
        {
            return RaiseAsyncInternal(EventRegistrationKey.From(eventName), sender, CancellationToken.None);
        }

        /// <summary>
        /// 异步触发事件
        /// </summary>
        /// <typeparam name="TArg0">第一个参数类型</typeparam>
        /// <param name="eventName">事件名称</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="arg0">第一个事件参数</param>
        /// <returns>异步任务</returns>
        public UniTask RaiseAsync<TArg0>(string eventName, object sender, TArg0 arg0)
        {
            return RaiseAsyncInternal(EventRegistrationKey.From(eventName), sender, CancellationToken.None, arg0);
        }

        /// <summary>
        /// 异步触发事件
        /// </summary>
        /// <typeparam name="TArg0">第一个参数类型</typeparam>
        /// <typeparam name="TArg1">第二个参数类型</typeparam>
        /// <param name="eventName">事件名称</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="arg0">第一个事件参数</param>
        /// <param name="arg1">第二个事件参数</param>
        /// <returns>异步任务</returns>
        public UniTask RaiseAsync<TArg0, TArg1>(string eventName, object sender, TArg0 arg0, TArg1 arg1)
        {
            return RaiseAsyncInternal(EventRegistrationKey.From(eventName), sender, CancellationToken.None, arg0, arg1);
        }

        /// <summary>
        /// 异步触发事件
        /// </summary>
        /// <param name="eventId">事件ID</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="args">事件参数</param>
        /// <returns>异步任务</returns>
        public UniTask RaiseAsync(int eventId, object sender = null, params object[] args)
        {
            return RaiseAsyncInternal(EventRegistrationKey.From(eventId), sender, CancellationToken.None, args);
        }

        /// <summary>
        /// 异步触发事件
        /// </summary>
        /// <param name="eventId">事件ID</param>
        /// <param name="sender">事件发送者</param>
        /// <returns>异步任务</returns>
        public UniTask RaiseAsync(int eventId, object sender = null)
        {
            return RaiseAsyncInternal(EventRegistrationKey.From(eventId), sender, CancellationToken.None);
        }

        /// <summary>
        /// 异步触发事件
        /// </summary>
        /// <typeparam name="TArg0">第一个参数类型</typeparam>
        /// <param name="eventId">事件ID</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="arg0">第一个事件参数</param>
        /// <returns>异步任务</returns>
        public UniTask RaiseAsync<TArg0>(int eventId, object sender, TArg0 arg0)
        {
            return RaiseAsyncInternal(EventRegistrationKey.From(eventId), sender, CancellationToken.None, arg0);
        }

        /// <summary>
        /// 异步触发事件
        /// </summary>
        /// <typeparam name="TArg0">第一个参数类型</typeparam>
        /// <typeparam name="TArg1">第二个参数类型</typeparam>
        /// <param name="eventId">事件ID</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="arg0">第一个事件参数</param>
        /// <param name="arg1">第二个事件参数</param>
        /// <returns>异步任务</returns>
        public UniTask RaiseAsync<TArg0, TArg1>(int eventId, object sender, TArg0 arg0, TArg1 arg1)
        {
            return RaiseAsyncInternal(EventRegistrationKey.From(eventId), sender, CancellationToken.None, arg0, arg1);
        }

        /// <summary>
        /// 异步触发事件
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="eventKey">事件键</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="args">事件参数</param>
        /// <returns>异步任务</returns>
        public UniTask RaiseAsync<TEnum>(TEnum eventKey, object sender = null, params object[] args)
            where TEnum : struct, Enum
        {
            return RaiseAsyncInternal(EventRegistrationKey.From(eventKey), sender, CancellationToken.None, args);
        }

        /// <summary>
        /// 异步触发事件
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="eventKey">事件键</param>
        /// <param name="sender">事件发送者</param>
        /// <returns>异步任务</returns>
        public UniTask RaiseAsync<TEnum>(TEnum eventKey, object sender = null)
            where TEnum : struct, Enum
        {
            return RaiseAsyncInternal(EventRegistrationKey.From(eventKey), sender, CancellationToken.None);
        }

        /// <summary>
        /// 异步触发事件
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <typeparam name="TArg0">第一个参数类型</typeparam>
        /// <param name="eventKey">事件键</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="arg0">第一个事件参数</param>
        /// <returns>异步任务</returns>
        public UniTask RaiseAsync<TEnum, TArg0>(TEnum eventKey, object sender, TArg0 arg0)
            where TEnum : struct, Enum
        {
            return RaiseAsyncInternal(EventRegistrationKey.From(eventKey), sender, CancellationToken.None, arg0);
        }

        /// <summary>
        /// 异步触发事件
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <typeparam name="TArg0">第一个参数类型</typeparam>
        /// <typeparam name="TArg1">第二个参数类型</typeparam>
        /// <param name="eventKey">事件键</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="arg0">第一个事件参数</param>
        /// <param name="arg1">第二个事件参数</param>
        /// <returns>异步任务</returns>
        public UniTask RaiseAsync<TEnum, TArg0, TArg1>(TEnum eventKey, object sender, TArg0 arg0, TArg1 arg1)
            where TEnum : struct, Enum
        {
            return RaiseAsyncInternal(EventRegistrationKey.From(eventKey), sender, CancellationToken.None, arg0, arg1);
        }

        /// <summary>
        /// 扫描并注册事件绑定提供程序
        /// </summary>
        public void ScanAndRegisterBindings()
        {
            if (_bindingsInitialized)
            {
                return;
            }

            _bindingsInitialized = true;
            EventBindingScanner.ScanAndRegister(this);
        }

        /// <summary>
        /// 重新扫描并注册事件绑定提供程序
        /// </summary>
        public void RescanAndRegisterBindings()
        {
            _bindingsInitialized = false;
            ScanAndRegisterBindings();
        }

        /// <summary>
        /// 释放事件模块持有的资源
        /// </summary>
        public void Dispose()
        {
            RemoveDiagnosticsSnapshotProviders();
            _handlers.Clear();
            _asyncHandlers.Clear();
            _bindingsInitialized = false;
        }

        private void RegisterInternal(EventRegistrationKey key, IEventHandle handler)
        {
            EnsureBindingsInitialized();

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var handlers = GetOrCreateHandlers(key);
            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
            }

            EnsureDiagnosticsSnapshotProviders();
        }

        private void RegisterByTypeInternal<THandler>(EventRegistrationKey key)
            where THandler : class, IEventHandle, new()
        {
            EnsureBindingsInitialized();

            var handlers = GetOrCreateHandlers(key);
            if (ContainsHandlerType<THandler>(handlers))
            {
                return;
            }

            handlers.Add(new THandler());
            EnsureDiagnosticsSnapshotProviders();
        }

        private void RegisterAsyncInternal(EventRegistrationKey key, IAsyncEventHandle handler)
        {
            EnsureBindingsInitialized();

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var handlers = GetOrCreateAsyncHandlers(key);
            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
            }

            EnsureDiagnosticsSnapshotProviders();
        }

        private void RegisterAsyncByTypeInternal<THandler>(EventRegistrationKey key)
            where THandler : class, IAsyncEventHandle, new()
        {
            EnsureBindingsInitialized();

            var handlers = GetOrCreateAsyncHandlers(key);
            if (ContainsHandlerType<THandler>(handlers))
            {
                return;
            }

            handlers.Add(new THandler());
            EnsureDiagnosticsSnapshotProviders();
        }

        private bool UnregisterInternal(EventRegistrationKey key, IEventHandle handler)
        {
            EnsureBindingsInitialized();
            return RemoveHandler(_handlers, key, handler);
        }

        private bool UnregisterByTypeInternal<THandler>(EventRegistrationKey key)
            where THandler : class, IEventHandle
        {
            EnsureBindingsInitialized();
            return RemoveHandlerByType<THandler, IEventHandle>(_handlers, key);
        }

        private bool UnregisterAsyncInternal(EventRegistrationKey key, IAsyncEventHandle handler)
        {
            EnsureBindingsInitialized();
            return RemoveHandler(_asyncHandlers, key, handler);
        }

        private bool UnregisterAsyncByTypeInternal<THandler>(EventRegistrationKey key)
            where THandler : class, IAsyncEventHandle
        {
            EnsureBindingsInitialized();
            return RemoveHandlerByType<THandler, IAsyncEventHandle>(_asyncHandlers, key);
        }

        private void EnsureBindingsInitialized()
        {
            if (_bindingsInitialized)
            {
                return;
            }

            ScanAndRegisterBindings();
        }

        private List<IEventHandle> GetOrCreateHandlers(EventRegistrationKey key)
        {
            if (!_handlers.TryGetValue(key, out var handlers))
            {
                handlers = new List<IEventHandle>();
                _handlers.Add(key, handlers);
            }

            return handlers;
        }

        private List<IAsyncEventHandle> GetOrCreateAsyncHandlers(EventRegistrationKey key)
        {
            if (!_asyncHandlers.TryGetValue(key, out var handlers))
            {
                handlers = new List<IAsyncEventHandle>();
                _asyncHandlers.Add(key, handlers);
            }

            return handlers;
        }

        private static bool ContainsHandlerType<THandler>(List<IEventHandle> handlers)
            where THandler : class, IEventHandle
        {
            for (var i = 0; i < handlers.Count; i++)
            {
                if (handlers[i] is THandler)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsHandlerType<THandler>(List<IAsyncEventHandle> handlers)
            where THandler : class, IAsyncEventHandle
        {
            for (var i = 0; i < handlers.Count; i++)
            {
                if (handlers[i] is THandler)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RemoveHandler<THandler>(Dictionary<EventRegistrationKey, List<THandler>> dictionary, EventRegistrationKey key, THandler handler)
            where THandler : class
        {
            if (handler == null)
            {
                return false;
            }

            if (!dictionary.TryGetValue(key, out var handlers))
            {
                return false;
            }

            var removed = handlers.Remove(handler);
            if (handlers.Count == 0)
            {
                dictionary.Remove(key);
            }

            return removed;
        }

        private static bool RemoveHandlerByType<TTarget, THandler>(Dictionary<EventRegistrationKey, List<THandler>> dictionary, EventRegistrationKey key)
            where TTarget : class
            where THandler : class
        {
            if (!dictionary.TryGetValue(key, out var handlers))
            {
                return false;
            }

            for (var i = 0; i < handlers.Count; i++)
            {
                if (handlers[i] is TTarget)
                {
                    handlers.RemoveAt(i);
                    if (handlers.Count == 0)
                    {
                        dictionary.Remove(key);
                    }

                    return true;
                }
            }

            return false;
        }

    }
}

