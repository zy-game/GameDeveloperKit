using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Procedure;
using GameDeveloperKit.Scripts.StoryTest;
using GameDeveloperKit.Story;
using GameDeveloperKit.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Playback;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryTestProcedureTests : RuntimeTestBase
    {
        private readonly List<GameObject> m_GameObjects = new List<GameObject>();
        private readonly List<UnityEngine.Object> m_Objects = new List<UnityEngine.Object>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                foreach (var gameObject in m_GameObjects)
                {
                    if (gameObject != null)
                    {
                        UnityEngine.Object.Destroy(gameObject);
                    }
                }

                m_GameObjects.Clear();
                await UniTask.Yield();

                try
                {
                    await App.Shutdown();
                }
                catch (GameException)
                {
                }

                await StartupLoadingTestFixture.RestoreAsync();

                foreach (var value in m_Objects)
                {
                    if (value != null)
                    {
                        UnityEngine.Object.DestroyImmediate(value);
                    }
                }

                m_Objects.Clear();
                RecordingProcedure.Reset();
            });
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_WhenAddedWithoutCanvas_CreatesRenderableCanvasRoot()
        {
            var gameObject = new GameObject("StoryTestPlayerView");
            m_GameObjects.Add(gameObject);
            gameObject.transform.localScale = Vector3.zero;

            gameObject.AddComponent<PlayerView>();
            yield return null;

            var canvas = gameObject.GetComponent<Canvas>();
            Assert.IsNotNull(canvas);
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.Greater(gameObject.transform.localScale.x, 0f);
            Assert.Greater(gameObject.transform.localScale.y, 0f);
            Assert.Greater(gameObject.transform.localScale.z, 0f);
            Assert.IsNotNull(gameObject.GetComponent("CanvasScaler"));
            Assert.IsNotNull(gameObject.GetComponent("GraphicRaycaster"));
        }

        [UnityTest]
        public IEnumerator StoryPlayerView_CreateDefault_UsesChineseTextFont()
        {
            var gameObject = new GameObject("StoryPlayerViewParent", typeof(RectTransform));
            m_GameObjects.Add(gameObject);

            var view = PlayerView.CreateDefault(gameObject.transform);
            m_GameObjects.Add(view.gameObject);
            yield return null;

            var font = Resources.Load<UnityEngine.Object>("SIMSUN SDF");
            Assert.IsNotNull(font);

            var textComponents = view.GetComponentsInChildren<Component>(true);
            var matched = 0;
            for (var i = 0; i < textComponents.Length; i++)
            {
                var component = textComponents[i];
                if (component == null ||
                    string.Equals(component.GetType().FullName, "TMPro.TextMeshProUGUI", StringComparison.Ordinal) is false)
                {
                    continue;
                }

                matched++;
                var fontProperty = component.GetType().GetProperty("font");
                Assert.IsNotNull(fontProperty);
                Assert.AreSame(font, fontProperty.GetValue(component));
            }

            Assert.Greater(matched, 0);
        }

        [UnityTest]
        public IEnumerator StartupAsync_WithStoryTestRequestAsset_EntersProcedureAndPlaysStory()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var playerView = CreatePlayerView();
                var programAsset = CreateObject<ProgramAsset>();
                programAsset.SetProgram(CreateLineProgram("story_test_asset"));
                var requestAsset = CreateObject<StoryTestRequestAsset>();
                SetField(requestAsset, "m_ProgramAsset", programAsset);
                SetField(requestAsset, "m_PlayerView", playerView);
                StartupLoadingTestFixture.Prepare();
                var startup = CreateStartup(requestAsset);

                await startup.StartupAsync();

                Assert.IsInstanceOf<StoryTestProcedure>(App.Procedure.Current);
                Assert.IsTrue(App.Story.HasProgram("story_test_asset"));
                AssertFrame(playerView.CurrentFrame, "chapter_01", "line");
                Assert.IsNull(playerView.LastError);
            });
        }

        [UnityTest]
        public IEnumerator ChangeAsync_WithStoryTestRequest_RegistersProgramAndPlaysChapter()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Initialize();
                var playerView = CreatePlayerView();
                var program = CreateTwoChapterProgram();
                var request = new StoryTestRequest(program, "chapter_02", playerView);

                await App.Procedure.ChangeAsync<StoryTestProcedure>(request);

                Assert.IsInstanceOf<StoryTestProcedure>(App.Procedure.Current);
                Assert.IsTrue(App.Story.HasProgram("story_test_program"));
                AssertFrame(playerView.CurrentFrame, "chapter_02", "line_02");
            });
        }

        [UnityTest]
        public IEnumerator ChangeAsync_WithRegisteredStoryId_PlaysRegisteredStory()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Initialize();
                var playerView = CreatePlayerView();
                App.Story.Register(CreateLineProgram("story_test_registered"));
                var request = new StoryTestRequest("story_test_registered", null, playerView);

                await App.Procedure.ChangeAsync<StoryTestProcedure>(request);

                AssertFrame(playerView.CurrentFrame, "chapter_01", "line");
                Assert.IsNull(playerView.LastError);
            });
        }

        [UnityTest]
        public IEnumerator ChangeAsync_WhenPlayerViewPrefabProvided_InstantiatesPlayerView()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Initialize();
                var playerViewPrefab = CreatePlayerViewPrefab();
                var request = new StoryTestRequest(
                    CreateLineProgram("story_test_player_prefab"),
                    null,
                    null,
                    playerViewPrefab);

                await App.Procedure.ChangeAsync<StoryTestProcedure>(request);

                var playerView = FindPlayerView();
                Assert.IsNotNull(playerView);
                Assert.AreNotSame(playerViewPrefab, playerView);
                Assert.AreSame(App.UI.GetLayerRoot(UILayer.StoryPlayback), playerView.transform.parent);
                AssertFrame(playerView.CurrentFrame, "chapter_01", "line");

                await App.Procedure.ChangeAsync<RecordingProcedure>();
                await UniTask.Yield();

                Assert.IsNull(FindPlayerView());
            });
        }

        [UnityTest]
        public IEnumerator ChangeAsync_WhenPlayerViewMissing_CreatesDefaultPlayerViewInStoryPlaybackLayer()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Initialize();
                var request = new StoryTestRequest(CreateLineProgram("story_test_default_player"));

                await App.Procedure.ChangeAsync<StoryTestProcedure>(request);

                var playerView = FindPlayerView();
                Assert.IsNotNull(playerView);
                Assert.AreSame(App.UI.GetLayerRoot(UILayer.StoryPlayback), playerView.transform.parent);
                Assert.IsNotNull(playerView.transform.Find("MediaLayer/VideoOutput")?.GetComponent<RawImage>());
                Assert.IsNotNull(playerView.transform.Find("DialoguePanel/ContinueButton")?.GetComponent<Button>());
                AssertFrame(playerView.CurrentFrame, "chapter_01", "line");

                await App.Procedure.ChangeAsync<RecordingProcedure>();
                await UniTask.Yield();

                Assert.IsNull(FindPlayerView());
            });
        }

        [UnityTest]
        public IEnumerator ChangeAsync_WhenLeavingStoryTestProcedure_StopsPlaybackWithoutDestroyingPlayer()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Initialize();
                var playerView = CreatePlayerView();
                var request = new StoryTestRequest(CreateLineProgram("story_test_leave"), null, playerView);

                await App.Procedure.ChangeAsync<StoryTestProcedure>(request);
                Assert.IsNotNull(playerView.CurrentFrame);

                await App.Procedure.ChangeAsync<RecordingProcedure>();

                Assert.IsNull(playerView.CurrentFrame);
                Assert.IsNotNull(playerView);
                Assert.IsInstanceOf<RecordingProcedure>(App.Procedure.Current);
            });
        }

        [UnityTest]
        public IEnumerator ChangeAsync_WhenUserDataIsInvalid_Throws()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Initialize();

                var exception = await ThrowsAsync<GameException>(async () =>
                {
                    await App.Procedure.ChangeAsync<StoryTestProcedure>(null);
                });

                StringAssert.Contains("StoryTestRequest", exception.Message);
            });
        }

        private FrameworkStartup CreateStartup(UnityEngine.Object userData)
        {
            var gameObject = new GameObject("StoryTestFrameworkStartup");
            m_GameObjects.Add(gameObject);

            var startup = gameObject.AddComponent<FrameworkStartup>();
            startup.enabled = false;
            SetField(startup, "m_TargetProcedureTypeName", typeof(StoryTestProcedure).AssemblyQualifiedName);
            SetField(startup, "m_TargetUserData", userData);
            SetField(startup, "m_Modules", new FrameworkStartupModuleOptions());
            return startup;
        }

        private PlayerView CreatePlayerView()
        {
            var view = PlayerView.CreateDefault();
            view.name = "StoryTestPlayerView";
            m_GameObjects.Add(view.gameObject);
            return view;
        }

        private PlayerView CreatePlayerViewPrefab()
        {
            var view = PlayerView.CreateDefault();
            view.name = "StoryTestPlayerViewPrefab";
            view.gameObject.SetActive(false);
            m_GameObjects.Add(view.gameObject);
            return view;
        }

        private static PlayerView FindPlayerView()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<PlayerView>();
#else
            return UnityEngine.Object.FindObjectOfType<PlayerView>();
#endif
        }

        private T CreateObject<T>() where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>();
            m_Objects.Add(value);
            return value;
        }

        private static Program CreateLineProgram(string storyId)
        {
            return new Program(
                storyId,
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "Chapter 01",
                        "line",
                        new[]
                        {
                            new Step(
                                "line",
                                StepKind.Line,
                                new StepData(textKey: "story.test.line")),
                        }),
                });
        }

        private static Program CreateTwoChapterProgram()
        {
            return new Program(
                "story_test_program",
                "1",
                "chapter_01",
                new[]
                {
                    new Chapter(
                        "chapter_01",
                        "Chapter 01",
                        "line_01",
                        new[]
                        {
                            new Step(
                                "line_01",
                                StepKind.Line,
                                new StepData(textKey: "story.test.chapter01")),
                        }),
                    new Chapter(
                        "chapter_02",
                        "Chapter 02",
                        "line_02",
                        new[]
                        {
                            new Step(
                                "line_02",
                                StepKind.Line,
                                new StepData(textKey: "story.test.chapter02")),
                        }),
                });
        }

        private static void AssertFrame(Frame frame, string chapterId, string stepId)
        {
            Assert.IsNotNull(frame);
            Assert.AreEqual(chapterId, frame.Chapter.ChapterId);
            Assert.AreEqual(stepId, frame.AnchorStep.StepId);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            field.SetValue(target, value);
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

        public sealed class RecordingProcedure : ProcedureBase
        {
            public static void Reset()
            {
            }
        }
    }
}
