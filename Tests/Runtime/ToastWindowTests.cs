using System;
using System.Collections;
using System.Reflection;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GameDeveloperKit.Tests
{
    public sealed class ToastWindowTests
    {
        [UnityTest]
        public IEnumerator AddToast_WhenCalledRepeatedly_CreatesAndStacksIndependentEntries()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var fixture = await CreateFixtureAsync();
                try
                {
                    fixture.Window.AddToast("first", 5f);
                    fixture.Window.AddToast("second", 5f);
                    await UniTask.Delay(TimeSpan.FromMilliseconds(350d), DelayType.UnscaledDeltaTime);

                    var first = fixture.Root.transform.Find("ToastEntry_1") as RectTransform;
                    var second = fixture.Root.transform.Find("ToastEntry_2") as RectTransform;
                    Assert.IsFalse(fixture.Template.gameObject.activeSelf);
                    Assert.AreEqual(2, fixture.Window.ActiveToastCount);
                    Assert.IsNotNull(first);
                    Assert.IsNotNull(second);
                    Assert.AreEqual("first", first.GetComponentInChildren<TMP_Text>(true).text);
                    Assert.AreEqual("second", second.GetComponentInChildren<TMP_Text>(true).text);
                    Assert.Greater(first.anchoredPosition.y, second.anchoredPosition.y);
                    Assert.Greater(second.anchoredPosition.y, 0f);
                    Assert.That(first.localScale.x, Is.EqualTo(1f).Within(0.05f));
                    Assert.That(second.localScale.x, Is.EqualTo(1f).Within(0.05f));
                    Assert.IsFalse(first.GetComponent<CanvasGroup>().blocksRaycasts);
                    Assert.IsFalse(second.GetComponent<CanvasGroup>().blocksRaycasts);
                }
                finally
                {
                    fixture.Dispose();
                }
            });
        }

        [UnityTest]
        public IEnumerator AddToast_WhenDurationEnds_MovesUpFadesAndRemovesEntry()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var fixture = await CreateFixtureAsync();
                try
                {
                    fixture.Window.AddToast("short", 0f);
                    await UniTask.Delay(TimeSpan.FromMilliseconds(300d), DelayType.UnscaledDeltaTime);

                    var entry = fixture.Root.transform.Find("ToastEntry_1") as RectTransform;
                    Assert.IsNotNull(entry);
                    Assert.Greater(entry.anchoredPosition.y, 80f);
                    Assert.Less(entry.GetComponent<CanvasGroup>().alpha, 1f);

                    await UniTask.Delay(TimeSpan.FromMilliseconds(250d), DelayType.UnscaledDeltaTime);
                    Assert.AreEqual(0, fixture.Window.ActiveToastCount);
                    Assert.IsNull(fixture.Root.transform.Find("ToastEntry_1"));
                }
                finally
                {
                    fixture.Dispose();
                }
            });
        }

        [UnityTest]
        public IEnumerator Release_WhenEntriesAreAnimating_ClearsEveryEntry()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var fixture = await CreateFixtureAsync();
                fixture.Window.AddToast("first", 5f);
                fixture.Window.AddToast("second", 5f);

                fixture.Window.Release();
                await UniTask.Yield();

                Assert.AreEqual(0, fixture.Window.ActiveToastCount);
                Assert.IsNull(fixture.Root.transform.Find("ToastEntry_1"));
                Assert.IsNull(fixture.Root.transform.Find("ToastEntry_2"));
                fixture.Dispose(false);
            });
        }

        private static async UniTask<ToastFixture> CreateFixtureAsync()
        {
            var root = new GameObject(
                "ToastTestRoot",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UIDocument));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(1920f, 1080f);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var templateObject = new GameObject("b_temp", typeof(RectTransform));
            var template = templateObject.GetComponent<RectTransform>();
            template.SetParent(rootRect, false);
            template.sizeDelta = new Vector2(300f, 100f);

            var backgroundObject = new GameObject("b_background", typeof(RectTransform), typeof(Image));
            var background = backgroundObject.GetComponent<RectTransform>();
            background.SetParent(template, false);
            background.sizeDelta = new Vector2(300f, 100f);

            var contentObject = new GameObject(
                "b_content",
                typeof(RectTransform),
                typeof(TextMeshProUGUI),
                typeof(ContentSizeFitter));
            var content = contentObject.GetComponent<RectTransform>();
            content.SetParent(template, false);
            content.sizeDelta = new Vector2(236f, 50f);
            var fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var document = root.GetComponent<UIDocument>();
            SetPrivateField(
                document,
                "mappings",
                new[]
                {
                    new UIBindMapping
                    {
                        Name = "b_temp",
                        Target = templateObject,
                        Components = new Component[] { template }
                    }
                });

            var window = new ToastWindow();
            window.Initialize(document, root, UILayer.Message);
            await window.OnAwakeAsync();
            return new ToastFixture(root, template, window);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(target, value);
        }

        private sealed class ToastFixture
        {
            public ToastFixture(GameObject root, RectTransform template, ToastWindow window)
            {
                Root = root;
                Template = template;
                Window = window;
            }

            public GameObject Root { get; }
            public RectTransform Template { get; }
            public ToastWindow Window { get; }

            public void Dispose(bool releaseWindow = true)
            {
                if (releaseWindow && Window.GameObject != null)
                {
                    Window.Release();
                }

                if (Root != null)
                {
                    UnityEngine.Object.DestroyImmediate(Root);
                }
            }
        }
    }
}
