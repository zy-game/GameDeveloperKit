using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 框架组合类，管理游戏框架的所有模块及其初始化顺序。
    /// </summary>
    public sealed class FrameworkComposition
    {
        private static readonly string[] CoreModuleOrder =
        {
            nameof(DiagnosticsModule),
            nameof(DataModule),
            nameof(PlatformModule)
        };

        private static readonly string[] FeatureModuleOrder =
        {
            nameof(LocalizationModule),
            nameof(AudioModule),
            nameof(InputModule),
            nameof(UIModule),
            nameof(SceneModule),
            nameof(ProcedureModule)
        };

        /// <summary>
        /// 获取诊断模块。
        /// </summary>
        public DiagnosticsModule Diagnostics { get; private set; }

        /// <summary>
        /// 获取数据模块。
        /// </summary>
        public DataModule Data { get; private set; }

        /// <summary>
        /// 获取平台模块。
        /// </summary>
        public PlatformModule Platform { get; private set; }

        /// <summary>
        /// 获取资源模块。
        /// </summary>
        public ResourceModule Resource { get; private set; }

        /// <summary>
        /// 获取本地化模块。
        /// </summary>
        public LocalizationModule Localization { get; private set; }

        /// <summary>
        /// 获取音频模块。
        /// </summary>
        public AudioModule Audio { get; private set; }

        /// <summary>
        /// 获取输入模块。
        /// </summary>
        public InputModule Input { get; private set; }

        /// <summary>
        /// 获取UI模块。
        /// </summary>
        public UIModule UI { get; private set; }

        /// <summary>
        /// 获取场景模块。
        /// </summary>
        public SceneModule Scene { get; private set; }

        /// <summary>
        /// 获取流程模块。
        /// </summary>
        public ProcedureModule Procedure { get; private set; }

        /// <summary>
        /// 获取核心模块的初始化顺序。
        /// </summary>
        /// <returns>模块名称列表。</returns>
        public static IReadOnlyList<string> GetCoreModuleOrder()
        {
            return CoreModuleOrder;
        }

        /// <summary>
        /// 获取功能模块的初始化顺序。
        /// </summary>
        /// <param name="initializeUI">是否初始化UI模块。</param>
        /// <param name="initializeSceneModule">是否初始化场景模块。</param>
        /// <param name="initializeProcedureModule">是否初始化流程模块。</param>
        /// <returns>模块名称列表。</returns>
        public static IReadOnlyList<string> GetFeatureModuleOrder(bool initializeUI, bool initializeSceneModule, bool initializeProcedureModule)
        {
            var results = new List<string>(FeatureModuleOrder.Length);
            results.Add(nameof(LocalizationModule));
            results.Add(nameof(AudioModule));
            results.Add(nameof(InputModule));

            if (initializeUI)
            {
                results.Add(nameof(UIModule));
            }

            if (initializeSceneModule)
            {
                results.Add(nameof(SceneModule));
            }

            if (initializeProcedureModule)
            {
                results.Add(nameof(ProcedureModule));
            }

            return results;
        }

        /// <summary>
        /// 异步初始化核心模块。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public async UniTask InitializeCoreAsync(CancellationToken cancellationToken = default)
        {
            Diagnostics = await Game.InitializeModuleAsync(static () => new DiagnosticsModule(), cancellationToken);
            Data = await Game.InitializeModuleAsync(static () => new DataModule(), cancellationToken);
            Platform = await Game.InitializeModuleAsync(static () => new PlatformModule(), cancellationToken);
        }

        /// <summary>
        /// 异步初始化资源模块。
        /// </summary>
        /// <param name="settings">资源设置。</param>
        /// <param name="prepareResourcesOnStartup">是否在启动时准备资源。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public async UniTask InitializeResourceAsync(ResourceSettings settings, bool prepareResourcesOnStartup, CancellationToken cancellationToken = default)
        {
            if (settings == null)
            {
                Resource = null;
                return;
            }

            Resource = Game.Resource;
            Resource.Initialize(settings);
            await Resource.InitializeAllPackagesAsync(cancellationToken);
            if (prepareResourcesOnStartup)
            {
                await Resource.UpdateService.PrepareAllPackagesAsync(cancellationToken);
            }
        }

        /// <summary>
        /// 异步初始化功能模块。
        /// </summary>
        /// <param name="overrideLanguage">覆盖语言。</param>
        /// <param name="initializeUI">是否初始化UI模块。</param>
        /// <param name="initializeSceneModule">是否初始化场景模块。</param>
        /// <param name="initializeProcedureModule">是否初始化流程模块。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public async UniTask InitializeFeaturesAsync(string overrideLanguage, bool initializeUI, bool initializeSceneModule, bool initializeProcedureModule, CancellationToken cancellationToken = default)
        {
            Localization = await Game.InitializeModuleAsync(static () => new LocalizationModule(), cancellationToken);
            if (!string.IsNullOrWhiteSpace(overrideLanguage))
            {
                Localization.SetLanguage(overrideLanguage);
            }

            Audio = await Game.InitializeModuleAsync(static () => new AudioModule(), cancellationToken);
            Input = await Game.InitializeModuleAsync(static () => new InputModule(), cancellationToken);

            if (initializeUI)
            {
                UI = await Game.InitializeModuleAsync(static () => new UIModule(), cancellationToken);
            }

            if (initializeSceneModule)
            {
                Scene = await Game.InitializeModuleAsync(static () => new SceneModule(), cancellationToken);
            }

            if (initializeProcedureModule)
            {
                Procedure = await Game.InitializeModuleAsync(static () => new ProcedureModule(), cancellationToken);
            }
        }

        /// <summary>
        /// 要求模块已就绪，否则抛出异常。
        /// </summary>
        /// <typeparam name="TModule">模块类型。</typeparam>
        /// <param name="module">模块实例。</param>
        /// <param name="moduleName">模块名称。</param>
        /// <returns>模块实例。</returns>
        /// <exception cref="InvalidOperationException">当模块不可用时抛出。</exception>
        public TModule RequireReady<TModule>(TModule module, string moduleName = null)
            where TModule : class, IGameFrameworkModule
        {
            if (module == null)
            {
                throw new InvalidOperationException($"Module '{moduleName ?? typeof(TModule).Name}' is not available in the current composition.");
            }

            Game.EnsureModuleReady<TModule>();
            return module;
        }
    }
}
