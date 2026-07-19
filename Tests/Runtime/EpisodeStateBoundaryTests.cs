using System;
using System.Collections.Generic;
using System.Reflection;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.State;
using NUnit.Framework;
using StoryProgram = GameDeveloperKit.Story.Model.Program;

namespace GameDeveloperKit.Tests
{
    public sealed class EpisodeStateBoundaryTests
    {
        [Test]
        public void DefinitionQuery_WhenProgramIsRegistered_ReturnsOriginalRouteDefinitions()
        {
            var program = CreateProgram();
            var module = new StoryModule();
            module.Register(program);

            Assert.IsTrue(module.TryGetProgram("story", out var registered));
            Assert.IsTrue(module.TryGetVolume("story", "volume", out var volume));
            Assert.IsTrue(module.TryGetEpisode("story", "episode", out var episode));

            Assert.AreSame(program, registered);
            Assert.AreSame(program.Volumes[0], volume);
            Assert.AreSame(program.Volumes[0].Episodes[0], episode);
            Assert.AreEqual("edge_root", volume.Route.Edges[0].EdgeId);
            Assert.AreEqual("layout", volume.Layouts[0].LayoutId);
        }

        [Test]
        public void DefinitionQuery_WhenDefinitionDoesNotExist_ReturnsFalseAndNull()
        {
            var module = new StoryModule();
            module.Register(CreateProgram());

            Assert.IsFalse(module.TryGetVolume("missing", "volume", out var missingStoryVolume));
            Assert.IsFalse(module.TryGetEpisode("missing", "episode", out var missingStoryEpisode));
            Assert.IsFalse(module.TryGetVolume("story", "missing", out var missingVolume));
            Assert.IsFalse(module.TryGetEpisode("story", "missing", out var missingEpisode));
            Assert.IsNull(missingStoryVolume);
            Assert.IsNull(missingStoryEpisode);
            Assert.IsNull(missingVolume);
            Assert.IsNull(missingEpisode);
        }

        [Test]
        public void DefinitionQuery_WhenKeyIsEmpty_RejectsInput()
        {
            var module = new StoryModule();

            Assert.Throws<ArgumentException>(() => module.TryGetVolume(" ", "volume", out _));
            Assert.Throws<ArgumentException>(() => module.TryGetVolume("story", " ", out _));
            Assert.Throws<ArgumentException>(() => module.TryGetEpisode(" ", "episode", out _));
            Assert.Throws<ArgumentException>(() => module.TryGetEpisode("story", " ", out _));
        }

        [Test]
        public void StateProvider_WhenBusinessChangesState_ComposesByStoryAndEpisodeOnly()
        {
            var module = new StoryModule();
            module.Register(CreateProgram());
            var provider = new TestStateProvider();
            EpisodeStateChanged observed = default;
            provider.Changed += value => observed = value;

            provider.SetState("story", "episode", EpisodeState.Locked);

            Assert.IsTrue(module.TryGetEpisode("story", "episode", out var episode));
            Assert.AreEqual("Episode", episode.Title);
            Assert.AreEqual(EpisodeState.Locked, provider.GetState("story", "episode"));
            Assert.AreEqual("story", observed.StoryId);
            Assert.AreEqual("episode", observed.EpisodeId);
            Assert.AreEqual(EpisodeState.Locked, observed.State);
            Assert.IsNull(typeof(EpisodeStateChanged).GetProperty("VolumeId"));
        }

        [Test]
        public void StartEpisode_WhenBusinessStateIsHidden_DoesNotApplyPlaybackGate()
        {
            AssertRestrictedStateDoesNotApplyPlaybackGate(EpisodeState.Hidden);
        }

        [Test]
        public void StartEpisode_WhenBusinessStateIsLocked_DoesNotApplyPlaybackGate()
        {
            AssertRestrictedStateDoesNotApplyPlaybackGate(EpisodeState.Locked);
        }

        private static void AssertRestrictedStateDoesNotApplyPlaybackGate(EpisodeState state)
        {
            var module = new StoryModule();
            module.Register(CreateProgram());
            var provider = new TestStateProvider();
            provider.SetState("story", "episode", state);

            var runner = module.StartEpisode("story", "volume", "episode");

            Assert.AreEqual(state, provider.GetState("story", "episode"));
            Assert.AreEqual("episode", runner.CurrentEpisodeId);
            Assert.AreEqual("end", runner.CurrentStepId);
        }

        [Test]
        public void StateContract_WhenInspected_IsReadOnlyAndModuleDoesNotOwnIt()
        {
            CollectionAssert.AreEqual(
                new[] { EpisodeState.Hidden, EpisodeState.Locked, EpisodeState.Available },
                (EpisodeState[])Enum.GetValues(typeof(EpisodeState)));

            var providerType = typeof(IEpisodeStateProvider);
            var getState = providerType.GetMethod("GetState");
            Assert.IsNotNull(getState);
            Assert.AreEqual(typeof(EpisodeState), getState.ReturnType);
            Assert.AreEqual(2, getState.GetParameters().Length);
            Assert.AreEqual("storyId", getState.GetParameters()[0].Name);
            Assert.AreEqual("episodeId", getState.GetParameters()[1].Name);
            Assert.AreEqual(1, providerType.GetEvents().Length);
            Assert.AreEqual("Changed", providerType.GetEvents()[0].Name);
            Assert.AreEqual(0, providerType.GetProperties().Length);

            var forbiddenNames = new[]
            {
                "SetEpisodeVisibility",
                "SetEpisodeState",
                "UnlockEpisode",
                "GetExplorationProgress",
                "SetStateProvider"
            };
            var members = typeof(StoryModule).GetMembers(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (var i = 0; i < members.Length; i++)
            {
                CollectionAssert.DoesNotContain(forbiddenNames, members[i].Name);
                Assert.IsFalse(ReferencesStateContract(members[i]), members[i].Name);
            }
        }

        [Test]
        public void EpisodeStateChanged_WhenIdentityOrStateIsInvalid_RejectsValue()
        {
            Assert.Throws<ArgumentNullException>(() => new EpisodeStateChanged(null, "episode", EpisodeState.Available));
            Assert.Throws<ArgumentException>(() => new EpisodeStateChanged("story", " ", EpisodeState.Available));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EpisodeStateChanged("story", "episode", (EpisodeState)99));
        }

        private static bool ReferencesStateContract(MemberInfo member)
        {
            switch (member)
            {
                case FieldInfo field:
                    return IsStateContract(field.FieldType);
                case PropertyInfo property:
                    return IsStateContract(property.PropertyType);
                case EventInfo eventInfo:
                    return IsStateContract(eventInfo.EventHandlerType);
                case MethodInfo method:
                    if (IsStateContract(method.ReturnType))
                    {
                        return true;
                    }

                    var parameters = method.GetParameters();
                    for (var i = 0; i < parameters.Length; i++)
                    {
                        if (IsStateContract(parameters[i].ParameterType))
                        {
                            return true;
                        }
                    }

                    return false;
                default:
                    return false;
            }
        }

        private static bool IsStateContract(Type type)
        {
            if (type == null)
            {
                return false;
            }

            if (type.IsByRef || type.IsArray)
            {
                type = type.GetElementType();
            }

            return type == typeof(EpisodeState) ||
                   type == typeof(EpisodeStateChanged) ||
                   type == typeof(IEpisodeStateProvider);
        }

        private static StoryProgram CreateProgram()
        {
            var episode = new Episode(
                "episode",
                "Episode",
                "start",
                new[] { new EpisodeExit("done") },
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("end"))),
                    new Step("end", StepKind.End, new StepData(exitId: "done"))
                });
            var route = new Route(new[] { RouteEdge.FromRoot("edge_root", "episode") });
            var layout = new RouteLayout(
                "layout",
                LayoutOrientation.Landscape,
                1920,
                1080,
                null,
                new Placement(160f, 120f),
                new[] { new EpisodePlacement("episode", new Placement(640f, 360f)) },
                new[] { new RouteEdgePlacement("edge_root", Array.Empty<Placement>()) });
            return new StoryProgram(
                "story",
                "1",
                new[]
                {
                    new Volume(
                        "volume",
                        "Volume",
                        new[] { episode },
                        route,
                        layouts: new[] { layout })
                });
        }

        private sealed class TestStateProvider : IEpisodeStateProvider
        {
            private readonly Dictionary<string, EpisodeState> m_States =
                new Dictionary<string, EpisodeState>(StringComparer.Ordinal);

            public event Action<EpisodeStateChanged> Changed;

            public EpisodeState GetState(string storyId, string episodeId)
            {
                return m_States.TryGetValue(Key(storyId, episodeId), out var state)
                    ? state
                    : EpisodeState.Available;
            }

            public void SetState(string storyId, string episodeId, EpisodeState state)
            {
                var changed = new EpisodeStateChanged(storyId, episodeId, state);
                m_States[Key(storyId, episodeId)] = state;
                Changed?.Invoke(changed);
            }

            private static string Key(string storyId, string episodeId)
            {
                return storyId + "\n" + episodeId;
            }
        }
    }
}
