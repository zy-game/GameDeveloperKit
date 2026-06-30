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
                try
                {
                    await App.Unregister<UIModule>();
                }
                catch (GameException)
                {
                }

                var root = GameObject.Find("GameDeveloperKit.UIRoot");
                if (root != null)
                {
                    UnityEngine.Object.Destroy(root);
                    await UniTask.Yield();
                }
            });
        }

        [UnityTest]
        public IEnumerator Register_WhenUIModuleIsRegistered_ReturnsUI()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Register<UIModule>();

                Assert.IsNotNull(App.UI);
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

        [Test]
        public void OpenAsync_WhenWindowHasNoOption_Throws()
        {
            var module = new UIModule();

            Assert.Throws<GameException>(() => module.OpenAsync<WindowWithoutOption>().GetAwaiter().GetResult());
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
    }
}
