using System;
using System.Collections;
using System.Reflection;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GameDeveloperKit.Tests
{
    public sealed class UIModuleTests : RuntimeTestBase
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Shutdown();
                await StartupLoadingTestFixture.RestoreAsync();

                var root = GameObject.Find("GameDeveloperKit.UIRoot");
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    await UniTask.Yield();
                }

                var timer = GameObject.Find("Timer");
                if (timer != null)
                {
                    UnityEngine.Object.DestroyImmediate(timer);
                    await UniTask.Yield();
                }

                CachedLifecycleWindow.Reset();
                ShortTtlWindow.Reset();
                CacheDisabledWindow.Reset();
                ActiveShutdownWindow.Reset();
                ThrowOnCachedAwakeWindow.Reset();
            });
        }

        [UnityTest]
        public IEnumerator Register_WhenUIModuleIsRegistered_ReturnsUI()
        {
            return UniTask.ToCoroutine(() =>
            {
                App.Register<UIModule>();

                Assert.IsNotNull(App.UI);
                return UniTask.CompletedTask;
            });
        }

        [UnityTest]
        public IEnumerator Startup_CreatesCanvasRootAndLayers()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = new UIModule();

                module.Startup();

                var root = GameObject.Find("GameDeveloperKit.UIRoot");
                Assert.IsNotNull(root);
                Assert.IsNotNull(root.GetComponent<Canvas>());
                Assert.IsNotNull(root.GetComponent<CanvasScaler>());
                Assert.IsNotNull(root.GetComponent<GraphicRaycaster>());
                var safeArea = root.transform.Find("SafeArea");
                Assert.IsNotNull(safeArea);
                Assert.IsNotNull(safeArea.Find("Background"));
                Assert.IsNotNull(safeArea.Find("Main"));
                Assert.IsNotNull(safeArea.Find("Window"));
                Assert.IsNotNull(safeArea.Find("Loading"));
                var messageLayer = safeArea.Find("Message");
                var storyPlaybackLayer = safeArea.Find("StoryPlayback");
                Assert.IsNotNull(messageLayer);
                Assert.IsNotNull(storyPlaybackLayer);
                Assert.AreSame(storyPlaybackLayer, module.GetLayerRoot(UILayer.StoryPlayback));
                Assert.Greater(storyPlaybackLayer.GetSiblingIndex(), messageLayer.GetSiblingIndex());

                module.Shutdown();
                await UniTask.Yield();

                Assert.IsNull(GameObject.Find("GameDeveloperKit.UIRoot"));
            });
        }

        [UnityTest]
        public IEnumerator OpenAsync_WhenModuleIsNotStarted_Throws()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = new UIModule();

                var exception = await ThrowsAsync<GameException>(async () => { await module.OpenAsync<TestWindow>(); });
                StringAssert.Contains("not started", exception.Message);
            });
        }

        [UnityTest]
        public IEnumerator OpenAsync_WhenWindowHasNoOption_Throws()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = new UIModule();

                await ThrowsAsync<GameException>(async () => { await module.OpenAsync<WindowWithoutOption>(); });
            });
        }

        [UnityTest]
        public IEnumerator GetLayerRoot_WhenLayerIsCustom_CreatesRootInOrder()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = new UIModule();
                module.Startup();

                var custom = new UILayer(150, "Cutscene");
                var customRoot = module.GetLayerRoot(custom);
                var safeArea = customRoot.parent;

                Assert.AreEqual("Cutscene", customRoot.name);
                Assert.AreSame(customRoot, module.GetLayerRoot(UILayer.FromOrder(150)));
                Assert.Greater(customRoot.GetSiblingIndex(), safeArea.Find("Main").GetSiblingIndex());
                Assert.Less(customRoot.GetSiblingIndex(), safeArea.Find("Window").GetSiblingIndex());

                module.Shutdown();
                await UniTask.Yield();
            });
        }

        [Test]
        public void IsOpen_WhenWindowNeverOpened_ReturnsFalse()
        {
            var module = new UIModule();

            Assert.IsFalse(module.IsOpen<TestWindow>());
            Assert.IsFalse(module.TryGet<TestWindow>(out _));
        }

        [Test]
        public void ApplyWindowRootLayout_WhenRootHasInvalidRect_NormalizesToFullScreenStretch()
        {
            var instance = new GameObject("Window", typeof(RectTransform));
            var rectTransform = instance.GetComponent<RectTransform>();
            rectTransform.localScale = Vector3.zero;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.pivot = Vector2.zero;
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, 30f);
            rectTransform.localPosition = new Vector3(12f, 34f, 56f);

            try
            {
                var method = typeof(UIModule).GetMethod("ApplyWindowRootLayout", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.IsNotNull(method);
                method.Invoke(null, new object[] { instance });

                Assert.AreEqual(Vector2.zero, rectTransform.pivot);
                Assert.AreEqual(Vector2.zero, rectTransform.anchorMin);
                Assert.AreEqual(Vector2.one, rectTransform.anchorMax);
                Assert.AreEqual(Vector2.zero, rectTransform.offsetMin);
                Assert.AreEqual(Vector2.zero, rectTransform.offsetMax);
                Assert.AreEqual(Quaternion.identity, rectTransform.localRotation);
                Assert.AreEqual(Vector3.one, rectTransform.localScale);
                Assert.AreEqual(0f, rectTransform.localPosition.z);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void GetComponent_WhenComponentIsExplicitlyBound_ReturnsBoundComponent()
        {
            var root = new GameObject("UI");
            var target = new GameObject("b_Title");
            target.transform.SetParent(root.transform);
            var document = root.AddComponent<UIDocument>();
            var firstCollider = target.AddComponent<BoxCollider>();
            var selectedCollider = target.AddComponent<BoxCollider>();
            SetPrivateField(
                document,
                "mappings",
                new[]
                {
                    new UIBindMapping
                    {
                        Name = "b_Title",
                        Target = target,
                        Components = new Component[] { selectedCollider }
                    }
                });

            try
            {
                Assert.AreSame(selectedCollider, document.GetComponent<BoxCollider>("b_Title"));
                Assert.AreNotSame(firstCollider, document.GetComponent<BoxCollider>("b_Title"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator CloseAsync_WhenCacheEnabled_CallsCloseLifecycleAndKeepsInstanceInactive()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare();
                CachedLifecycleWindow.Reset();
                App.Register<UIModule>();

                var window = await App.UI.OpenAsync<CachedLifecycleWindow>();
                var instance = window.GameObject;

                await App.UI.CloseAsync<CachedLifecycleWindow>();

                Assert.IsFalse(App.UI.IsOpen<CachedLifecycleWindow>());
                Assert.IsFalse(App.UI.TryGet<CachedLifecycleWindow>(out _));
                Assert.AreEqual(1, CachedLifecycleWindow.DisableCount);
                Assert.AreEqual(1, CachedLifecycleWindow.ReleaseCount);
                Assert.IsNull(window.Document);
                Assert.IsNull(window.GameObject);
                Assert.IsTrue(instance != null);
                Assert.IsFalse(instance.activeSelf);
                Assert.AreEqual(1, App.Cache.Snapshot().EntryCount);
            });
        }

        [UnityTest]
        public IEnumerator OpenAsync_WhenCachedWithinTtl_ReusesInstanceAndReplaysOpenLifecycle()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare();
                CachedLifecycleWindow.Reset();
                App.Register<UIModule>();

                var firstWindow = await App.UI.OpenAsync<CachedLifecycleWindow>();
                var firstInstance = firstWindow.GameObject;
                await App.UI.CloseAsync<CachedLifecycleWindow>();

                var secondWindow = await App.UI.OpenAsync<CachedLifecycleWindow>();

                Assert.AreSame(firstWindow, secondWindow);
                Assert.AreSame(firstInstance, secondWindow.GameObject);
                Assert.IsTrue(firstInstance.activeSelf);
                Assert.IsTrue(App.UI.IsOpen<CachedLifecycleWindow>());
                Assert.AreEqual(2, CachedLifecycleWindow.AwakeCount);
                Assert.AreEqual(2, CachedLifecycleWindow.OpenCount);
                Assert.AreEqual(2, CachedLifecycleWindow.EnableCount);
                Assert.AreEqual(1, CachedLifecycleWindow.DisableCount);
                Assert.AreEqual(1, CachedLifecycleWindow.ReleaseCount);
            });
        }

        [UnityTest]
        public IEnumerator OpenAsync_WhenWindowLoaded_UsesLayerHierarchySorting()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare();
                App.Register<UIModule>();

                var window = await App.UI.OpenAsync<CachedLifecycleWindow>();

                var canvas = window.GameObject.GetComponent<Canvas>();
                Assert.IsNotNull(canvas);
                Assert.IsFalse(canvas.overrideSorting);
                Assert.AreEqual(0, canvas.sortingOrder);
                Assert.AreSame(App.UI.GetLayerRoot(UILayer.Window), window.GameObject.transform.parent);
            });
        }

        [UnityTest]
        public IEnumerator OpenAsync_WhenSameLayerWindowReopens_MovesLogicalTopToHierarchyTop()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare();
                App.Register<UIModule>();

                var first = await App.UI.OpenAsync<LayerStackWindowA>();
                var second = await App.UI.OpenAsync<LayerStackWindowB>();

                Assert.Greater(
                    second.GameObject.transform.GetSiblingIndex(),
                    first.GameObject.transform.GetSiblingIndex());

                await App.UI.OpenAsync<LayerStackWindowA>();

                Assert.Greater(
                    first.GameObject.transform.GetSiblingIndex(),
                    second.GameObject.transform.GetSiblingIndex());
            });
        }

        [UnityTest]
        public IEnumerator OpenAsync_WhenLayerIsCustom_UsesLazyLayerRoot()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare();
                App.Register<UIModule>();

                var window = await App.UI.OpenAsync<CustomLayerWindow>();
                var layerRoot = App.UI.GetLayerRoot(UILayer.FromOrder(250));

                Assert.AreSame(layerRoot, window.GameObject.transform.parent);
                Assert.IsTrue(App.UI.IsOpen<CustomLayerWindow>());

                await App.UI.CloseAsync<CustomLayerWindow>();
                Assert.IsFalse(App.UI.IsOpen<CustomLayerWindow>());
            });
        }

        [UnityTest]
        public IEnumerator TrimAsync_WhenCachedWindowTtlElapsed_DestroysInstanceWithoutRepeatingRelease()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare();
                ShortTtlWindow.Reset();
                App.Register<UIModule>();

                var firstWindow = await App.UI.OpenAsync<ShortTtlWindow>();
                var firstInstance = firstWindow.GameObject;
                await App.UI.CloseAsync<ShortTtlWindow>();

                App.Timer.Update(0.2f, 0.2f);
                await UniTask.Yield();
                Assert.AreEqual(0, await App.Cache.TrimAsync());

                Assert.IsTrue(firstInstance == null);
                Assert.AreEqual(1, ShortTtlWindow.ReleaseCount);

                var secondWindow = await App.UI.OpenAsync<ShortTtlWindow>();

                Assert.AreNotSame(firstInstance, secondWindow.GameObject);
                Assert.AreEqual(2, ShortTtlWindow.AwakeCount);
            });
        }

        [UnityTest]
        public IEnumerator CloseAsync_WhenCacheDisabled_DestroysInstanceImmediately()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare();
                CacheDisabledWindow.Reset();
                App.Register<UIModule>();

                var window = await App.UI.OpenAsync<CacheDisabledWindow>();
                var instance = window.GameObject;

                await App.UI.CloseAsync<CacheDisabledWindow>();
                await UniTask.Yield();

                Assert.IsFalse(App.UI.IsOpen<CacheDisabledWindow>());
                Assert.AreEqual(1, CacheDisabledWindow.DisableCount);
                Assert.AreEqual(1, CacheDisabledWindow.ReleaseCount);
                Assert.IsTrue(instance == null);
                Assert.AreEqual(0, App.Cache.Snapshot().EntryCount);
            });
        }

        [UnityTest]
        public IEnumerator CloseAsync_WhenCalledTwice_DoesNotCreateDuplicateCacheEntry()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare();
                CachedLifecycleWindow.Reset();
                App.Register<UIModule>();

                await App.UI.OpenAsync<CachedLifecycleWindow>();

                await App.UI.CloseAsync<CachedLifecycleWindow>();
                await App.UI.CloseAsync<CachedLifecycleWindow>();

                Assert.AreEqual(1, CachedLifecycleWindow.DisableCount);
                Assert.AreEqual(1, CachedLifecycleWindow.ReleaseCount);
                Assert.AreEqual(1, App.Cache.Snapshot().EntryCount);
            });
        }

        [UnityTest]
        public IEnumerator CloseAsync_WhenWindowWasNeverOpened_DoesNotCreateCacheEntry()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare();
                App.Register<UIModule>();

                await App.UI.CloseAsync<CachedLifecycleWindow>();

                Assert.IsFalse(App.UI.IsOpen<CachedLifecycleWindow>());
                Assert.AreEqual(0, App.Cache.Snapshot().EntryCount);
            });
        }

        [UnityTest]
        public IEnumerator Shutdown_WhenActiveAndCachedWindowsExist_DestroysBoth()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare();
                CachedLifecycleWindow.Reset();
                ActiveShutdownWindow.Reset();
                App.Register<UIModule>();

                var cachedWindow = await App.UI.OpenAsync<CachedLifecycleWindow>();
                var cachedInstance = cachedWindow.GameObject;
                await App.UI.CloseAsync<CachedLifecycleWindow>();
                var activeWindow = await App.UI.OpenAsync<ActiveShutdownWindow>();
                var activeInstance = activeWindow.GameObject;

                await App.Shutdown();
                await UniTask.Yield();

                Assert.IsTrue(cachedInstance == null);
                Assert.IsTrue(activeInstance == null);
                Assert.IsNull(GameObject.Find("GameDeveloperKit.UIRoot"));
            });
        }

        [UnityTest]
        public IEnumerator OpenAsync_WhenCachedAwakeThrows_DestroysCachedRecordAndRethrows()
        {
            return UniTask.ToCoroutine(async () =>
            {
                StartupLoadingTestFixture.Prepare();
                ThrowOnCachedAwakeWindow.Reset();
                App.Register<UIModule>();

                var firstWindow = await App.UI.OpenAsync<ThrowOnCachedAwakeWindow>();
                var firstInstance = firstWindow.GameObject;
                await App.UI.CloseAsync<ThrowOnCachedAwakeWindow>();
                ThrowOnCachedAwakeWindow.ThrowOnNextAwake = true;

                var exception = await ThrowsAsync<InvalidOperationException>(async () =>
                {
                    await App.UI.OpenAsync<ThrowOnCachedAwakeWindow>();
                });
                await UniTask.Yield();

                StringAssert.Contains("cached awake failed", exception.Message);
                Assert.IsFalse(App.UI.IsOpen<ThrowOnCachedAwakeWindow>());
                Assert.IsFalse(App.UI.TryGet<ThrowOnCachedAwakeWindow>(out _));
                Assert.IsTrue(firstInstance == null);
            });
        }

        private static async UniTask<TException> ThrowsAsync<TException>(Func<UniTask> action)
            where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException exception)
            {
                return exception;
            }

            Assert.Fail($"Expected exception of type {typeof(TException).FullName}.");
            return null;
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(target, value);
        }

        [UIOption("tests/ui")]
        private sealed class TestWindow : UIWindow
        {
        }

        private sealed class WindowWithoutOption : UIWindow
        {
        }

        [UIOption("Resources/Loading", 200, CacheTimeToLive = 30f)]
        private sealed class CachedLifecycleWindow : CountingWindow
        {
            public static int AwakeCount { get; private set; }
            public static int OpenCount { get; private set; }
            public static int EnableCount { get; private set; }
            public static int DisableCount { get; private set; }
            public static int ReleaseCount { get; private set; }

            public static void Reset()
            {
                AwakeCount = 0;
                OpenCount = 0;
                EnableCount = 0;
                DisableCount = 0;
                ReleaseCount = 0;
            }

            protected override void RecordAwake()
            {
                AwakeCount++;
            }

            protected override void RecordOpen()
            {
                OpenCount++;
            }

            protected override void RecordEnable()
            {
                EnableCount++;
            }

            protected override void RecordDisable()
            {
                DisableCount++;
            }

            protected override void RecordRelease()
            {
                ReleaseCount++;
            }
        }

        [UIOption("Resources/Loading", 200, CacheTimeToLive = 0.1f)]
        private sealed class ShortTtlWindow : CountingWindow
        {
            public static int AwakeCount { get; private set; }
            public static int ReleaseCount { get; private set; }

            public static void Reset()
            {
                AwakeCount = 0;
                ReleaseCount = 0;
            }

            protected override void RecordAwake()
            {
                AwakeCount++;
            }

            protected override void RecordRelease()
            {
                ReleaseCount++;
            }
        }

        [UIOption("Resources/Loading", 200, CacheEnabled = false)]
        private sealed class CacheDisabledWindow : CountingWindow
        {
            public static int DisableCount { get; private set; }
            public static int ReleaseCount { get; private set; }

            public static void Reset()
            {
                DisableCount = 0;
                ReleaseCount = 0;
            }

            protected override void RecordDisable()
            {
                DisableCount++;
            }

            protected override void RecordRelease()
            {
                ReleaseCount++;
            }
        }

        [UIOption("Resources/Loading", 200)]
        private sealed class ActiveShutdownWindow : CountingWindow
        {
            public static int ReleaseCount { get; private set; }

            public static void Reset()
            {
                ReleaseCount = 0;
            }

            protected override void RecordRelease()
            {
                ReleaseCount++;
            }
        }

        [UIOption("Resources/Loading", 200, CacheEnabled = false)]
        private sealed class LayerStackWindowA : CountingWindow
        {
        }

        [UIOption("Resources/Loading", 200, CacheEnabled = false)]
        private sealed class LayerStackWindowB : CountingWindow
        {
        }

        [UIOption("Resources/Loading", 250, CacheEnabled = false)]
        private sealed class CustomLayerWindow : CountingWindow
        {
        }

        [UIOption("Resources/Loading", 200)]
        private sealed class ThrowOnCachedAwakeWindow : CountingWindow
        {
            public static bool ThrowOnNextAwake { get; set; }

            public static void Reset()
            {
                ThrowOnNextAwake = false;
            }

            protected override void RecordAwake()
            {
                if (ThrowOnNextAwake)
                {
                    ThrowOnNextAwake = false;
                    throw new InvalidOperationException("cached awake failed");
                }
            }
        }

        private abstract class CountingWindow : UIWindow
        {
            public override UniTask OnAwakeAsync()
            {
                Assert.IsNotNull(Document);
                Assert.IsNotNull(GameObject);
                RecordAwake();
                return UniTask.CompletedTask;
            }

            public override UniTask OnOpenAsync()
            {
                Assert.IsNotNull(Document);
                Assert.IsNotNull(GameObject);
                RecordOpen();
                return UniTask.CompletedTask;
            }

            public override void OnEnable()
            {
                Assert.IsNotNull(Document);
                Assert.IsNotNull(GameObject);
                RecordEnable();
            }

            public override void OnDisable()
            {
                RecordDisable();
            }

            public override void Release()
            {
                RecordRelease();
                base.Release();
            }

            protected virtual void RecordAwake()
            {
            }

            protected virtual void RecordOpen()
            {
            }

            protected virtual void RecordEnable()
            {
            }

            protected virtual void RecordDisable()
            {
            }

            protected virtual void RecordRelease()
            {
            }
        }
    }
}
