using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Event;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Settlement;
using GameDeveloperKit.StoryEditor.Migration;
using GameDeveloperKit.StoryEditor.Model;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryRouteMigrationAnalyzeTests
    {
        private readonly List<UnityEngine.Object> m_Objects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < m_Objects.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(m_Objects[i]);
            }

            m_Objects.Clear();
        }

        [Test]
        public void Analyze_WhenLegacyJumpAndLayoutExist_BuildsCandidateWithoutChangingSource()
        {
            var asset = CreateAsset();
            var first = AddEpisode(asset, "episode_a", Node("start", NodeKind.Start), Node("jump", (NodeKind)LegacyNodeKinds.JumpEpisode));
            var second = AddEpisode(asset, "episode_b", Node("target_start", NodeKind.Start), Node("target_end", NodeKind.End));
            first.EntryNodeId = "start";
            second.EntryNodeId = "target_start";
            first.Edges.Add(Edge("start_jump", "start", "completed", "jump"));
            first.Nodes[1].Parameters.Add(new AuthoringParameter { Key = "chapterId", Value = "episode_b" });
            asset.LegacyEntryEpisodeId = "episode_a";
            asset.LegacyDetailLayout.Nodes.Add(new EpisodeNodePlacement
            {
                LegacyGraphId = "episode_a",
                NodeId = "jump",
                Position = new Vector2(120f, 80f)
            });

            using (var preview = Analyze(asset))
            {
                Assert.IsTrue(preview.Report.CanApply);
                Assert.IsFalse(preview.IsNoOp);
                Assert.IsNull(asset.Volumes[0].Route);
                Assert.AreEqual(LegacyNodeKinds.JumpEpisode, (int)first.Nodes[1].NodeKind);
                var migrated = preview.Candidate.FindEpisode("episode_a");
                Assert.AreEqual(NodeKind.End, migrated.Nodes.Single(x => x.NodeId == "jump").NodeKind);
                Assert.AreEqual(1, migrated.DetailLayout.Nodes.Count);
                Assert.AreEqual(2, preview.Candidate.Volumes[0].Route.Edges.Count);
                Assert.AreEqual(RouteEdgeSourceKind.Root, preview.Candidate.Volumes[0].Route.Edges[0].SourceKind);
                Assert.AreEqual("episode_b", preview.Candidate.Volumes[0].Route.Edges[1].ToEpisodeId);
            }
        }

        [Test]
        public void Analyze_WhenChoiceOwnsExclusiveBranch_ExtractsChildEpisode()
        {
            var asset = CreateChoiceAsset(false, false);

            using (var preview = Analyze(asset))
            {
                Assert.IsTrue(preview.Report.CanApply);
                Assert.AreEqual(1, asset.Volumes[0].Episodes.Count);
                Assert.AreEqual(2, preview.Candidate.Volumes[0].Episodes.Count);
                var parent = preview.Candidate.FindEpisode("episode_a");
                var child = preview.Candidate.Volumes[0].Episodes.Single(x => x.EpisodeId != "episode_a");
                Assert.IsFalse(parent.Nodes.Any(x => x.NodeId == "branch_line"));
                Assert.IsTrue(child.Nodes.Any(x => x.NodeId == "branch_line"));
                Assert.IsTrue(child.Nodes.Any(x => x.NodeKind == NodeKind.Start));
                Assert.IsFalse(parent.Edges.Any(x => string.Equals(x.FromPortId, "selected", StringComparison.Ordinal)));
                Assert.IsTrue(preview.Candidate.Volumes[0].Route.Edges.Any(x =>
                    x.SourceKind == RouteEdgeSourceKind.EpisodeExit &&
                    x.FromExitId == "choice" &&
                    x.ToEpisodeId == child.EpisodeId));
            }
        }

        [Test]
        public void Analyze_WhenChoiceBranchesShareSuffix_ReportsStableConflict()
        {
            var asset = CreateChoiceAsset(true, false);

            using (var preview = Analyze(asset))
            {
                Assert.IsFalse(preview.Report.CanApply);
                Assert.AreEqual("branch_shared_node", preview.Report.Issues.Single().Code);
                StringAssert.Contains("episode:episode_a/node:shared", preview.Report.Issues.Single().Location);
                Assert.IsNull(asset.Volumes[0].Route);
            }
        }

        [Test]
        public void Analyze_WhenChoiceBranchCycles_ReportsConflict()
        {
            var asset = CreateChoiceAsset(false, true);

            using (var preview = Analyze(asset))
            {
                Assert.IsFalse(preview.Report.CanApply);
                Assert.AreEqual("branch_cycle", preview.Report.Issues.Single().Code);
                Assert.IsNull(asset.Volumes[0].Route);
            }
        }

        [Test]
        public void Analyze_WhenChoiceTargetIsMissing_ReportsLocatedConflict()
        {
            var asset = CreateChoiceAsset(false, false);
            asset.Volumes[0].Episodes[0].Edges.Single(x => x.EdgeId == "choice_branch").TargetNodeId = "missing";

            using (var preview = Analyze(asset))
            {
                Assert.IsFalse(preview.Report.CanApply);
                Assert.AreEqual("missing_target_node", preview.Report.Issues.Single().Code);
                StringAssert.Contains("episode:episode_a/node:missing", preview.Report.Issues.Single().Location);
            }
        }

        [Test]
        public void Analyze_WhenChoiceBranchHasMultipleIncoming_ReportsConflict()
        {
            var asset = CreateChoiceAsset(false, false);
            var episode = asset.Volumes[0].Episodes[0];
            episode.Nodes.Add(Node("branch_side", NodeKind.Dialogue));
            episode.Nodes.Add(Node("branch_shared", NodeKind.End));
            episode.Edges.RemoveAll(x => x.EdgeId == "branch_end");
            episode.Edges.Add(Edge("branch_main_shared", "branch_line", "completed", "branch_shared"));
            episode.Edges.Add(Edge("branch_main_side", "branch_line", "alternate", "branch_side"));
            episode.Edges.Add(Edge("branch_side_shared", "branch_side", "completed", "branch_shared"));

            using (var preview = Analyze(asset))
            {
                Assert.IsFalse(preview.Report.CanApply);
                Assert.AreEqual("branch_multiple_incoming", preview.Report.Issues.Single().Code);
                StringAssert.Contains("node:branch_shared", preview.Report.Issues.Single().Location);
            }
        }

        [Test]
        public void Analyze_WhenBranchContainsCrossEpisodeStepTarget_ReportsConflict()
        {
            var asset = CreateChoiceAsset(false, false);
            var episode = asset.Volumes[0].Episodes[0];
            episode.Edges.RemoveAll(x => x.EdgeId == "branch_end");
            episode.Edges.Add(new AuthoringEdge
            {
                EdgeId = "branch_cross",
                FromNodeId = "branch_line",
                FromPortId = "completed",
                TargetKind = (TransitionTargetKind)LegacyNodeKinds.TargetEpisode,
                LegacyTargetEpisodeId = "episode_b"
            });

            using (var preview = Analyze(asset))
            {
                Assert.IsFalse(preview.Report.CanApply);
                Assert.AreEqual("cross_episode_step_target", preview.Report.Issues.Single().Code);
                StringAssert.Contains("edge:branch_cross", preview.Report.Issues.Single().Location);
            }
        }

        [Test]
        public void Analyze_WhenLegacyEventDefinitionIsMissing_ReportsConflict()
        {
            var asset = CreateAsset();
            var episode = AddEpisode(asset, "episode_a", Node("start", NodeKind.Start), Node("qte", (NodeKind)LegacyNodeKinds.Qte));
            episode.EntryNodeId = "start";
            episode.Edges.Add(Edge("start_qte", "start", "completed", "qte"));
            AddParameters(episode.Nodes[1],
                ("inputActionId", "submit"),
                ("durationSeconds", "2"),
                ("promptTextKey", "qte.prompt"));

            using (var preview = Analyze(asset))
            {
                Assert.IsFalse(preview.Report.CanApply);
                Assert.AreEqual("missing_event_definition", preview.Report.Issues.Single().Code);
                Assert.AreEqual(LegacyNodeKinds.Qte, (int)episode.Nodes[1].NodeKind);
            }
        }

        [Test]
        public void Analyze_WhenLegacyEventDefinitionMatches_ConvertsEventParameters()
        {
            var asset = CreateAsset();
            var episode = AddEpisode(asset, "episode_a", Node("start", NodeKind.Start), Node("qte", (NodeKind)LegacyNodeKinds.Qte));
            episode.EntryNodeId = "start";
            episode.Edges.Add(Edge("start_qte", "start", "completed", "qte"));
            AddParameters(episode.Nodes[1],
                ("inputActionId", "submit"),
                ("durationSeconds", "2"),
                ("promptTextKey", "qte.prompt"));

            using (var preview = MigrationService.Analyze(asset, new MigrationEventDefinitions(), new EmptySettlementDefinitions()))
            {
                Assert.IsTrue(preview.Report.CanApply);
                var migrated = preview.Candidate.FindEpisode("episode_a").Nodes.Single(x => x.NodeId == "qte");
                Assert.AreEqual(NodeKind.Event, migrated.NodeKind);
                Assert.AreEqual("gameplay.qte", Parameter(migrated, "eventId"));
                Assert.AreEqual("request", Parameter(migrated, "mode"));
            }
        }

        [Test]
        public void Analyze_WhenSettlementDefinitionIsMissing_ReportsConflict()
        {
            var asset = CreateAsset();
            var episode = AddEpisode(asset, "episode_a", Node("start", NodeKind.Start), Node("settle", NodeKind.SettleEpisode));
            episode.EntryNodeId = "start";
            episode.Edges.Add(Edge("start_settle", "start", "completed", "settle"));
            var plan = new SettlementPlan(
                "settlement_a",
                1,
                new[] { new SettlementOperation("operation_a", "business.reward", new ArgumentBag()) });
            AddParameters(episode.Nodes[1],
                ("settlementId", "settlement_a"),
                ("plan", SettlementPlanCodec.Serialize(plan)));

            using (var preview = Analyze(asset))
            {
                Assert.IsFalse(preview.Report.CanApply);
                Assert.AreEqual("missing_settlement_definition", preview.Report.Issues.Single().Code);
                Assert.AreEqual(NodeKind.SettleEpisode, episode.Nodes[1].NodeKind);
            }
        }

        [Test]
        public void Apply_WhenCandidateIsValid_UsesOneUndoAndSecondApplyIsNoOp()
        {
            var asset = CreateJumpAsset();

            var result = MigrationService.Apply(asset, true, new EmptyEventDefinitions(), new EmptySettlementDefinitions());

            Assert.AreEqual(MigrationApplyStatus.Applied, result.Status);
            Assert.IsNotNull(asset.Volumes[0].Route);
            Assert.AreEqual(NodeKind.End, asset.FindEpisode("episode_a").Nodes.Single(x => x.NodeId == "jump").NodeKind);
            Assert.AreEqual(
                MigrationApplyStatus.NoOp,
                MigrationService.Apply(asset, true, new EmptyEventDefinitions(), new EmptySettlementDefinitions()).Status);

            Undo.PerformUndo();
            Assert.IsNull(asset.Volumes[0].Route);
            Assert.AreEqual(LegacyNodeKinds.JumpEpisode, (int)asset.FindEpisode("episode_a").Nodes.Single(x => x.NodeId == "jump").NodeKind);
            Undo.PerformRedo();
            Assert.IsNotNull(asset.Volumes[0].Route);
        }

        [Test]
        public void Apply_WhenAnalyzeHasConflict_DoesNotChangeSource()
        {
            var asset = CreateChoiceAsset(true, false);
            var originalEpisodeCount = asset.Volumes[0].Episodes.Count;

            var result = MigrationService.Apply(asset, true, new EmptyEventDefinitions(), new EmptySettlementDefinitions());

            Assert.AreEqual(MigrationApplyStatus.Blocked, result.Status);
            Assert.AreEqual(originalEpisodeCount, asset.Volumes[0].Episodes.Count);
            Assert.IsNull(asset.Volumes[0].Route);
        }

        [Test]
        public void Apply_WhenCandidateValidationFails_DoesNotChangeSource()
        {
            var asset = CreateJumpAsset();
            asset.LegacyDetailLayout.Nodes.Add(new EpisodeNodePlacement
            {
                LegacyGraphId = "episode_a",
                NodeId = "unknown",
                Position = Vector2.zero
            });

            var result = MigrationService.Apply(asset, true, new EmptyEventDefinitions(), new EmptySettlementDefinitions());

            Assert.AreEqual(MigrationApplyStatus.Blocked, result.Status);
            Assert.IsTrue(result.Report.Issues.Any(x => x.Code == "invalid_detail_layout_node"));
            Assert.IsNull(asset.Volumes[0].Route);
            Assert.AreEqual(LegacyNodeKinds.JumpEpisode, (int)asset.FindEpisode("episode_a").Nodes.Single(x => x.NodeId == "jump").NodeKind);
        }

        [Test]
        public void Apply_WhenMigrationHasWarning_RequiresConfirmation()
        {
            var asset = CreateJumpAsset();
            var secondVolume = new AuthoringVolume { VolumeId = "volume_b", Title = "Second" };
            var episode = new AuthoringEpisode { EpisodeId = "episode_c", Title = "Third", EntryNodeId = "c_start" };
            episode.Nodes.Add(Node("c_start", NodeKind.Start));
            episode.Nodes.Add(Node("c_end", NodeKind.End));
            episode.Edges.Add(Edge("c_end", "c_start", "completed", "c_end"));
            secondVolume.Episodes.Add(episode);
            asset.Volumes.Add(secondVolume);

            var pending = MigrationService.Apply(asset, false, new EmptyEventDefinitions(), new EmptySettlementDefinitions());

            Assert.AreEqual(MigrationApplyStatus.WarningConfirmationRequired, pending.Status);
            Assert.IsTrue(pending.Report.Issues.Any(x => x.Code == "volume_root_inferred"));
            Assert.IsNull(asset.Volumes[0].Route);
            Assert.IsNull(asset.Volumes[1].Route);

            var applied = MigrationService.Apply(asset, true, new EmptyEventDefinitions(), new EmptySettlementDefinitions());
            Assert.AreEqual(MigrationApplyStatus.Applied, applied.Status);
            Assert.IsNotNull(asset.Volumes[0].Route);
            Assert.IsNotNull(asset.Volumes[1].Route);
        }

        private MigrationPreview Analyze(AuthoringAsset asset)
        {
            return MigrationService.Analyze(asset, new EmptyEventDefinitions(), new EmptySettlementDefinitions());
        }

        private AuthoringAsset CreateAsset()
        {
            var asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            m_Objects.Add(asset);
            asset.StoryId = "migration_story";
            asset.Volumes.Clear();
            asset.Volumes.Add(new AuthoringVolume { VolumeId = "volume_a", Title = "Volume" });
            return asset;
        }

        private AuthoringAsset CreateJumpAsset()
        {
            var asset = CreateAsset();
            var first = AddEpisode(asset, "episode_a", Node("start", NodeKind.Start), Node("jump", (NodeKind)LegacyNodeKinds.JumpEpisode));
            var second = AddEpisode(asset, "episode_b", Node("target_start", NodeKind.Start), Node("target_end", NodeKind.End));
            first.EntryNodeId = "start";
            second.EntryNodeId = "target_start";
            first.Edges.Add(Edge("start_jump", "start", "completed", "jump"));
            second.Edges.Add(Edge("target_end", "target_start", "completed", "target_end"));
            first.Nodes[1].Parameters.Add(new AuthoringParameter { Key = "chapterId", Value = "episode_b" });
            asset.LegacyEntryEpisodeId = "episode_a";
            return asset;
        }

        private AuthoringAsset CreateChoiceAsset(bool sharedSuffix, bool cycle)
        {
            var asset = CreateAsset();
            var episode = AddEpisode(
                asset,
                "episode_a",
                Node("start", NodeKind.Start),
                Node("line", NodeKind.Dialogue),
                Node("choice", NodeKind.Choice),
                Node("branch_line", NodeKind.Dialogue),
                Node("branch_end", NodeKind.End));
            episode.EntryNodeId = "start";
            episode.Edges.Add(Edge("start_line", "start", "completed", "line"));
            episode.Edges.Add(Edge("line_choice", "line", "completed", "choice"));
            episode.Edges.Add(Edge("choice_branch", "choice", "selected", "branch_line"));
            episode.Edges.Add(Edge("branch_end", "branch_line", "completed", "branch_end"));

            if (sharedSuffix)
            {
                episode.Nodes.Add(Node("choice_b", NodeKind.Choice));
                episode.Nodes.Add(Node("branch_b", NodeKind.Dialogue));
                episode.Nodes.Add(Node("shared", NodeKind.End));
                episode.Edges.RemoveAll(x => x.EdgeId == "branch_end");
                episode.Edges.Add(Edge("line_choice_b", "line", "completed", "choice_b"));
                episode.Edges.Add(Edge("choice_b_branch", "choice_b", "selected", "branch_b"));
                episode.Edges.Add(Edge("branch_a_shared", "branch_line", "completed", "shared"));
                episode.Edges.Add(Edge("branch_b_shared", "branch_b", "completed", "shared"));
            }

            if (cycle)
            {
                episode.Edges.RemoveAll(x => x.EdgeId == "branch_end");
                episode.Edges.Add(Edge("branch_cycle", "branch_line", "completed", "branch_line"));
            }

            asset.LegacyEntryEpisodeId = "episode_a";
            return asset;
        }

        private static AuthoringEpisode AddEpisode(AuthoringAsset asset, string episodeId, params AuthoringNode[] nodes)
        {
            var episode = new AuthoringEpisode { EpisodeId = episodeId, Title = episodeId };
            episode.Nodes.AddRange(nodes);
            asset.Volumes[0].Episodes.Add(episode);
            return episode;
        }

        private static AuthoringNode Node(string id, NodeKind kind)
        {
            return new AuthoringNode { NodeId = id, Title = id, NodeKind = kind };
        }

        private static AuthoringEdge Edge(string id, string from, string port, string target)
        {
            return new AuthoringEdge
            {
                EdgeId = id,
                FromNodeId = from,
                FromPortId = port,
                TargetKind = TransitionTargetKind.Node,
                TargetNodeId = target
            };
        }

        private static void AddParameters(AuthoringNode node, params (string key, string value)[] values)
        {
            for (var i = 0; i < values.Length; i++)
            {
                node.Parameters.Add(new AuthoringParameter { Key = values[i].key, Value = values[i].value });
            }
        }

        private static string Parameter(AuthoringNode node, string key)
        {
            return node.Parameters.Single(x => x.Key == key).Value;
        }

        private sealed class EmptyEventDefinitions : IEventDefinitionProvider
        {
            public IReadOnlyList<EventDefinition> GetDefinitions()
            {
                return Array.Empty<EventDefinition>();
            }
        }

        private sealed class MigrationEventDefinitions : IEventDefinitionProvider
        {
            public IReadOnlyList<EventDefinition> GetDefinitions()
            {
                return new[]
                {
                    new EventDefinition(
                        "gameplay.qte",
                        "QTE",
                        "Migration",
                        EventMode.Request,
                        new[]
                        {
                            new EventArgumentDefinition("inputActionId", "Input", required: true),
                            new EventArgumentDefinition("durationSeconds", "Duration", ParameterValueType.Number, true),
                            new EventArgumentDefinition("promptTextKey", "Prompt", required: true)
                        },
                        new[] { "success", "fail" })
                };
            }
        }

        private sealed class EmptySettlementDefinitions : ISettlementDefinitionProvider
        {
            public IReadOnlyList<SettlementDefinition> GetDefinitions()
            {
                return Array.Empty<SettlementDefinition>();
            }
        }
    }
}
