using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit;
using GameDeveloperKit.Audio;
using GameDeveloperKit.Config;
using GameDeveloperKit.Data;
using GameDeveloperKit.Events;
using GameDeveloperKit.Files;
using GameDeveloperKit.Grid;
using GameDeveloperKit.Log;
using GameDeveloperKit.Network;
using GameDeveloperKit.Procedure;
using GameDeveloperKit.Resource;
using GameDeveloperKit.UI;
using GameDeveloperKit.World;
using UnityEngine;
using ZLinq;
using ILogger = GameDeveloperKit.Log.ILogger;


/// <summary>
/// 游戏框架
/// </summary>
public static class Game
{
    private static readonly Dictionary<Type, IModule> Modules = new Dictionary<Type, IModule>();

    /// <summary>
    /// 日志器（兼容旧接口）
    /// </summary>
    public static LoggerModule Debug { get; private set; }

    /// <summary>
    /// VFS模块
    /// </summary>
    public static IFileManager File { get; private set; }

    /// <summary>
    /// 资源模块
    /// </summary>
    public static IResourceManager Resource { get; private set; }

    /// <summary>
    /// 事件模块
    /// </summary>
    public static IEventManager Event { get; private set; }

    /// <summary>
    /// 流程模块
    /// </summary>
    public static IProcedureManager Procedure { get; private set; }

    /// <summary>
    /// Web请求模块
    /// </summary>
    public static IWebManager Web { get; private set; }

    /// <summary>
    /// 下载模块
    /// </summary>
    public static IDownloadManager Download { get; private set; }

    /// <summary>
    /// 网络模块
    /// </summary>
    public static INetworkManager Network { get; private set; }

    /// <summary>
    /// UI模块
    /// </summary>
    public static IUIManager UI { get; private set; }

    /// <summary>
    /// 世界模块
    /// </summary>
    public static IWorldManager World { get; private set; }

    /// <summary>
    /// 音效模块
    /// </summary>
    public static IAudioManager Audio { get; private set; }

    /// <summary>
    /// 配置模块
    /// </summary>
    public static IConfigManager Config { get; private set; }

    /// <summary>
    /// 数据模块
    /// </summary>
    public static IDataManager Data { get; private set; }

    public static IGridManager Grid { get; private set; }

    /// <summary>
    /// 启动框架
    /// </summary>
    /// <param name="startup">Startup 实例，用于传递自定义 Procedure 配置</param>
    public static void Startup(Startup startup = null)
    {
        // 初始化日志模块（最先）
        AddModule(Debug = new LoggerModule("[GameFramework]", LogLevel.Debug));
        Debug.Info("GameFramework starting...");
        ReferencePool.Initialize();
        Debug.Info("ReferencePool initialized");
        AddModule(World = new WorldModule());
        AddModule(Event = new EventModule());
        AddModule(Procedure = new ProcedureManager());
        AddModule(File = new VFSModule());
        AddModule(Web = new WebModule());
        AddModule(Network = new NetworkModule());
        AddModule(Download = new DownloadModule());
        AddModule(Resource = new ResourceModule());
        AddModule(Audio = new AudioModule());
        AddModule(Config = new ConfigModule());
        AddModule(Data = new DataModule());
        AddModule(UI = new UIModule());
        AddModule(Grid = new GridModule());
        Debug.Info("GameFramework started successfully");
        Procedure.StartAsync<InitializeFrameworkProcedure>(args: startup);
    }

    /// <summary>
    /// 关闭框架
    /// </summary>
    public static void Shutdown()
    {
        foreach (var module in Modules.Values.AsValueEnumerable())
        {
            module.OnClearup();
        }

        Modules.Clear();
        Debug.Info("GameFramework shutdown successfully");
    }

    /// <summary>
    /// 添加模块
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static void AddModule<T>() where T : IModule
    {
        AddModule(Activator.CreateInstance<T>());
    }

    /// <summary>
    /// 添加模块
    /// </summary>
    /// <param name="module"></param>
    public static void AddModule(IModule module)
    {
        Modules[module.GetType()] = module;
        module.OnStartup();
    }

    /// <summary>
    /// 获取模块
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T GetModule<T>() where T : class, IModule
    {
        if (Modules.TryGetValue(typeof(T), out var module))
        {
            return module as T;
        }

        return null;
    }

    /// <summary>
    /// 移除模块
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static void RemoveModule<T>() where T : class, IModule
    {
        if (!Modules.TryGetValue(typeof(T), out var module)) return;
        module.OnClearup();
        Modules.Remove(typeof(T));
    }

    /// <summary>
    /// 更新框架
    /// </summary>
    public static void Update()
    {
        PerformanceMonitor.StartFrame();

        foreach (var module in Modules.Values.AsValueEnumerable())
        {
            module.OnUpdate(Time.deltaTime);
        }

        PerformanceMonitor.EndFrame();
    }
}