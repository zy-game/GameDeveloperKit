using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Model;
using NUnit.Framework;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryEpisodeCompletionTests
    {
        [Test]
        public void TransitionCompletion_NotifiesSourceBeforeStartingTargetAndChains()
        {
            var first = TransitionEpisode("episode_a", "transition_a");
            var second = TransitionEpisode("episode_b", "transition_b");
            var target = ActiveEpisode("episode_c");
            var program = Program(
                new[] { first, second, target },
                RouteEdge.FromRoot("root_a", first.EpisodeId),
                RouteEdge.FromExit("edge_ab", first.EpisodeId, "transition_a", second.EpisodeId),
                RouteEdge.FromExit("edge_bc", second.EpisodeId, "transition_b", target.EpisodeId));
            var module = new StoryModule();
            var completions = new List<EpisodeCompletion>();
            var currentEpisodesDuringNotification = new List<string>();
            module.Register(program);
            module.EpisodeCompleted += completion =>
            {
                completions.Add(completion);
                currentEpisodesDuringNotification.Add(module.CurrentRunner.CurrentEpisodeId);
            };

            var runner = module.StartEpisode(program.StoryId, "volume", first.EpisodeId);

            Assert.AreSame(module.CurrentRunner, runner);
            Assert.AreEqual(target.EpisodeId, module.CurrentFrame.Episode.EpisodeId);
            Assert.AreEqual("line", module.CurrentFrame.AnchorStep.StepId);
            CollectionAssert.AreEqual(new[] { first.EpisodeId, second.EpisodeId }, currentEpisodesDuringNotification);
            CollectionAssert.AreEqual(
                new[] { EpisodeCompletionKind.Transition, EpisodeCompletionKind.Transition },
                completions.Select(x => x.Kind));
            CollectionAssert.AreEqual(new[] { second.EpisodeId, target.EpisodeId }, completions.Select(x => x.NextEpisodeId));
            CollectionAssert.AreEqual(new[] { "edge_ab", "edge_bc" }, completions.Select(x => x.RouteEdgeId));
        }

        [Test]
        public void ConvergedTransitions_FromEitherSourceStartSharedTarget()
        {
            var first = TransitionEpisode("episode_a", "transition_a");
            var second = TransitionEpisode("episode_b", "transition_b");
            var target = ActiveEpisode("episode_c");
            var program = Program(
                new[] { first, second, target },
                RouteEdge.FromRoot("root_a", first.EpisodeId),
                RouteEdge.FromRoot("root_b", second.EpisodeId),
                RouteEdge.FromExit("edge_ac", first.EpisodeId, "transition_a", target.EpisodeId),
                RouteEdge.FromExit("edge_bc", second.EpisodeId, "transition_b", target.EpisodeId));
            var module = new StoryModule();
            var completions = new List<EpisodeCompletion>();
            module.Register(program);
            module.EpisodeCompleted += completions.Add;

            module.StartEpisode(program.StoryId, "volume", first.EpisodeId);
            Assert.AreEqual(target.EpisodeId, module.CurrentRunner.CurrentEpisodeId);
            module.StartEpisode(program.StoryId, "volume", second.EpisodeId);
            Assert.AreEqual(target.EpisodeId, module.CurrentRunner.CurrentEpisodeId);

            CollectionAssert.AreEqual(
                new[] { first.EpisodeId, second.EpisodeId },
                completions.Select(x => x.EpisodeId));
            Assert.IsTrue(completions.All(x => x.NextEpisodeId == target.EpisodeId));
        }

        [Test]
        public void NonTransitionCompletion_NotifiesWithRouteContextWithoutStartingTarget()
        {
            var choice = ChoiceEpisode("episode_choice", "choice_exit");
            var settlement = EndEpisode("episode_settlement", "settlement_end", "  reward.route-a  ");
            var ordinaryEnd = EndEpisode("episode_end", "ordinary_end", "   ");
            var natural = NaturalEpisode("episode_natural");
            var target = ActiveEpisode("episode_target");
            var program = Program(
                new[] { choice, settlement, ordinaryEnd, natural, target },
                RouteEdge.FromRoot("root_choice", choice.EpisodeId),
                RouteEdge.FromRoot("root_settlement", settlement.EpisodeId),
                RouteEdge.FromRoot("root_end", ordinaryEnd.EpisodeId),
                RouteEdge.FromRoot("root_natural", natural.EpisodeId),
                RouteEdge.FromExit("edge_choice_target", choice.EpisodeId, "choice_exit", target.EpisodeId),
                RouteEdge.FromExit("edge_end_target", settlement.EpisodeId, "settlement_end", target.EpisodeId));
            var module = new StoryModule();
            var completions = new List<EpisodeCompletion>();
            module.Register(program);
            module.EpisodeCompleted += completions.Add;

            module.StartEpisode(program.StoryId, "volume", choice.EpisodeId);
            var choiceFrame = module.Select("choice_button");
            Assert.IsTrue(choiceFrame.IsCompleted);
            Assert.AreEqual(choice.EpisodeId, module.CurrentRunner.CurrentEpisodeId);
            Assert.AreEqual(target.EpisodeId, completions[0].NextEpisodeId);

            var settlementFrame = module.StartEpisode(program.StoryId, "volume", settlement.EpisodeId).CurrentFrame;
            Assert.IsTrue(settlementFrame.IsCompleted);
            Assert.AreEqual(settlement.EpisodeId, module.CurrentRunner.CurrentEpisodeId);
            Assert.AreEqual("reward.route-a", completions[1].SettlementId);
            Assert.AreEqual(target.EpisodeId, completions[1].NextEpisodeId);

            module.StartEpisode(program.StoryId, "volume", ordinaryEnd.EpisodeId);
            module.StartEpisode(program.StoryId, "volume", natural.EpisodeId);
            var naturalSnapshot = module.CreateSnapshot();
            var restoredNatural = module.Restore(naturalSnapshot);

            CollectionAssert.AreEqual(
                new[]
                {
                    EpisodeCompletionKind.Choice,
                    EpisodeCompletionKind.End,
                    EpisodeCompletionKind.End,
                    EpisodeCompletionKind.Natural
                },
                completions.Select(x => x.Kind));
            Assert.IsNull(completions[2].SettlementId);
            Assert.IsNull(completions[3].ExitId);
            Assert.IsNull(completions[3].NextEpisodeId);
            Assert.AreEqual(EpisodeCompletionKind.Natural, naturalSnapshot.CompletedKind);
            Assert.AreEqual("start", naturalSnapshot.StepId);
            Assert.IsTrue(restoredNatural.CurrentFrame.IsCompleted);
            Assert.AreEqual(EpisodeCompletionKind.Natural, restoredNatural.CurrentFrame.CompletedKind);
            Assert.AreEqual(4, completions.Count);
        }

        [Test]
        public void TransitionCompletion_WhenHandlerThrows_KeepsCompletedSourceAndDoesNotStartTarget()
        {
            var source = TransitionEpisode("episode_source", "transition");
            var target = ActiveEpisode("episode_target");
            var program = Program(
                new[] { source, target },
                RouteEdge.FromRoot("root", source.EpisodeId),
                RouteEdge.FromExit("edge", source.EpisodeId, "transition", target.EpisodeId));
            var module = new StoryModule();
            var notifications = 0;
            module.Register(program);
            module.EpisodeCompleted += _ =>
            {
                notifications++;
                throw new InvalidOperationException("settlement failed");
            };

            var exception = Assert.Throws<InvalidOperationException>(() =>
                module.StartEpisode(program.StoryId, "volume", source.EpisodeId));

            Assert.AreEqual("settlement failed", exception.Message);
            Assert.AreEqual(1, notifications);
            Assert.AreEqual(source.EpisodeId, module.CurrentRunner.CurrentEpisodeId);
            Assert.IsTrue(module.CurrentFrame.IsCompleted);
            Assert.AreEqual(EpisodeCompletionKind.Transition, module.CurrentFrame.CompletedKind);
        }

        [Test]
        public void ProgramAssetAndCompletedSnapshots_PreserveTransitionAndSettlementWithoutReplay()
        {
            var transition = TransitionEpisode("episode_transition", "transition");
            var ending = EndEpisode("episode_end", "end", "reward.final");
            var program = Program(
                new[] { transition, ending },
                RouteEdge.FromRoot("root", transition.EpisodeId),
                RouteEdge.FromExit("edge", transition.EpisodeId, "transition", ending.EpisodeId));
            var asset = ScriptableObject.CreateInstance<ProgramAsset>();
            try
            {
                asset.SetProgram(program);
                var restoredProgram = asset.ToProgram();
                var restoredTransition = restoredProgram.Volumes[0].Episodes[0].Steps
                    .Single(x => x.Kind == StepKind.Transition);
                var restoredEnd = restoredProgram.Volumes[0].Episodes[1].Steps
                    .Single(x => x.Kind == StepKind.End);
                Assert.AreEqual(StepKind.Transition, restoredTransition.Kind);
                Assert.AreEqual("transition", restoredTransition.Data.ExitId);
                Assert.AreEqual("reward.final", restoredEnd.Data.SettlementId);

                var endRunner = new Runner(restoredProgram);
                endRunner.Start("volume", ending.EpisodeId);
                var endSnapshot = endRunner.CreateSnapshot();
                Assert.AreEqual(EpisodeCompletionKind.End, endSnapshot.CompletedKind);
                Assert.AreEqual("reward.final", endSnapshot.CompletedSettlementId);

                var transitionRunner = new Runner(restoredProgram);
                transitionRunner.Start("volume", transition.EpisodeId);
                var transitionSnapshot = transitionRunner.CreateSnapshot();
                Assert.AreEqual(EpisodeCompletionKind.Transition, transitionSnapshot.CompletedKind);

                var module = new StoryModule();
                var notifications = 0;
                module.Register(restoredProgram);
                module.EpisodeCompleted += _ => notifications++;
                var restoredEndRunner = module.Restore(endSnapshot);
                Assert.AreEqual(EpisodeCompletionKind.End, restoredEndRunner.CurrentFrame.CompletedKind);
                Assert.AreEqual("reward.final", restoredEndRunner.CurrentFrame.CompletedSettlementId);
                var restoredTransitionRunner = module.Restore(transitionSnapshot);
                Assert.AreEqual(transition.EpisodeId, restoredTransitionRunner.CurrentEpisodeId);
                Assert.AreEqual(EpisodeCompletionKind.Transition, restoredTransitionRunner.CurrentFrame.CompletedKind);
                Assert.AreEqual(0, notifications);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private static Episode TransitionEpisode(string episodeId, string exitId)
        {
            return new Episode(
                episodeId,
                episodeId,
                "start",
                new[] { new EpisodeExit(exitId) },
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step(exitId))),
                    new Step(exitId, StepKind.Transition, new StepData(exitId: exitId))
                });
        }

        private static Episode EndEpisode(string episodeId, string exitId, string settlementId)
        {
            return new Episode(
                episodeId,
                episodeId,
                "start",
                new[] { new EpisodeExit(exitId) },
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step(exitId))),
                    new Step(exitId, StepKind.End, new StepData(exitId: exitId, settlementId: settlementId))
                });
        }

        private static Episode ChoiceEpisode(string episodeId, string exitId)
        {
            return new Episode(
                episodeId,
                episodeId,
                "start",
                new[] { new EpisodeExit(exitId) },
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("choice"))),
                    new Step(
                        "choice",
                        StepKind.Choice,
                        new StepData(choices: new[] { new Choice("choice_button", exitId, "Choose") }))
                });
        }

        private static Episode NaturalEpisode(string episodeId)
        {
            return new Episode(
                episodeId,
                episodeId,
                "start",
                Array.Empty<EpisodeExit>(),
                new[] { new Step("start", StepKind.Start) });
        }

        private static Episode ActiveEpisode(string episodeId)
        {
            return new Episode(
                episodeId,
                episodeId,
                "start",
                Array.Empty<EpisodeExit>(),
                new[]
                {
                    new Step("start", StepKind.Start, new StepData(target: Target.Step("line"))),
                    new Step("line", StepKind.Line, new StepData(textKey: "active"))
                });
        }

        private static Program Program(IReadOnlyList<Episode> episodes, params RouteEdge[] edges)
        {
            return new Program(
                "completion_story",
                "1",
                new[] { new Volume("volume", "Volume", episodes, new Route(edges)) });
        }
    }
}
