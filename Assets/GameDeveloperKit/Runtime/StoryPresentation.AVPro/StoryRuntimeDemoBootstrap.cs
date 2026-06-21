using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using GameDeveloperKit.Resource;
using RenderHeads.Media.AVProVideo;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// Runtime bootstrap for playing a compiled story program with AVPro presentation.
    /// </summary>
    public sealed class StoryRuntimeDemoBootstrap : MonoBehaviour
    {
        private const float DefaultFirstFrameTimeoutSeconds = 10f;

        [Header("剧情")]
        [SerializeField] private StoryProgramAsset m_StoryProgramAsset;
        [SerializeField] private string m_StoryProgramResourcePath = "NewStoryProgram";
        [SerializeField] private string m_ChapterId;

        [Header("播放器")]
        [SerializeField] private StoryPlayerView m_PlayerView;
        [SerializeField] private StoryPlayerView m_PlayerPrefab;
        [SerializeField] private Transform m_PlayerParent;
        [SerializeField] private bool m_DestroyInstantiatedPlayerOnLeave = true;

        [Header("启动")]
        [SerializeField] private bool m_StartOnAwake = true;
        [SerializeField] private bool m_InitializeResourceModule = true;
        [SerializeField] private bool m_InitializeAllResourcePackages = true;
        [SerializeField] private ResourceMode m_EditorPlayResourceMode = ResourceMode.EditorSimulator;
        [SerializeField] private float m_FirstFrameTimeoutSeconds = DefaultFirstFrameTimeoutSeconds;

        private StoryRuntimeLoadingView m_LoadingView;
        private StoryRuntimeChapterPreload m_ChapterPreload;
        private bool m_Started;

        private void Awake()
        {
            if (m_StartOnAwake)
            {
                StartStoryAsync().Forget();
            }
        }

        private void OnDestroy()
        {
            CloseLoading();
            ClearPreload();
        }

        /// <summary>
        /// Starts the configured runtime story playback.
        /// </summary>
        public void StartStory()
        {
            StartStoryAsync().Forget();
        }

        /// <summary>
        /// Starts the configured runtime story playback.
        /// </summary>
        /// <returns>Startup task.</returns>
        public async UniTask StartStoryAsync()
        {
            if (m_Started)
            {
                return;
            }

            m_Started = true;
            try
            {
                await App.Startup();
                await EnsureResourceInitializedAsync();

                await OpenLoadingAsync();
                SetLoading(0.05f, "正在加载剧情...");

                var storyAsset = ResolveStoryProgramAsset();
                if (storyAsset == null)
                {
                    throw new GameException(
                        $"Runtime story program asset not found. Assign StoryProgramAsset or export Resources/{m_StoryProgramResourcePath}.asset first.");
                }

                var program = storyAsset.ToProgram();
                var chapterId = string.IsNullOrWhiteSpace(m_ChapterId) ? program.EntryChapterId : m_ChapterId;

                SetLoading(0.2f, "正在预加载当前章节媒体...");
                m_ChapterPreload = new StoryRuntimeChapterPreload(program, chapterId);
                await m_ChapterPreload.PreloadStartupAsync(SetPreloadProgress);
                m_ChapterPreload.PreloadRemainingVideosAsync(SetPreloadProgress).Forget();

                SetLoading(0.85f, "正在启动剧情...");
                var playerView = ResolvePlayerView();
                var firstFrameTask = WaitFirstVideoFrameAsync(playerView, m_ChapterPreload.HasVideos);
                var request = StoryProcedureRequest.Direct(program, chapterId);
                request.PlayerView = playerView;
                request.DestroyInstantiatedPlayerOnLeave = m_DestroyInstantiatedPlayerOnLeave;

                await App.Procedure.ChangeAsync<StoryProcedure>(request);

                SetLoading(0.95f, m_ChapterPreload.HasVideos ? "等待第一个视频画面..." : "正在显示剧情...");
                await firstFrameTask;
                CloseLoading();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetLoading(1f, "剧情启动失败，请查看日志。");
            }
        }

        private StoryProgramAsset ResolveStoryProgramAsset()
        {
            if (m_StoryProgramAsset != null)
            {
                return m_StoryProgramAsset;
            }

            if (string.IsNullOrWhiteSpace(m_StoryProgramResourcePath))
            {
                return null;
            }

            return Resources.Load<StoryProgramAsset>(m_StoryProgramResourcePath);
        }

        private async UniTask EnsureResourceInitializedAsync()
        {
            if (m_InitializeResourceModule is false || App.Resource.IsInitialized)
            {
                return;
            }

            var settings = Resources.Load<ResourceSettings>("ResourceSettings");
            if (settings == null)
            {
                throw new GameException("ResourceSettings not found. Runtime story playback requires ResourceModule initialization.");
            }

            var runtimeSettings = Instantiate(settings);
            runtimeSettings.hideFlags = HideFlags.HideAndDontSave;
#if UNITY_EDITOR
            runtimeSettings.Mode = m_EditorPlayResourceMode;
#endif
            await App.Resource.InitializeAsync(new ResourceInitializeOptions
            {
                Settings = runtimeSettings
            });

            if (m_InitializeAllResourcePackages)
            {
                await InitializeAllResourcePackagesAsync();
            }
        }

        private static async UniTask InitializeAllResourcePackagesAsync()
        {
            var packages = App.Resource.Manifest?.Packages;
            if (packages == null)
            {
                return;
            }

            for (var i = 0; i < packages.Count; i++)
            {
                var packageName = packages[i]?.Name;
                if (string.IsNullOrWhiteSpace(packageName) ||
                    string.Equals(packageName, BuiltinMode.BUILTIN_PACKAGE_NAME, StringComparison.Ordinal))
                {
                    continue;
                }

                var operation = await App.Resource.InitializePackageAsync(packageName);
                if (operation == null || operation.Status != OperationStatus.Succeeded)
                {
                    throw new GameException($"Resource package initialize failed: {packageName}", operation?.Error);
                }
            }
        }

        private StoryPlayerView ResolvePlayerView()
        {
            if (IsSceneInstance(m_PlayerView))
            {
                return m_PlayerView;
            }

            var assignedPrefab = m_PlayerView;
            m_PlayerView = FindObjectOfType<StoryPlayerView>(true);
            if (IsSceneInstance(m_PlayerView))
            {
                return m_PlayerView;
            }

            var prefab = m_PlayerPrefab != null ? m_PlayerPrefab : assignedPrefab;
            if (prefab == null)
            {
                throw new GameException("StoryRuntimeDemoBootstrap requires a scene StoryPlayerView or PlayerPrefab.");
            }

            m_PlayerView = m_PlayerParent == null
                ? Instantiate(prefab)
                : Instantiate(prefab, m_PlayerParent);
            return m_PlayerView;
        }

        private static bool IsSceneInstance(Component component)
        {
            return component != null && component.gameObject.scene.IsValid();
        }

        private async UniTask WaitFirstVideoFrameAsync(StoryPlayerView playerView, bool hasVideos)
        {
            if (playerView == null)
            {
                return;
            }

            if (hasVideos is false)
            {
                await UniTask.NextFrame();
                return;
            }

            var completion = new UniTaskCompletionSource();
            void OnReady(StoryAvProVideoPlayback _)
            {
                completion.TrySetResult();
            }

            playerView.FirstVideoFrameReady += OnReady;
            try
            {
                var timeoutSeconds = m_FirstFrameTimeoutSeconds <= 0f
                    ? DefaultFirstFrameTimeoutSeconds
                    : m_FirstFrameTimeoutSeconds;
                var timeout = UniTask.Delay(TimeSpan.FromSeconds(timeoutSeconds), ignoreTimeScale: true);
                var index = await UniTask.WhenAny(completion.Task, timeout);
                if (index == 1)
                {
                    Debug.LogWarning($"Story first video frame was not ready after {timeoutSeconds:0.#} seconds. Loading will be closed.");
                }
            }
            finally
            {
                playerView.FirstVideoFrameReady -= OnReady;
            }
        }

        private async UniTask OpenLoadingAsync()
        {
            try
            {
                var window = await LoadingModule.OpenAsync();
                m_LoadingView = StoryRuntimeLoadingView.FromWindow(window);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"LoadingModule failed. Runtime fallback loading will be used. {exception.Message}");
                m_LoadingView = StoryRuntimeLoadingView.CreateFallback();
            }
        }

        private void SetPreloadProgress(float progress, string message)
        {
            SetLoading(Mathf.Lerp(0.2f, 0.85f, Mathf.Clamp01(progress)), message);
        }

        private void SetLoading(float progress, string message)
        {
            m_LoadingView?.Set(progress, message);
        }

        private void CloseLoading()
        {
            if (m_LoadingView == null)
            {
                return;
            }

            m_LoadingView.Close();
            m_LoadingView = null;
        }

        private void ClearPreload()
        {
            if (m_ChapterPreload == null)
            {
                return;
            }

            m_ChapterPreload.Dispose();
            m_ChapterPreload = null;
        }

        private sealed class StoryRuntimeChapterPreload : IDisposable
        {
            private readonly List<string> m_Videos = new List<string>();
            private readonly List<string> m_Audios = new List<string>();
            private readonly List<string> m_Images = new List<string>();
            private readonly List<StoryRuntimeAvProPreloadedVideo> m_PreloadedVideos = new List<StoryRuntimeAvProPreloadedVideo>();
            private readonly List<AssetHandle> m_PreloadedAssets = new List<AssetHandle>();

            private bool m_Disposed;

            public StoryRuntimeChapterPreload(StoryProgram program, string chapterId)
            {
                CollectMedia(program, chapterId);
            }

            public bool HasVideos => m_Videos.Count > 0;

            public async UniTask PreloadStartupAsync(Action<float, string> progress)
            {
                EnsureNotDisposed();
                var startupVideoCount = HasVideos ? 1 : 0;
                var total = startupVideoCount + m_Audios.Count + m_Images.Count;
                if (total == 0)
                {
                    progress?.Invoke(1f, "当前章节没有需要预加载的媒体。");
                    return;
                }

                var done = 0;
                for (var i = 0; i < m_Audios.Count; i++)
                {
                    progress?.Invoke((float)done / total, $"正在预加载音频 {i + 1}/{m_Audios.Count}...");
                    await PreloadAssetAsync(m_Audios[i]);
                    done++;
                }

                for (var i = 0; i < m_Images.Count; i++)
                {
                    progress?.Invoke((float)done / total, $"正在预加载图片 {i + 1}/{m_Images.Count}...");
                    await PreloadAssetAsync(m_Images[i]);
                    done++;
                }

                if (startupVideoCount > 0)
                {
                    progress?.Invoke((float)done / total, "正在预热第一个视频...");
                    var preloaded = new StoryRuntimeAvProPreloadedVideo(m_Videos[0]);
                    m_PreloadedVideos.Add(preloaded);
                    await preloaded.PreloadFirstFrameAsync();
                }

                progress?.Invoke(1f, HasVideos ? "第一个视频已可播放。" : "当前章节媒体预加载完成。");
            }

            public async UniTaskVoid PreloadRemainingVideosAsync(Action<float, string> progress)
            {
                if (m_Videos.Count <= 1)
                {
                    return;
                }

                for (var i = 1; i < m_Videos.Count; i++)
                {
                    if (m_Disposed)
                    {
                        return;
                    }

                    progress?.Invoke((float)i / m_Videos.Count, $"后台预热视频 {i + 1}/{m_Videos.Count}...");
                    var preloaded = new StoryRuntimeAvProPreloadedVideo(m_Videos[i]);
                    m_PreloadedVideos.Add(preloaded);
                    await preloaded.PreloadFirstFrameAsync();
                    await UniTask.Yield();
                }
            }

            public void Dispose()
            {
                if (m_Disposed)
                {
                    return;
                }

                m_Disposed = true;
                for (var i = 0; i < m_PreloadedVideos.Count; i++)
                {
                    m_PreloadedVideos[i]?.Dispose();
                }

                m_PreloadedVideos.Clear();

                for (var i = 0; i < m_PreloadedAssets.Count; i++)
                {
                    var handle = m_PreloadedAssets[i];
                    if (handle == null)
                    {
                        continue;
                    }

                    try
                    {
                        if (App.Resource.IsInitialized)
                        {
                            App.Resource.UnloadAsset(handle).Forget();
                        }
                        else
                        {
                            handle.Release();
                        }
                    }
                    catch
                    {
                        handle.Release();
                    }
                }

                m_PreloadedAssets.Clear();
            }

            private void CollectMedia(StoryProgram program, string chapterId)
            {
                if (program == null)
                {
                    return;
                }

                var chapter = FindChapter(program, chapterId);
                if (chapter == null)
                {
                    return;
                }

                for (var i = 0; i < chapter.Steps.Count; i++)
                {
                    var step = chapter.Steps[i];
                    var command = step?.Data?.Command;
                    if (command == null)
                    {
                        continue;
                    }

                    switch (command.Name)
                    {
                        case StoryMediaCommandNames.PlayVideo:
                            AddUnique(m_Videos, command.Arguments.GetString(StoryMediaCommandNames.ClipArgument));
                            break;
                        case StoryMediaCommandNames.PlayAudio:
                            AddUnique(m_Audios, command.Arguments.GetString(StoryMediaCommandNames.ClipArgument));
                            break;
                        case StoryMediaCommandNames.ShowImage:
                            AddUnique(m_Images, command.Arguments.GetString(StoryMediaCommandNames.ImageArgument));
                            break;
                    }
                }
            }

            private async UniTask PreloadAssetAsync(string location)
            {
                if (string.IsNullOrWhiteSpace(location))
                {
                    return;
                }

                try
                {
                    var handle = await App.Resource.LoadAssetAsync(location);
                    if (handle != null && handle.Status == ResourceStatus.Succeeded)
                    {
                        m_PreloadedAssets.Add(handle);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Story preload asset failed: {location}. {exception.Message}");
                }
            }

            private static StoryChapter FindChapter(StoryProgram program, string chapterId)
            {
                var targetChapterId = string.IsNullOrWhiteSpace(chapterId) ? program.EntryChapterId : chapterId;
                for (var i = 0; i < program.Chapters.Count; i++)
                {
                    var chapter = program.Chapters[i];
                    if (chapter != null && string.Equals(chapter.ChapterId, targetChapterId, StringComparison.Ordinal))
                    {
                        return chapter;
                    }
                }

                return null;
            }

            private static void AddUnique(List<string> list, string value)
            {
                if (string.IsNullOrWhiteSpace(value) || list.Contains(value))
                {
                    return;
                }

                list.Add(value);
            }

            private void EnsureNotDisposed()
            {
                if (m_Disposed)
                {
                    throw new ObjectDisposedException(nameof(StoryRuntimeChapterPreload));
                }
            }
        }

        private sealed class StoryRuntimeAvProPreloadedVideo : IDisposable
        {
            private const float TimeoutSeconds = 8f;

            private readonly string m_Path;
            private GameObject m_GameObject;
            private MediaPlayer m_Player;
            private UniTaskCompletionSource m_FirstFrameCompletion;
            private bool m_Disposed;

            public StoryRuntimeAvProPreloadedVideo(string path)
            {
                m_Path = path;
            }

            public async UniTask PreloadFirstFrameAsync()
            {
                var resolvedPath = ResolveMediaPath(m_Path);
                if (string.IsNullOrWhiteSpace(resolvedPath))
                {
                    Debug.LogWarning($"Story video preload path is invalid: {m_Path}");
                    return;
                }

                EnsurePlayer();
                m_FirstFrameCompletion = new UniTaskCompletionSource();
                var opened = m_Player.OpenMedia(MediaPathType.AbsolutePathOrURL, resolvedPath, true);
                if (opened is false)
                {
                    Debug.LogWarning($"AVPro preload cannot open video: {m_Path}");
                    return;
                }

                var timeout = UniTask.Delay(TimeSpan.FromSeconds(TimeoutSeconds), ignoreTimeScale: true);
                var result = await UniTask.WhenAny(m_FirstFrameCompletion.Task, timeout);
                if (result == 1)
                {
                    Debug.LogWarning($"AVPro preload first frame timeout: {m_Path}");
                }

                if (m_Player != null)
                {
                    m_Player.Pause();
                }
            }

            public void Dispose()
            {
                if (m_Disposed)
                {
                    return;
                }

                m_Disposed = true;
                if (m_Player != null)
                {
                    m_Player.Events.RemoveListener(OnMediaEvent);
                    m_Player.CloseMedia();
                }

                if (m_GameObject != null)
                {
                    Destroy(m_GameObject);
                }

                m_Player = null;
                m_GameObject = null;
                m_FirstFrameCompletion = null;
            }

            private void EnsurePlayer()
            {
                if (m_Player != null)
                {
                    return;
                }

                m_GameObject = new GameObject($"StoryVideoPreload_{Path.GetFileNameWithoutExtension(m_Path)}")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                DontDestroyOnLoad(m_GameObject);

                m_Player = m_GameObject.AddComponent<MediaPlayer>();
                m_Player.hideFlags = HideFlags.HideAndDontSave;
                m_Player.AutoOpen = false;
                m_Player.AutoStart = false;
                m_Player.Loop = false;
                m_Player.AudioMuted = true;
                m_Player.Events.AddListener(OnMediaEvent);
            }

            private void OnMediaEvent(MediaPlayer player, MediaPlayerEvent.EventType eventType, ErrorCode errorCode)
            {
                switch (eventType)
                {
                    case MediaPlayerEvent.EventType.FirstFrameReady:
                        m_FirstFrameCompletion?.TrySetResult();
                        break;
                    case MediaPlayerEvent.EventType.Error:
                        m_FirstFrameCompletion?.TrySetResult();
                        Debug.LogWarning($"AVPro preload video error: {m_Path} {errorCode}");
                        break;
                }
            }

            private static string ResolveMediaPath(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return null;
                }

                if (path.IndexOf("://", StringComparison.Ordinal) >= 0)
                {
                    return path;
                }

                var normalized = path.Replace('\\', '/');
                if (normalized.StartsWith("Assets/StreamingAssets/", StringComparison.OrdinalIgnoreCase))
                {
                    return Path.Combine(
                        Application.streamingAssetsPath,
                        normalized.Substring("Assets/StreamingAssets/".Length));
                }

                if (normalized.StartsWith("StreamingAssets/", StringComparison.OrdinalIgnoreCase))
                {
                    return Path.Combine(
                        Application.streamingAssetsPath,
                        normalized.Substring("StreamingAssets/".Length));
                }

                if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                    return string.IsNullOrWhiteSpace(projectRoot)
                        ? Path.GetFullPath(normalized)
                        : Path.GetFullPath(Path.Combine(projectRoot, normalized));
                }

                return Path.IsPathRooted(normalized) ? normalized : Path.GetFullPath(normalized);
            }
        }

        private sealed class StoryRuntimeLoadingView
        {
            private readonly LoadingWindow m_Window;
            private readonly GameObject m_FallbackRoot;
            private readonly Slider m_FallbackSlider;
            private readonly TMP_Text m_FallbackText;

            private StoryRuntimeLoadingView(LoadingWindow window)
            {
                m_Window = window;
            }

            private StoryRuntimeLoadingView(GameObject fallbackRoot, Slider fallbackSlider, TMP_Text fallbackText)
            {
                m_FallbackRoot = fallbackRoot;
                m_FallbackSlider = fallbackSlider;
                m_FallbackText = fallbackText;
            }

            public static StoryRuntimeLoadingView FromWindow(LoadingWindow window)
            {
                return new StoryRuntimeLoadingView(window);
            }

            public static StoryRuntimeLoadingView CreateFallback()
            {
                var root = new GameObject("StoryRuntimeLoading");
                DontDestroyOnLoad(root);

                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = short.MaxValue;
                root.AddComponent<GraphicRaycaster>();

                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                var background = CreateRect("Background", root.transform, Vector2.zero, Vector2.one);
                var image = background.gameObject.AddComponent<Image>();
                image.color = new Color(0.02f, 0.02f, 0.025f, 1f);

                var panel = CreateRect("Panel", background, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                panel.sizeDelta = new Vector2(720f, 160f);

                var text = CreateText("Info", panel, new Vector2(0.5f, 0.65f), new Vector2(680f, 48f), "正在准备剧情...");
                var sliderRoot = CreateRect("Progress", panel, new Vector2(0.5f, 0.28f), new Vector2(680f, 24f));
                var slider = sliderRoot.gameObject.AddComponent<Slider>();
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.interactable = false;

                var backgroundBar = sliderRoot.gameObject.AddComponent<Image>();
                backgroundBar.color = new Color(0.12f, 0.12f, 0.16f, 1f);

                var fillArea = CreateRect("Fill Area", sliderRoot, Vector2.zero, Vector2.one);
                fillArea.offsetMin = Vector2.zero;
                fillArea.offsetMax = Vector2.zero;
                var fill = CreateRect("Fill", fillArea, Vector2.zero, Vector2.one);
                var fillImage = fill.gameObject.AddComponent<Image>();
                fillImage.color = new Color(0.2f, 0.55f, 1f, 1f);
                slider.fillRect = fill;

                return new StoryRuntimeLoadingView(root, slider, text);
            }

            public void Set(float progress, string message)
            {
                if (m_Window?.Model != null)
                {
                    if (m_Window.Model.slider_slider != null)
                    {
                        m_Window.Model.slider_slider.value = Mathf.Clamp01(progress);
                    }

                    if (m_Window.Model.text_info != null)
                    {
                        m_Window.Model.text_info.text = message ?? string.Empty;
                    }
                }

                if (m_FallbackSlider != null)
                {
                    m_FallbackSlider.value = Mathf.Clamp01(progress);
                }

                if (m_FallbackText != null)
                {
                    m_FallbackText.text = message ?? string.Empty;
                }
            }

            public void Close()
            {
                if (m_Window != null)
                {
                    LoadingModule.Close();
                }

                if (m_FallbackRoot != null)
                {
                    Destroy(m_FallbackRoot);
                }
            }

            private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
            {
                var gameObject = new GameObject(name, typeof(RectTransform));
                var rect = (RectTransform)gameObject.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
                return rect;
            }

            private static TMP_Text CreateText(string name, Transform parent, Vector2 anchor, Vector2 size, string text)
            {
                var rect = CreateRect(name, parent, anchor, anchor);
                rect.sizeDelta = size;
                var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
                label.text = text;
                label.color = Color.white;
                label.fontSize = 28f;
                label.alignment = TextAlignmentOptions.Center;
                return label;
            }
        }
    }
}
