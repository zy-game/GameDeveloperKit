using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;
using GameDeveloperKit.UI.Internal;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// 定义 UI Module 类型。
    /// </summary>
    public sealed class UIModule : GameModuleBase
    {
        /// <summary>
        /// 定义 Root Name 常量。
        /// </summary>
        internal const string RootName = "GameDeveloperKit.UIRoot";

        /// <summary>
        /// 存储 Reference Resolution。
        /// </summary>
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private static readonly UILayer[] LayerOrder =
        {
            UILayer.Background,
            UILayer.Main,
            UILayer.Window,
            UILayer.Loading,
            UILayer.Message,
        };

        /// <summary>
        /// 存储 Layers。
        /// </summary>
        private readonly Dictionary<UILayer, RectTransform> m_Layers = new Dictionary<UILayer, RectTransform>();
        /// <summary>
        /// 存储 Records。
        /// </summary>
        private readonly Dictionary<Type, UIWindowRecord> m_Records = new Dictionary<Type, UIWindowRecord>();
        /// <summary>
        /// 存储 Pending Opens。
        /// </summary>
        private readonly Dictionary<Type, UniTaskCompletionSource<UIWindow>> m_PendingOpens = new Dictionary<Type, UniTaskCompletionSource<UIWindow>>();
        /// <summary>
        /// 存储 Layer Stacks。
        /// </summary>
        private readonly Dictionary<UILayer, UIWindowStack> m_LayerStacks = new Dictionary<UILayer, UIWindowStack>();
        /// <summary>
        /// 存储 Back Stack。
        /// </summary>
        private readonly List<UIWindowRecord> m_BackStack = new List<UIWindowRecord>();
        /// <summary>
        /// 存储 Safe Area Driver。
        /// </summary>
        private readonly UISafeAreaDriver m_SafeAreaDriver = new UISafeAreaDriver();

        /// <summary>
        /// 存储 Root。
        /// </summary>
        private GameObject m_Root;
        /// <summary>
        /// 存储 Canvas。
        /// </summary>
        private Canvas m_Canvas;
        /// <summary>
        /// 存储 Canvas Scaler。
        /// </summary>
        private CanvasScaler m_CanvasScaler;

        /// <summary>
        /// 启动 member。
        /// </summary>
        /// <returns>操作完成任务。</returns>
        public override UniTask Startup()
        {
            if (m_Root != null)
            {
                return UniTask.CompletedTask;
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
            CreateLayers();
            m_SafeAreaDriver.RefreshAll();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 关闭 member。
        /// </summary>
        /// <returns>操作完成任务。</returns>
        public override async UniTask Shutdown()
        {
            var pendingTasks = new List<UniTask<UIWindow>>();
            foreach (var pending in m_PendingOpens.Values)
            {
                pendingTasks.Add(pending.Task);
            }

            m_PendingOpens.Clear();
            foreach (var pendingTask in pendingTasks)
            {
                try
                {
                    await pendingTask;
                }
                catch
                {
                }
            }

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
                    await CloseRecordAsync(record);
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
        /// <returns>操作完成任务。</returns>
        public UniTask<T> OpenAsync<T>() where T : UIWindow
        {
            return OpenInternalAsync<T>();
        }

        /// <summary>
        /// 执行 Is Open。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        /// <returns>条件满足时返回 true。</returns>
        public bool IsOpen<T>() where T : UIWindow
        {
            return m_Records.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 尝试获取 member。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        /// <param name="window">window 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
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
        /// 刷新 Safe Area。
        /// </summary>
        public void RefreshSafeArea()
        {
            m_SafeAreaDriver.RefreshIfChanged();
        }

        /// <summary>
        /// 注册 Document。
        /// </summary>
        /// <param name="document">document 参数。</param>
        internal void RegisterDocument(UIDocument document)
        {
            m_SafeAreaDriver.Add(document);
        }

        /// <summary>
        /// 注销 Document。
        /// </summary>
        /// <param name="document">document 参数。</param>
        internal void UnregisterDocument(UIDocument document)
        {
            m_SafeAreaDriver.Remove(document);
        }

        /// <summary>
        /// 执行 Close。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public void Close<T>() where T : UIWindow
        {
            var type = typeof(T);
            if (m_Records.TryGetValue(type, out var record))
            {
                CloseRecordAsync(record).Forget();
                return;
            }

            if (m_PendingOpens.TryGetValue(type, out var pending))
            {
                CloseAfterPendingAsync<T>(pending.Task).Forget();
            }
        }

        /// <summary>
        /// 执行 Back。
        /// </summary>
        /// <returns>操作完成任务。</returns>
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
        /// <returns>操作完成任务。</returns>
        public async UniTask<T> Switch<T>() where T : UIWindow
        {
            var option = GetOption(typeof(T));
            var current = m_LayerStacks[option.Layer].Top;
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
        /// <returns>操作完成任务。</returns>
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
        /// <returns>操作完成任务。</returns>
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
                instance = Object.Instantiate(prefab, m_Layers[option.Layer], false);
                document = instance.GetComponentInChildren<UIDocument>(true);
                if (document == null)
                {
                    throw new GameException($"UI prefab '{option.Path}' is missing GameDeveloperKit.UI.UIDocument.");
                }

                UISafeAreaDriver.Apply(document);

                var window = Activator.CreateInstance<T>();
                window.Initialize(document, instance, option.Layer);

                var record = new UIWindowRecord
                {
                    WindowType = type,
                    Option = option,
                    Window = window,
                    Document = document,
                    Instance = instance,
                    AssetHandle = handle,
                    Layer = option.Layer,
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
        /// <param name="type">type 参数。</param>
        /// <returns>执行结果。</returns>
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

            if (IsValidLayer(option.Layer) is false)
            {
                throw new ArgumentException("UIOption layer must be a single valid UILayer.", nameof(type));
            }

            return option;
        }

        /// <summary>
        /// 加载 Prefab Async。
        /// </summary>
        /// <param name="path">path 参数。</param>
        /// <returns>操作完成任务。</returns>
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
        /// 执行 Push Back Stack。
        /// </summary>
        /// <param name="record">record 参数。</param>
        private void PushBackStack(UIWindowRecord record)
        {
            if (record == null || IsNavigable(record.Layer) is false)
            {
                return;
            }

            m_BackStack.Remove(record);
            m_BackStack.Add(record);
        }

        /// <summary>
        /// 执行 Disable Top Before Push。
        /// </summary>
        /// <param name="record">record 参数。</param>
        private void DisableTopBeforePush(UIWindowRecord record)
        {
            if (record == null || IsNavigable(record.Layer) is false)
            {
                return;
            }

            var top = m_LayerStacks[record.Layer].Top;
            if (top == null || top == record)
            {
                return;
            }

            top.Window?.OnDisable();
        }

        /// <summary>
        /// 执行 Is Navigable。
        /// </summary>
        /// <param name="layer">layer 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        private static bool IsNavigable(UILayer layer)
        {
            return layer is UILayer.Main or UILayer.Window;
        }

        /// <summary>
        /// 执行 Is Valid Layer。
        /// </summary>
        /// <param name="layer">layer 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        private static bool IsValidLayer(UILayer layer)
        {
            return layer is UILayer.Background or UILayer.Main or UILayer.Window or UILayer.Loading or UILayer.Message;
        }

        /// <summary>
        /// 卸载 Asset Async。
        /// </summary>
        /// <param name="handle">handle 参数。</param>
        /// <returns>操作完成任务。</returns>
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
        /// <param name="record">record 参数。</param>
        /// <returns>操作完成任务。</returns>
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
        /// 执行 Close After Pending Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        /// <param name="pending">pending 参数。</param>
        /// <returns>操作完成任务。</returns>
        private async UniTaskVoid CloseAfterPendingAsync<T>(UniTask<UIWindow> pending) where T : UIWindow
        {
            try
            {
                await pending;
                Close<T>();
            }
            catch
            {
            }
        }

        /// <summary>
        /// 创建 Layers。
        /// </summary>
        private void CreateLayers()
        {
            m_Layers.Clear();
            m_LayerStacks.Clear();
            foreach (var layer in LayerOrder)
            {
                var layerTransform = CreateStretchRect(layer.ToString(), m_Root.transform);
                m_Layers.Add(layer, layerTransform);
                m_LayerStacks.Add(layer, new UIWindowStack());
            }
        }

        /// <summary>
        /// 创建 Stretch Rect。
        /// </summary>
        /// <param name="name">name 参数。</param>
        /// <param name="parent">parent 参数。</param>
        /// <returns>执行结果。</returns>
        private static RectTransform CreateStretchRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rectTransform = (RectTransform)gameObject.transform;
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            return rectTransform;
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