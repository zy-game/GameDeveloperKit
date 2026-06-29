using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Debugger;
using GameDeveloperKit.Resource;
using GameDeveloperKit.Timer;
using GameDeveloperKit.UI.Internal;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.UI
{
    [ModuleDependency(typeof(ResourceModule))]
    [ModuleDependency(typeof(TimerModule))]
    public sealed partial class UIModule : GameModuleBase
    {
        internal const string RootName = "GameDeveloperKit.UIRoot";
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private static readonly UILayer[] LayerOrder =
        {
            UILayer.Background,
            UILayer.Main,
            UILayer.Window,
            UILayer.Loading,
            UILayer.Message,
            UILayer.StoryPlayback,
        };
        private readonly Dictionary<UILayer, RectTransform> m_Layers = new Dictionary<UILayer, RectTransform>();
        private readonly Dictionary<Type, UIWindowRecord> m_Records = new Dictionary<Type, UIWindowRecord>();
        private readonly Dictionary<Type, UniTaskCompletionSource<UIWindow>> m_PendingOpens = new Dictionary<Type, UniTaskCompletionSource<UIWindow>>();
        private readonly Dictionary<UILayer, UIWindowStack> m_LayerStacks = new Dictionary<UILayer, UIWindowStack>();
        private readonly List<UIWindowRecord> m_BackStack = new List<UIWindowRecord>();
        private readonly UISafeAreaDriver m_SafeAreaDriver = new UISafeAreaDriver();
        private GameObject m_Root;
        private Canvas m_Canvas;
        private CanvasScaler m_CanvasScaler;
        private RectTransform m_SafeAreaRoot;
        private UpdateTimerHandle m_SafeAreaUpdateHandle;

        /// <summary>
        /// 启动 member。
        /// </summary>
        public override void Startup()
        {
            if (m_Root != null)
            {
                return;
            }

            m_Root = new GameObject(RootName, typeof(RectTransform));
            Object.DontDestroyOnLoad(m_Root);

            m_Canvas = m_Root.AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            m_CanvasScaler = m_Root.AddComponent<CanvasScaler>();
            m_CanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            m_CanvasScaler.referenceResolution = ReferenceResolution;
            m_CanvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            m_CanvasScaler.matchWidthOrHeight = 0.5f;

            m_Root.AddComponent<GraphicRaycaster>();
            m_SafeAreaRoot = CreateStretchRect("SafeArea", m_Root.transform);
            m_SafeAreaDriver.Initialize(m_SafeAreaRoot, m_Canvas, m_CanvasScaler);
            CreateLayers();
            RegisterSafeAreaUpdate();
        }

        /// <summary>
        /// 关闭 member。
        /// </summary>
        public override void Shutdown()
        {
            foreach (var pending in m_PendingOpens.Values)
            {
                pending.TrySetException(new GameException("UIModule is shutting down."));
            }

            m_PendingOpens.Clear();

            for (var i = LayerOrder.Length - 1; i >= 0; i--)
            {
                var layer = LayerOrder[i];
                var records = new List<UIWindowRecord>();
                var allRecords = new List<UIWindowRecord>(m_Records.Values);
                foreach (var record in allRecords)
                {
                    if (record.Layer == layer)
                    {
                        records.Add(record);
                    }
                }

                foreach (var record in records)
                {
                    CloseRecordImmediate(record);
                }
            }

            m_SafeAreaDriver.Clear();
            m_Records.Clear();
            m_BackStack.Clear();
            foreach (var stack in m_LayerStacks.Values)
            {
                stack.Clear();
            }

            m_LayerStacks.Clear();
            m_Layers.Clear();
            m_Canvas = null;
            m_CanvasScaler = null;
            m_SafeAreaRoot = null;
            UnregisterSafeAreaUpdate();

            if (m_Root != null)
            {
                DestroyGameObject(m_Root);
                m_Root = null;
            }
        }

        /// <summary>
        /// 执行 Open Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public UniTask<T> OpenAsync<T>() where T : UIWindow
        {
            return OpenInternalAsync<T>();
        }

        /// <summary>
        /// 执行 Is Open。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public bool IsOpen<T>() where T : UIWindow
        {
            return m_Records.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 尝试获取 member。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public bool TryGet<T>(out T window) where T : UIWindow
        {
            if (m_Records.TryGetValue(typeof(T), out var record) && record.Window is T result)
            {
                window = result;
                return true;
            }

            window = null;
            return false;
        }

        /// <summary>
        /// 执行 Close。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public void Close<T>() where T : UIWindow
        {
            CloseAndReportAsync<T>().Forget();
        }

        /// <summary>
        /// 执行 Close Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public async UniTask CloseAsync<T>() where T : UIWindow
        {
            var type = typeof(T);
            if (m_Records.TryGetValue(type, out var record))
            {
                await CloseRecordAsync(record);
                return;
            }

            if (m_PendingOpens.TryGetValue(type, out var pending))
            {
                await CloseAfterPendingAsync<T>(pending.Task);
            }
        }

        /// <summary>
        /// 执行 Back。
        /// </summary>
        public async UniTask Back()
        {
            if (m_BackStack.Count <= 1)
            {
                return;
            }

            var current = m_BackStack[m_BackStack.Count - 1];
            var previous = m_BackStack[m_BackStack.Count - 2];
            await CloseRecordAsync(current);
            if (previous != null && m_Records.ContainsKey(previous.WindowType))
            {
                m_LayerStacks[previous.Layer].Push(previous);
                previous.Window.OnEnable();
            }
        }

        /// <summary>
        /// 执行 Switch。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public async UniTask<T> Switch<T>() where T : UIWindow
        {
            var option = GetOption(typeof(T));
            var current = m_LayerStacks[UILayer.FromOrder(option.LayerOrder)].Top;
            if (current != null && current.WindowType != typeof(T))
            {
                await CloseRecordAsync(current);
            }

            return await OpenInternalAsync<T>();
        }

        /// <summary>
        /// 执行 Open Internal Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        private async UniTask<T> OpenInternalAsync<T>() where T : UIWindow
        {
            EnsureStarted();
            RefreshSafeArea();

            var type = typeof(T);
            if (m_Records.TryGetValue(type, out var record))
            {
                DisableTopBeforePush(record);
                m_LayerStacks[record.Layer].Push(record);
                PushBackStack(record);
                await record.Window.OnOpenAsync();
                record.Window.OnEnable();
                return (T)record.Window;
            }

            if (m_PendingOpens.TryGetValue(type, out var pending))
            {
                return (T)await pending.Task;
            }

            var completionSource = new UniTaskCompletionSource<UIWindow>();
            m_PendingOpens.Add(type, completionSource);
            try
            {
                var window = await OpenNewAsync<T>();
                completionSource.TrySetResult(window);
                return window;
            }
            catch (Exception exception)
            {
                completionSource.TrySetException(exception);
                throw;
            }
            finally
            {
                m_PendingOpens.Remove(type);
            }
        }

        /// <summary>
        /// 执行 Open New Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        private async UniTask<T> OpenNewAsync<T>() where T : UIWindow
        {
            var type = typeof(T);
            var option = GetOption(type);
            var handle = await LoadPrefabAsync(option.Path);
            var prefab = handle.GetAsset<GameObject>();
            if (prefab == null)
            {
                await UnloadAssetAsync(handle);
                throw new GameException($"UI asset is not a GameObject prefab: {option.Path}");
            }

            GameObject instance = null;
            UIDocument document = null;
            try
            {
                instance = Object.Instantiate(prefab, m_Layers[UILayer.FromOrder(option.LayerOrder)], false);
                document = instance.GetComponentInChildren<UIDocument>(true);
                if (document == null)
                {
                    throw new GameException($"UI prefab '{option.Path}' is missing GameDeveloperKit.UI.UIDocument.");
                }

                var window = Activator.CreateInstance<T>();
                window.Initialize(document, instance, UILayer.FromOrder(option.LayerOrder));

                var record = new UIWindowRecord
                {
                    WindowType = type,
                    Option = option,
                    Window = window,
                    Document = document,
                    Instance = instance,
                    AssetHandle = handle,
                    Layer = UILayer.FromOrder(option.LayerOrder),
                    Status = UIWindowStatus.Loading,
                };

                RegisterDocument(document);
                await window.OnAwakeAsync();
                DisableTopBeforePush(record);
                await window.OnOpenAsync();
                window.OnEnable();

                record.Status = UIWindowStatus.Opened;
                m_Records.Add(type, record);
                m_LayerStacks[record.Layer].Push(record);
                PushBackStack(record);
                return window;
            }
            catch
            {
                UnregisterDocument(document);
                DestroyGameObject(instance);
                await UnloadAssetAsync(handle);
                throw;
            }
        }

        /// <summary>
        /// 获取 Option。
        /// </summary>
        private static UIOption GetOption(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (type.IsAbstract)
            {
                throw new ArgumentException("UI window type cannot be abstract.", nameof(type));
            }

            var option = (UIOption)Attribute.GetCustomAttribute(type, typeof(UIOption), false);
            if (option == null)
            {
                throw new GameException($"UI window '{type.Name}' is missing UIOption.");
            }

            if (string.IsNullOrWhiteSpace(option.Path))
            {
                throw new ArgumentException("UIOption path cannot be empty.", nameof(type));
            }

            if (IsValidLayer(UILayer.FromOrder(option.LayerOrder)) is false)
            {
                throw new ArgumentException("UIOption layer must be a single valid UILayer.", nameof(type));
            }

            return option;
        }

        /// <summary>
        /// 加载 Prefab Async。
        /// </summary>
        private async UniTask<AssetHandle> LoadPrefabAsync(string path)
        {
            AssetHandle handle;
            try
            {
                handle = await App.Resource.LoadAssetAsync(path);
            }
            catch (Exception exception)
            {
                throw new GameException($"Failed to load UI prefab: {path}", exception);
            }

            if (handle == null || handle.Status is not ResourceStatus.Succeeded)
            {
                throw new GameException($"Failed to load UI prefab: {path}", handle?.Error);
            }

            return handle;
        }

        /// <summary>
        /// 确保 Started。
        /// </summary>
        private void EnsureStarted()
        {
            if (m_Root == null)
            {
                throw new GameException("UIModule is not started.");
            }
        }

        /// <summary>
        /// 卸载 Asset Async。
        /// </summary>
        private static async UniTask UnloadAssetAsync(AssetHandle handle)
        {
            if (handle == null || handle.Info == null)
            {
                return;
            }

            await App.Resource.UnloadAsset(handle);
        }

        /// <summary>
        /// 执行 Close Record Async。
        /// </summary>
        private async UniTask CloseRecordAsync(UIWindowRecord record)
        {
            if (record == null || record.Status == UIWindowStatus.Closing)
            {
                return;
            }

            record.Status = UIWindowStatus.Closing;
            m_Records.Remove(record.WindowType);
            if (m_LayerStacks.TryGetValue(record.Layer, out var stack))
            {
                stack.Remove(record);
            }

            m_BackStack.Remove(record);
            UnregisterDocument(record.Document);

            record.Window?.OnDisable();
            record.Window?.Release();
            DestroyGameObject(record.Instance);
            await UnloadAssetAsync(record.AssetHandle);

            record.Window = null;
            record.Document = null;
            record.Instance = null;
            record.AssetHandle = null;
        }

        /// <summary>
        /// 同步关闭窗口记录。
        /// </summary>
        private void CloseRecordImmediate(UIWindowRecord record)
        {
            if (record == null || record.Status == UIWindowStatus.Closing)
            {
                return;
            }

            record.Status = UIWindowStatus.Closing;
            m_Records.Remove(record.WindowType);
            if (m_LayerStacks.TryGetValue(record.Layer, out var stack))
            {
                stack.Remove(record);
            }

            m_BackStack.Remove(record);
            UnregisterDocument(record.Document);

            record.Window?.OnDisable();
            record.Window?.Release();
            DestroyGameObject(record.Instance);
            record.AssetHandle?.Release();

            record.Window = null;
            record.Document = null;
            record.Instance = null;
            record.AssetHandle = null;
        }

        /// <summary>
        /// 执行 Close After Pending Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        private async UniTask CloseAfterPendingAsync<T>(UniTask<UIWindow> pending) where T : UIWindow
        {
            try
            {
                await pending;
            }
            catch
            {
                return;
            }

            if (m_Records.TryGetValue(typeof(T), out var record))
            {
                await CloseRecordAsync(record);
            }
        }

        /// <summary>
        /// 执行 Close 并上报后台关闭异常。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        private async UniTaskVoid CloseAndReportAsync<T>() where T : UIWindow
        {
            try
            {
                await CloseAsync<T>();
            }
            catch (Exception exception)
            {
                ReportCloseException(typeof(T), exception);
            }
        }

        /// <summary>
        /// 上报后台关闭异常。
        /// </summary>
        /// <param name="windowType">window Type 参数。</param>
        private static void ReportCloseException(Type windowType, Exception exception)
        {
            if (App.TryGetRegistered<DebugModule>(out var debug))
            {
                debug.Error(exception, $"Failed to close UI window '{windowType.Name}'.", nameof(UIModule));
                return;
            }

            UnityEngine.Debug.LogException(exception);
        }

        /// <summary>
        /// 销毁 Game Object。
        /// </summary>
        /// <param name="gameObject">game Object 参数。</param>
        private static void DestroyGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(gameObject);
            }
            else
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
