using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Event;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.Story.State;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;
using NUnit.Framework;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryRouteAuthoringAcceptanceTests
    {
        [Test]
        public void CanonicalSample_WhenCompiled_CoversMultiVolumeRouteAuthoringContracts()
        {
            var asset = SampleGraphFixture.Create();
            try
            {
                var authoringReport = AuthoringValidator.Validate(asset);
                AssertNoErrors(authoringReport.Issues);

                var program = ProgramCompiler.Compile(asset, out var compileReport);

                AssertNoErrors(compileReport.Issues);
                Assert.IsNotNull(program);
                Assert.AreEqual(2, program.Volumes.Count);
                AssertTree(program.Volumes.Single(x => x.VolumeId == SampleGraphFixture.PrimaryVolumeId));
                AssertTree(program.Volumes.Single(x => x.VolumeId == SampleGraphFixture.SecondaryVolumeId));

                var primary = program.Volumes.Single(x => x.VolumeId == SampleGraphFixture.PrimaryVolumeId);
                var arrival = FindEpisode(program, SampleGraphFixture.RootEpisodeId);
                var choice = arrival.Steps.Single(x => x.Kind == StepKind.Choice);
                Assert.AreEqual(2, choice.Choices.Count);
                CollectionAssert.AreEquivalent(
                    new[] { "choice_enter_alley", "choice_help_guard" },
                    choice.Choices.Select(x => x.ExitId).ToArray());
                Assert.AreEqual(2, primary.Route.Edges.Count(x =>
                    x.SourceKind == RouteEdgeSourceKind.EpisodeExit &&
                    x.FromEpisodeId == SampleGraphFixture.RootEpisodeId));

                AssertEvent(program, "episode_alley", "alley_minigame", EventMode.Request, "sample.minigame.lockpick");
                AssertEvent(program, "episode_final", "final_emit_event", EventMode.Notify, "sample.story.completed");
                var settlement = FindStep(program, "episode_final", "final_settlement");
                Assert.AreEqual(StepKind.Command, settlement.Kind);
                Assert.AreEqual(SettlementCommandNames.SettleEpisode, settlement.Data.Command.Name);

                var landscape = primary.Layouts.Single(x => x.Orientation == LayoutOrientation.Landscape);
                var portrait = primary.Layouts.Single(x => x.Orientation == LayoutOrientation.Portrait);
                AssertFinite(landscape.RootPlacement);
                AssertFinite(portrait.RootPlacement);
                Assert.IsTrue(landscape.Episodes.All(x => IsFinite(x.Position)));
                Assert.IsTrue(portrait.Episodes.All(x => IsFinite(x.Position)));
                AssertEdgePath(landscape, IdentityId.RootEdge(SampleGraphFixture.RootEpisodeId));
                AssertEdgePath(portrait, IdentityId.RootEdge(SampleGraphFixture.RootEpisodeId));

                var secondary = program.Volumes.Single(x => x.VolumeId == SampleGraphFixture.SecondaryVolumeId);
                Assert.AreEqual(SampleGraphFixture.SecondaryRootEpisodeId, secondary.Route.Edges.Single().ToEpisodeId);
                AssertEdgePath(secondary.Layouts.Single(), IdentityId.RootEdge(SampleGraphFixture.SecondaryRootEpisodeId));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CanonicalSample_WhenBusinessStateAndMetadataChange_IdentityAndOwnershipStayExternal()
        {
            var asset = SampleGraphFixture.Create();
            try
            {
                var program = ProgramCompiler.Compile(asset, out var compileReport);
                AssertNoErrors(compileReport.Issues);
                var baseline = IdentityManifest.Create(program);

                asset.Volumes[0].Title = "第一卷：重命名不改变身份";
                asset.Volumes[0].Layouts[0].RootPlacement.Position = new Vector2(160f, 540f);
                var recompiled = ProgramCompiler.Compile(asset, out var changedReport);
                AssertNoErrors(changedReport.Issues);
                var identityChanges = IdentityChangeReport.Compare(baseline, IdentityManifest.Create(recompiled));
                Assert.AreEqual(0, identityChanges.AddedEpisodeIds.Count);
                Assert.AreEqual(0, identityChanges.RemovedEpisodeIds.Count);
                Assert.AreEqual(0, identityChanges.AddedEdgeIds.Count);
                Assert.AreEqual(0, identityChanges.RemovedEdgeIds.Count);
                Assert.AreEqual(0, identityChanges.RemovedExits.Count);

                var businessState = new BusinessEpisodeStateProvider();
                businessState.SetState(
                    SampleGraphFixture.StoryId,
                    SampleGraphFixture.SecondaryRootEpisodeId,
                    EpisodeState.Hidden);

                var module = new StoryModule();
                module.Startup();
                try
                {
                    module.Register(recompiled);
                    Assert.IsTrue(module.TryGetVolume(
                        SampleGraphFixture.StoryId,
                        SampleGraphFixture.SecondaryVolumeId,
                        out var volume));
                    Assert.IsTrue(module.TryGetEpisode(
                        SampleGraphFixture.StoryId,
                        SampleGraphFixture.SecondaryRootEpisodeId,
                        out var episode));
                    Assert.AreEqual(SampleGraphFixture.SecondaryVolumeId, volume.VolumeId);
                    Assert.AreEqual(SampleGraphFixture.SecondaryRootEpisodeId, episode.EpisodeId);
                    Assert.AreEqual(
                        EpisodeState.Hidden,
                        businessState.GetState(SampleGraphFixture.StoryId, SampleGraphFixture.SecondaryRootEpisodeId));

                    var runner = module.StartEpisode(
                        SampleGraphFixture.StoryId,
                        SampleGraphFixture.SecondaryVolumeId,
                        SampleGraphFixture.SecondaryRootEpisodeId);
                    Assert.AreEqual(SampleGraphFixture.SecondaryRootEpisodeId, runner.CurrentFrame.Episode.EpisodeId);
                }
                finally
                {
                    module.Shutdown();
                }

                Assert.IsFalse(typeof(StoryModule)
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Any(field => typeof(IEpisodeStateProvider).IsAssignableFrom(field.FieldType)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private static void AssertTree(Volume volume)
        {
            var root = volume.Route.Edges.Single(x => x.SourceKind == RouteEdgeSourceKind.Root);
            foreach (var episode in volume.Episodes)
            {
                Assert.AreEqual(1, volume.Route.Edges.Count(x => x.ToEpisodeId == episode.EpisodeId), episode.EpisodeId);
            }

            var reachable = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Queue<string>();
            pending.Enqueue(root.ToEpisodeId);
            while (pending.Count > 0)
            {
                var episodeId = pending.Dequeue();
                Assert.IsTrue(reachable.Add(episodeId), $"Route contains a cycle at {episodeId}.");
                foreach (var edge in volume.Route.Edges.Where(x =>
                             x.SourceKind == RouteEdgeSourceKind.EpisodeExit &&
                             x.FromEpisodeId == episodeId))
                {
                    pending.Enqueue(edge.ToEpisodeId);
                }
            }

            CollectionAssert.AreEquivalent(volume.Episodes.Select(x => x.EpisodeId).ToArray(), reachable.ToArray());
        }

        private static void AssertEvent(
            GameDeveloperKit.Story.Model.Program program,
            string episodeId,
            string stepId,
            EventMode mode,
            string eventId)
        {
            var step = FindStep(program, episodeId, stepId);
            Assert.AreEqual(StepKind.Command, step.Kind);
            Assert.IsTrue(EventCommandCodec.TryDecode(step.Data.Command, out var request, out var error), error);
            Assert.AreEqual(mode, request.Mode);
            Assert.AreEqual(eventId, request.EventId);
        }

        private static void AssertEdgePath(RouteLayout layout, string edgeId)
        {
            var placement = layout.Edges.Single(x => x.EdgeId == edgeId);
            Assert.AreEqual("main", placement.StyleKey);
            Assert.AreEqual(2, placement.ControlPoints.Count);
        }

        private static void AssertFinite(Placement placement)
        {
            Assert.IsTrue(IsFinite(placement), $"Placement is not finite: ({placement.X}, {placement.Y}).");
        }

        private static bool IsFinite(Placement placement)
        {
            return float.IsNaN(placement.X) is false &&
                   float.IsInfinity(placement.X) is false &&
                   float.IsNaN(placement.Y) is false &&
                   float.IsInfinity(placement.Y) is false;
        }

        private static Episode FindEpisode(GameDeveloperKit.Story.Model.Program program, string episodeId)
        {
            return program.Volumes.SelectMany(x => x.Episodes)
                .Single(x => x.EpisodeId == episodeId);
        }

        private static Step FindStep(GameDeveloperKit.Story.Model.Program program, string episodeId, string stepId)
        {
            return FindEpisode(program, episodeId).Steps.Single(x => x.StepId == stepId);
        }

        private static void AssertNoErrors(IEnumerable<ValidationIssue> issues)
        {
            Assert.IsFalse(
                issues.Any(x => x.Severity == ValidationSeverity.Error),
                string.Join(Environment.NewLine, issues.Select(x => x.ToString())));
        }

        private sealed class BusinessEpisodeStateProvider : IEpisodeStateProvider
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
                return storyId + ":" + episodeId;
            }
        }
    }
}
