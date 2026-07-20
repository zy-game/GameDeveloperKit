using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Story;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Event;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.Story.Settlement;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Event;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Settlement;
using GameDeveloperKit.StoryEditor.Validation;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Migration
{
    internal static class MigrationService
    {
        private const string UndoName = "Migrate Story Route Content";

        public static MigrationPreview Analyze(AuthoringAsset source)
        {
            return Analyze(source, EventDefinitionCatalog.Shared, SettlementDefinitionCatalog.Shared);
        }

        internal static MigrationPreview Analyze(
            AuthoringAsset source,
            IEventDefinitionProvider eventDefinitions,
            ISettlementDefinitionProvider settlementDefinitions)
        {
            var eventCatalog = EventDefinitionCatalog.Create(new[] { eventDefinitions });
            var settlementCatalog = SettlementDefinitionCatalog.Create(new[] { settlementDefinitions });
            return Analyze(source, eventCatalog, settlementCatalog);
        }

        public static MigrationResult Apply(AuthoringAsset source, bool confirmWarnings)
        {
            return Apply(source, confirmWarnings, null, null);
        }

        internal static MigrationResult Apply(
            AuthoringAsset source,
            bool confirmWarnings,
            IEventDefinitionProvider eventDefinitions,
            ISettlementDefinitionProvider settlementDefinitions)
        {
            using (var preview = eventDefinitions == null && settlementDefinitions == null
                       ? Analyze(source)
                       : Analyze(source, eventDefinitions, settlementDefinitions))
            {
                if (preview.IsNoOp)
                {
                    return new MigrationResult(MigrationApplyStatus.NoOp, preview.Report);
                }

                if (!preview.Report.CanApply)
                {
                    return new MigrationResult(MigrationApplyStatus.Blocked, preview.Report);
                }

                ValidateCandidate(preview.Candidate, eventDefinitions, preview.Report);
                preview.Report.Sort();
                if (!preview.Report.CanApply)
                {
                    return new MigrationResult(MigrationApplyStatus.Blocked, preview.Report);
                }

                if (preview.Report.HasWarnings && !confirmWarnings)
                {
                    return new MigrationResult(MigrationApplyStatus.WarningConfirmationRequired, preview.Report);
                }

                AuthoringUndo.Mutate(source, UndoName, () => EditorUtility.CopySerialized(preview.Candidate, source));
                if (EditorUtility.IsPersistent(source))
                {
                    AssetDatabase.SaveAssetIfDirty(source);
                }

                return new MigrationResult(MigrationApplyStatus.Applied, preview.Report);
            }
        }

        private static MigrationPreview Analyze(
            AuthoringAsset source,
            EventDefinitionCatalog eventCatalog,
            SettlementDefinitionCatalog settlementCatalog)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var report = new MigrationReport();
            if (!HasLegacyMarkers(source))
            {
                return new MigrationPreview(null, report, true);
            }

            var candidate = UnityEngine.Object.Instantiate(source);
            candidate.name = source.name;
            candidate.hideFlags = HideFlags.HideAndDontSave;
            candidate.EnsureDefaults();
            AddCatalogIssues(eventCatalog.Errors, "event_definition_catalog", report);
            AddCatalogIssues(settlementCatalog.Errors, "settlement_definition_catalog", report);
            MigrateDetailLayout(candidate, report);

            var pendingEdges = new List<AuthoringRouteEdge>();
            for (var volumeIndex = 0; volumeIndex < candidate.Volumes.Count && report.CanApply; volumeIndex++)
            {
                var volume = candidate.Volumes[volumeIndex];
                if (volume == null)
                {
                    continue;
                }

                if (volume.Route != null)
                {
                    report.AddConflict(
                        "mixed_route_protocol",
                        $"story:{candidate.StoryId}/volume:{volume.VolumeId}/route",
                        "Legacy nodes cannot be migrated together with an explicit current Route.");
                    break;
                }

                var originalEpisodes = volume.Episodes.ToArray();
                for (var episodeIndex = 0; episodeIndex < originalEpisodes.Length && report.CanApply; episodeIndex++)
                {
                    var episode = originalEpisodes[episodeIndex];
                    if (episode != null)
                    {
                        BranchAnalyzer.Extract(candidate.StoryId, volume, episode, pendingEdges, report);
                    }
                }
            }

            if (report.CanApply)
            {
                ConvertNodes(candidate, eventCatalog, settlementCatalog, pendingEdges, report);
            }

            if (report.CanApply)
            {
                BuildRoutes(candidate, pendingEdges, report);
            }

            report.Sort();
            return new MigrationPreview(candidate, report, false);
        }

        private static bool HasLegacyMarkers(AuthoringAsset source)
        {
            if (source.LegacyDetailLayout.Nodes.Count != 0)
            {
                return true;
            }

            for (var volumeIndex = 0; volumeIndex < source.Volumes.Count; volumeIndex++)
            {
                var volume = source.Volumes[volumeIndex];
                if (volume == null || volume.Route == null)
                {
                    return true;
                }

                for (var episodeIndex = 0; episodeIndex < volume.Episodes.Count; episodeIndex++)
                {
                    var episode = volume.Episodes[episodeIndex];
                    if (episode == null)
                    {
                        continue;
                    }

                    for (var nodeIndex = 0; nodeIndex < episode.Nodes.Count; nodeIndex++)
                    {
                        var kind = episode.Nodes[nodeIndex]?.NodeKind ?? default;
                        if ((int)kind == LegacyNodeKinds.JumpEpisode ||
                            LegacyNodeKinds.IsSpecializedEvent(kind))
                        {
                            return true;
                        }
                    }

                    for (var edgeIndex = 0; edgeIndex < episode.Edges.Count; edgeIndex++)
                    {
                        var edge = episode.Edges[edgeIndex];
                        if (edge != null &&
                            ((int)edge.TargetKind == LegacyNodeKinds.TargetEpisode ||
                             string.Equals(edge.FromPortId, "selected", StringComparison.Ordinal)))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static void AddCatalogIssues(
            IReadOnlyList<string> errors,
            string code,
            MigrationReport report)
        {
            for (var i = 0; i < (errors?.Count ?? 0); i++)
            {
                report.AddConflict(code, "definitions", errors[i]);
            }
        }

        private static void MigrateDetailLayout(AuthoringAsset candidate, MigrationReport report)
        {
            var legacy = candidate.LegacyDetailLayout.Nodes;
            for (var i = 0; i < legacy.Count; i++)
            {
                var placement = legacy[i];
                var graphId = placement?.LegacyGraphId;
                var location = $"story:{candidate.StoryId}/layout/node[{i}]";
                var episode = candidate.FindEpisode(graphId);
                if (placement == null || episode == null)
                {
                    report.AddConflict(
                        "unknown_detail_layout_episode",
                        location,
                        $"Legacy detail layout references an unknown Episode. episode:{graphId}");
                    continue;
                }

                placement.LegacyGraphId = null;
                episode.DetailLayout.Nodes.Add(placement);
                report.AddChange(
                    MigrationChangeKind.Converted,
                    $"story:{candidate.StoryId}/episode:{episode.EpisodeId}/node:{placement.NodeId}/layout",
                    "global GraphLayout -> Episode detail layout");
            }

            if (report.CanApply)
            {
                legacy.Clear();
            }
        }

        private static void ConvertNodes(
            AuthoringAsset candidate,
            EventDefinitionCatalog eventCatalog,
            SettlementDefinitionCatalog settlementCatalog,
            IList<AuthoringRouteEdge> pendingEdges,
            MigrationReport report)
        {
            for (var volumeIndex = 0; volumeIndex < candidate.Volumes.Count; volumeIndex++)
            {
                var volume = candidate.Volumes[volumeIndex];
                if (volume == null)
                {
                    continue;
                }

                for (var episodeIndex = 0; episodeIndex < volume.Episodes.Count; episodeIndex++)
                {
                    var episode = volume.Episodes[episodeIndex];
                    if (episode == null)
                    {
                        continue;
                    }

                    for (var nodeIndex = 0; nodeIndex < episode.Nodes.Count; nodeIndex++)
                    {
                        var node = episode.Nodes[nodeIndex];
                        if (node == null)
                        {
                            continue;
                        }

                        if ((int)node.NodeKind == LegacyNodeKinds.JumpEpisode)
                        {
                            ConvertJump(candidate, volume, episode, node, pendingEdges, report);
                        }
                        else if (LegacyNodeKinds.IsSpecializedEvent(node.NodeKind))
                        {
                            LegacyEventCodec.Convert(candidate.StoryId, volume.VolumeId, episode, node, eventCatalog, report);
                        }
                        else if ((int)node.NodeKind == LegacyNodeKinds.SettleEpisode)
                        {
                            LegacySettlementCodec.Validate(candidate.StoryId, volume.VolumeId, episode, node, settlementCatalog, report);
                        }
                    }
                }
            }
        }

        private static void ConvertJump(
            AuthoringAsset candidate,
            AuthoringVolume volume,
            AuthoringEpisode episode,
            AuthoringNode node,
            IList<AuthoringRouteEdge> pendingEdges,
            MigrationReport report)
        {
            var location = $"story:{candidate.StoryId}/volume:{volume.VolumeId}/episode:{episode.EpisodeId}/node:{node.NodeId}";
            var targetEpisodeId = Parameter(node, "chapterId") ?? Parameter(node, "episodeId");
            var outgoing = episode.Edges
                .Where(x => x != null && string.Equals(x.FromNodeId, node.NodeId, StringComparison.Ordinal))
                .ToArray();
            for (var i = 0; i < outgoing.Length; i++)
            {
                if ((int)outgoing[i].TargetKind == LegacyNodeKinds.TargetEpisode && string.IsNullOrWhiteSpace(targetEpisodeId))
                {
                    targetEpisodeId = outgoing[i].LegacyTargetEpisodeId;
                }
            }

            if (string.IsNullOrWhiteSpace(targetEpisodeId) ||
                !volume.Episodes.Any(x => x != null && string.Equals(x.EpisodeId, targetEpisodeId, StringComparison.Ordinal)))
            {
                report.AddConflict(
                    "unknown_jump_target",
                    location,
                    $"Legacy Jump target must exist in the same Volume. episode:{targetEpisodeId}");
                return;
            }

            node.NodeKind = NodeKind.End;
            node.Parameters.Clear();
            episode.Edges.RemoveAll(x => x != null && string.Equals(x.FromNodeId, node.NodeId, StringComparison.Ordinal));
            pendingEdges.Add(new AuthoringRouteEdge
            {
                SourceKind = RouteEdgeSourceKind.EpisodeExit,
                FromEpisodeId = episode.EpisodeId,
                FromExitId = node.NodeId,
                ToEpisodeId = targetEpisodeId
            });
            report.AddChange(MigrationChangeKind.Converted, location, $"Jump node -> End exit + RouteEdge to Episode:{targetEpisodeId}");
        }

        private static string Parameter(AuthoringNode node, string key)
        {
            for (var i = 0; i < node.Parameters.Count; i++)
            {
                var parameter = node.Parameters[i];
                if (parameter != null && string.Equals(parameter.Key, key, StringComparison.Ordinal))
                {
                    return string.IsNullOrWhiteSpace(parameter.Value) ? null : parameter.Value;
                }
            }

            return null;
        }

        private static void BuildRoutes(
            AuthoringAsset candidate,
            IReadOnlyList<AuthoringRouteEdge> pendingEdges,
            MigrationReport report)
        {
            for (var volumeIndex = 0; volumeIndex < candidate.Volumes.Count; volumeIndex++)
            {
                var volume = candidate.Volumes[volumeIndex];
                if (volume == null || volume.Episodes.Count == 0)
                {
                    continue;
                }

                var explicitRoot = volume.Episodes.FirstOrDefault(x =>
                    x != null && string.Equals(x.EpisodeId, candidate.LegacyEntryEpisodeId, StringComparison.Ordinal));
                var rootEpisode = explicitRoot ?? volume.Episodes.FirstOrDefault(x => x != null);
                if (rootEpisode == null)
                {
                    report.AddConflict(
                        "missing_volume_root",
                        $"story:{candidate.StoryId}/volume:{volume.VolumeId}/route",
                        "Volume has no Episode that can become the route root.");
                    continue;
                }

                if (explicitRoot == null)
                {
                    report.AddWarning(
                        "volume_root_inferred",
                        $"story:{candidate.StoryId}/volume:{volume.VolumeId}/route",
                        $"Legacy content has one global entry; this Volume root was inferred from its first Episode. episode:{rootEpisode.EpisodeId}");
                }

                volume.Route = new AuthoringRoute();
                volume.Route.Edges.Add(new AuthoringRouteEdge
                {
                    EdgeId = IdentityId.RootEdge(rootEpisode.EpisodeId),
                    SourceKind = RouteEdgeSourceKind.Root,
                    ToEpisodeId = rootEpisode.EpisodeId
                });
                report.AddChange(
                    MigrationChangeKind.Added,
                    $"story:{candidate.StoryId}/volume:{volume.VolumeId}/route/edge:{IdentityId.RootEdge(rootEpisode.EpisodeId)}",
                    $"Entry Episode -> Root RouteEdge to Episode:{rootEpisode.EpisodeId}");

                foreach (var pending in pendingEdges.Where(x =>
                             volume.Episodes.Any(y => y != null && string.Equals(y.EpisodeId, x.FromEpisodeId, StringComparison.Ordinal))))
                {
                    volume.Route.Edges.Add(new AuthoringRouteEdge
                    {
                        EdgeId = IdentityId.ExitEdge(pending.FromEpisodeId, pending.FromExitId),
                        SourceKind = RouteEdgeSourceKind.EpisodeExit,
                        FromEpisodeId = pending.FromEpisodeId,
                        FromExitId = pending.FromExitId,
                        ToEpisodeId = pending.ToEpisodeId
                    });
                }
            }
        }

        private static void ValidateCandidate(
            AuthoringAsset candidate,
            IEventDefinitionProvider eventDefinitions,
            MigrationReport report)
        {
            var program = eventDefinitions == null
                ? ProgramCompiler.Compile(candidate, out var validation)
                : ProgramCompiler.Compile(candidate, eventDefinitions, out validation);
            AddValidationIssues(validation, report);
            ValidateDetailLayouts(candidate, report);
            if (!report.CanApply || program == null)
            {
                return;
            }

            try
            {
                var module = new StoryModule();
                module.Register(program);
                IdentityManifest.Create(program);
            }
            catch (Exception exception)
            {
                report.AddConflict(
                    "candidate_runtime_invalid",
                    $"story:{candidate.StoryId}",
                    $"Migrated candidate cannot be registered by StoryModule. reason:{exception.Message}");
            }
        }

        private static void AddValidationIssues(ValidationReport validation, MigrationReport report)
        {
            for (var i = 0; i < (validation?.Issues.Count ?? 0); i++)
            {
                var issue = validation.Issues[i];
                if (issue.Severity == ValidationSeverity.Error)
                {
                    report.AddConflict("candidate_compile_error", issue.Source, issue.Message);
                }
                else if (issue.Severity == ValidationSeverity.Warning)
                {
                    report.AddWarning("candidate_compile_warning", issue.Source, issue.Message);
                }
            }
        }

        private static void ValidateDetailLayouts(AuthoringAsset candidate, MigrationReport report)
        {
            for (var volumeIndex = 0; volumeIndex < candidate.Volumes.Count; volumeIndex++)
            {
                var volume = candidate.Volumes[volumeIndex];
                for (var episodeIndex = 0; episodeIndex < (volume?.Episodes.Count ?? 0); episodeIndex++)
                {
                    var episode = volume.Episodes[episodeIndex];
                    if (episode == null)
                    {
                        continue;
                    }

                    var nodeIds = new HashSet<string>(episode.Nodes.Where(x => x != null).Select(x => x.NodeId), StringComparer.Ordinal);
                    var placed = new HashSet<string>(StringComparer.Ordinal);
                    for (var placementIndex = 0; placementIndex < episode.DetailLayout.Nodes.Count; placementIndex++)
                    {
                        var placement = episode.DetailLayout.Nodes[placementIndex];
                        var location = $"story:{candidate.StoryId}/volume:{volume.VolumeId}/episode:{episode.EpisodeId}/layout/node[{placementIndex}]";
                        if (placement == null || !nodeIds.Contains(placement.NodeId))
                        {
                            report.AddConflict("invalid_detail_layout_node", location, $"Detail layout references an unknown node. node:{placement?.NodeId}");
                            continue;
                        }

                        if (!placed.Add(placement.NodeId))
                        {
                            report.AddConflict("duplicate_detail_layout_node", location, $"Detail layout node placement must be unique. node:{placement.NodeId}");
                        }

                        if (!IsFinite(placement.Position.x) || !IsFinite(placement.Position.y))
                        {
                            report.AddConflict("invalid_detail_layout_position", location, "Detail layout position must be finite.");
                        }
                    }
                }
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
