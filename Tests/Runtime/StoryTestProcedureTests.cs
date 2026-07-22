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
        public IEnumerator StartupAsync_WithStoryTestRequestAsset_EntersProcedureAndPlaysStory()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var programAsset = CreateObject<ProgramAsset>();
                programAsset.SetProgram(CreateLineProgram("story_test_asset"));
                var requestAsset = CreateObject<StoryTestRequestAsset>();
                SetField(requestAsset, "m_ProgramAsset", programAsset);
                SetField(requestAsset, "m_VolumeId", StoryProgramTestFactory.VolumeId);
                SetField(requestAsset, "m_EpisodeId", "episode_01");
                StartupLoadingTestFixture.Prepare();
                var startup = CreateStartup(requestAsset);

                await startup.StartupAsync();

                Assert.IsInstanceOf<StoryTestProcedure>(App.Procedure.Current);
                Assert.IsTrue(App.Story.HasProgram("story_test_asset"));
                Assert.IsTrue(App.UI.TryGet<PlaybackView>(out var playbackView));
                AssertFrame(playbackView.CurrentFrame, "episode_01", "line");
                Assert.IsNull(playbackView.LastError);
            });
        }

        [UnityTest]
        public IEnumerator ChangeAsync_WithStoryTestRequest_RegistersProgramAndPlaysEpisode()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Initialize();
                var program = CreateTwoEpisodeProgram();
                var request = new StoryTestRequest(program, StoryProgramTestFactory.VolumeId, "episode_02");

                await App.Procedure.ChangeAsync<StoryTestProcedure>(request);

                Assert.IsInstanceOf<StoryTestProcedure>(App.Procedure.Current);
                Assert.IsTrue(App.Story.HasProgram("story_test_program"));
                Assert.IsTrue(App.UI.TryGet<PlaybackView>(out var playbackView));
                AssertFrame(playbackView.CurrentFrame, "episode_02", "line_02");
            });
        }

        [UnityTest]
        public IEnumerator ChangeAsync_WithRegisteredStoryId_PlaysRegisteredStory()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Initialize();
                App.Story.Register(CreateLineProgram("story_test_registered"));
                var request = new StoryTestRequest("story_test_registered", StoryProgramTestFactory.VolumeId, "episode_01");

                await App.Procedure.ChangeAsync<StoryTestProcedure>(request);

                Assert.IsTrue(App.UI.TryGet<PlaybackView>(out var playbackView));
                AssertFrame(playbackView.CurrentFrame, "episode_01", "line");
                Assert.IsNull(playbackView.LastError);
            });
        }

        [UnityTest]
        public IEnumerator ChangeAsync_OpensPlaybackViewInStoryPlaybackLayer()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Initialize();
                var request = new StoryTestRequest(
                    CreateLineProgram("story_test_default_player"),
                    StoryProgramTestFactory.VolumeId,
                    "episode_01");

                await App.Procedure.ChangeAsync<StoryTestProcedure>(request);

                Assert.IsTrue(App.UI.TryGet<PlaybackView>(out var playbackView));
                Assert.AreSame(App.UI.GetLayerRoot(UILayer.StoryPlayback), playbackView.GameObject.transform.parent);
                Assert.IsNotNull(playbackView.Document.GetComponent<RawImage>("VideoOutput"));
                Assert.IsNotNull(playbackView.Document.GetComponent<Button>("ContinueButton"));
                AssertFrame(playbackView.CurrentFrame, "episode_01", "line");

                await App.Procedure.ChangeAsync<RecordingProcedure>();
                await UniTask.Yield();

                Assert.IsFalse(App.UI.IsOpen<PlaybackView>());
            });
        }

        [UnityTest]
        public IEnumerator ChangeAsync_WhenLeavingStoryTestProcedure_StopsAndClosesPlaybackView()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Initialize();
                var request = new StoryTestRequest(
                    CreateLineProgram("story_test_leave"),
                    StoryProgramTestFactory.VolumeId,
                    "episode_01");

                await App.Procedure.ChangeAsync<StoryTestProcedure>(request);
                Assert.IsTrue(App.UI.TryGet<PlaybackView>(out var playbackView));
                Assert.IsNotNull(playbackView.CurrentFrame);

                await App.Procedure.ChangeAsync<RecordingProcedure>();

                Assert.IsNull(playbackView.CurrentFrame);
                Assert.IsNull(playbackView.GameObject);
                Assert.IsFalse(App.UI.IsOpen<PlaybackView>());
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

        private T CreateObject<T>() where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>();
            m_Objects.Add(value);
            return value;
        }

        private static Program CreateLineProgram(string storyId)
        {
            return StoryProgramTestFactory.Program(
                storyId,
                "1",
                "episode_01",
                new[]
                {
                    StoryProgramTestFactory.Episode(
                        "episode_01",
                        "Episode 01",
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

        private static Program CreateTwoEpisodeProgram()
        {
            return StoryProgramTestFactory.Program(
                "story_test_program",
                "1",
                "episode_01",
                new[]
                {
                    StoryProgramTestFactory.Episode(
                        "episode_01",
                        "Episode 01",
                        "line_01",
                        new[]
                        {
                            new Step(
                                "line_01",
                                StepKind.Line,
                                new StepData(textKey: "story.test.episode01")),
                        }),
                    StoryProgramTestFactory.Episode(
                        "episode_02",
                        "Episode 02",
                        "line_02",
                        new[]
                        {
                            new Step(
                                "line_02",
                                StepKind.Line,
                                new StepData(textKey: "story.test.episode02")),
                        }),
                });
        }

        private static void AssertFrame(Frame frame, string episodeId, string stepId)
        {
            Assert.IsNotNull(frame);
            Assert.AreEqual(episodeId, frame.Episode.EpisodeId);
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
