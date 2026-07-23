using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.StoryEditor.Logic;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;
using UnityEditor;

namespace GameDeveloperKit.StoryEditor.Compiler
{
    public static partial class ProgramCompiler
    {
        public static Program Compile(AuthoringAsset asset, out ValidationReport report)
        {
            return Compile(
                asset,
                LogicDefinitionCatalog.Shared,
                out report);
        }

        private static Program Compile(
            AuthoringAsset asset,
            LogicDefinitionCatalog logicDefinitions,
            out ValidationReport report)
        {
            report = new ValidationReport();
            if (asset == null)
            {
                report.AddError("asset", "Story authoring asset is missing.");
                return null;
            }

            var volumes = ResolveVolumes(asset, report);
            ValidateProjectIdentityConflicts(asset, null, report);
            return CompileSources(asset, volumes, logicDefinitions, true, report);
        }

        internal static Volume CompileVolume(
            AuthoringAsset project,
            AuthoringVolumeAsset volume,
            out ValidationReport report)
        {
            report = new ValidationReport();
            if (project == null)
            {
                report.AddError("asset", "Story authoring asset is missing.");
                return null;
            }

            if (volume?.Volume == null)
            {
                report.AddError("volume", "Story volume asset is missing.");
                return null;
            }

            ValidateSingleVolumeContext(project, volume, report);
            ValidateProjectIdentityConflicts(project, volume, report);
            if (report.HasErrors)
            {
                return null;
            }

            var program = CompileSources(
                project,
                new[] { volume.Volume },
                LogicDefinitionCatalog.Shared,
                false,
                report);
            return program == null || program.Volumes.Count == 0 ? null : program.Volumes[0];
        }

        private static Program CompileSources(
            AuthoringAsset asset,
            IReadOnlyList<AuthoringVolume> sourceVolumes,
            LogicDefinitionCatalog logicDefinitions,
            bool addPublishedIdentityIssues,
            ValidationReport report)
        {
            logicDefinitions ??= LogicDefinitionCatalog.Shared;
            for (var i = 0; i < logicDefinitions.Errors.Count; i++)
            {
                report.AddError("logic-definitions", logicDefinitions.Errors[i]);
            }

            ValidateText(asset.StoryId, "story", report);
            ValidateText(asset.Version, "version", report);
            var episodeLookup = BuildEpisodeLookup(asset.StoryId, sourceVolumes, report);

            if (report.HasErrors)
            {
                return null;
            }

            var commandDefinitions = new List<CommandDefinition>();
            var commandNames = new HashSet<string>(StringComparer.Ordinal);
            var routeEdgeIds = new HashSet<string>(StringComparer.Ordinal);
            var volumes = new List<Volume>();
            for (var volumeIndex = 0; volumeIndex < sourceVolumes.Count; volumeIndex++)
            {
                var sourceVolume = sourceVolumes[volumeIndex];
                if (sourceVolume == null)
                {
                    continue;
                }

                var episodes = new List<Episode>();
                for (var episodeIndex = 0; episodeIndex < sourceVolume.Episodes.Count; episodeIndex++)
                {
                    var episode = sourceVolume.Episodes[episodeIndex];
                    if (episode == null)
                    {
                        continue;
                    }

                    var compiled = CompileEpisode(
                        asset.StoryId,
                        episode,
                        episodeLookup,
                        logicDefinitions,
                        commandDefinitions,
                        commandNames,
                        report);
                    if (compiled != null)
                    {
                        episodes.Add(compiled);
                    }
                }

                var route = RouteCompiler.Compile(asset.StoryId, sourceVolume, episodes, routeEdgeIds, report);
                var layouts = LayoutCompiler.Compile(asset.StoryId, sourceVolume, episodes, route, report);
                volumes.Add(new Volume(
                    TrimToNull(sourceVolume.VolumeId),
                    TrimToNull(sourceVolume.Title),
                    episodes,
                    route,
                    GetPreviewImagePath(sourceVolume),
                    TrimToNull(sourceVolume.Description),
                    layouts));
            }

            if (report.HasErrors)
            {
                return null;
            }

            var program = new Program(
                TrimToNull(asset.StoryId),
                TrimToNull(asset.Version),
                volumes,
                new VariableSchema(),
                new CommandSchema(commandDefinitions));
            if (addPublishedIdentityIssues)
            {
                AddPublishedIdentityIssues(asset, program, report);
            }

            return report.HasErrors ? null : program;
        }

        public static ValidationReport Validate(AuthoringAsset asset)
        {
            Compile(asset, out var report);
            return report;
        }

        private static IReadOnlyList<AuthoringVolume> ResolveVolumes(
            AuthoringAsset asset,
            ValidationReport report)
        {
            var result = new List<AuthoringVolume>();
            if (asset.VolumeAssets.Count == 0)
            {
                for (var i = 0; i < asset.EmbeddedVolumes.Count; i++)
                {
                    result.Add(asset.EmbeddedVolumes[i]);
                }

                return result;
            }

            var references = new HashSet<AuthoringVolumeAsset>();
            var volumeIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < asset.VolumeAssets.Count; i++)
            {
                var volumeAsset = asset.VolumeAssets[i];
                if (volumeAsset == null)
                {
                    report.AddError($"story:{asset.StoryId}/volume[{i}]", "Volume asset reference cannot be null.");
                    continue;
                }

                if (references.Add(volumeAsset) is false)
                {
                    report.AddError($"story:{asset.StoryId}/volume[{i}]", "Volume asset reference cannot be duplicated.");
                    continue;
                }

                if (EditorUtility.IsPersistent(asset) && EditorUtility.IsPersistent(volumeAsset))
                {
                    if (AuthoringProjectResolver.TryResolveOwner(volumeAsset, out var owner, out var ownerError) is false ||
                        owner != asset)
                    {
                        report.AddError(
                            $"story:{asset.StoryId}/volume[{i}]",
                            ownerError ?? $"Volume asset belongs to another story project: {AssetPath(volumeAsset)}");
                        continue;
                    }
                }

                var volume = volumeAsset.Volume;
                if (string.IsNullOrWhiteSpace(volume.VolumeId))
                {
                    report.AddError(
                        $"story:{asset.StoryId}/volume[{i}]",
                        $"Volume id cannot be empty. asset:{AssetPath(volumeAsset)}");
                    continue;
                }

                if (volumeIds.Add(volume.VolumeId) is false)
                {
                    continue;
                }

                result.Add(volume);
            }

            return result;
        }

        private static void ValidateSingleVolumeContext(
            AuthoringAsset project,
            AuthoringVolumeAsset volume,
            ValidationReport report)
        {
            var referenceCount = 0;
            for (var i = 0; i < project.VolumeAssets.Count; i++)
            {
                if (project.VolumeAssets[i] == volume)
                {
                    referenceCount++;
                }
            }

            if (referenceCount != 1)
            {
                report.AddError(
                    $"story:{project.StoryId}/volume:{volume.Volume.VolumeId}",
                    $"Volume asset must appear exactly once in its story project. asset:{AssetPath(volume)}");
                return;
            }

            if (EditorUtility.IsPersistent(project) && EditorUtility.IsPersistent(volume) &&
                (AuthoringProjectResolver.TryResolveOwner(volume, out var owner, out var ownerError) is false ||
                 owner != project))
            {
                report.AddError(
                    $"story:{project.StoryId}/volume:{volume.Volume.VolumeId}",
                    ownerError ?? $"Volume asset belongs to another story project: {AssetPath(volume)}");
            }
        }

        private static void ValidateProjectIdentityConflicts(
            AuthoringAsset project,
            AuthoringVolumeAsset focus,
            ValidationReport report)
        {
            if (project.VolumeAssets.Count == 0)
            {
                return;
            }

            var volumes = new Dictionary<string, AuthoringVolumeAsset>(StringComparer.Ordinal);
            var episodes = new Dictionary<string, AuthoringVolumeAsset>(StringComparer.Ordinal);
            var routeEdges = new Dictionary<string, AuthoringVolumeAsset>(StringComparer.Ordinal);
            for (var volumeIndex = 0; volumeIndex < project.VolumeAssets.Count; volumeIndex++)
            {
                var volumeAsset = project.VolumeAssets[volumeIndex];
                if (volumeAsset == null)
                {
                    continue;
                }

                var volume = volumeAsset.Volume;
                AddIdentity(volumes, "volume", volume.VolumeId, volumeAsset, focus, project, report);
                for (var episodeIndex = 0; episodeIndex < volume.Episodes.Count; episodeIndex++)
                {
                    var episode = volume.Episodes[episodeIndex];
                    if (episode != null)
                    {
                        AddIdentity(episodes, "episode", episode.EpisodeId, volumeAsset, focus, project, report);
                    }
                }

                for (var edgeIndex = 0; edgeIndex < (volume.Route?.Edges.Count ?? 0); edgeIndex++)
                {
                    var edge = volume.Route.Edges[edgeIndex];
                    if (edge != null)
                    {
                        AddIdentity(routeEdges, "route edge", edge.EdgeId, volumeAsset, focus, project, report);
                    }
                }
            }
        }

        private static void AddIdentity(
            IDictionary<string, AuthoringVolumeAsset> identities,
            string kind,
            string id,
            AuthoringVolumeAsset volume,
            AuthoringVolumeAsset focus,
            AuthoringAsset project,
            ValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            if (identities.TryGetValue(id, out var first) is false)
            {
                identities.Add(id, volume);
                return;
            }

            if (focus != null && first != focus && volume != focus)
            {
                return;
            }

            report.AddError(
                $"story:{project.StoryId}/{kind}:{id}",
                $"Duplicate {kind} id '{id}'. assets:{AssetPath(first)}, {AssetPath(volume)}");
        }

        private static string AssetPath(AuthoringVolumeAsset volume)
        {
            var path = AssetDatabase.GetAssetPath(volume);
            return string.IsNullOrWhiteSpace(path) ? "<unsaved volume>" : path;
        }

        private static IReadOnlyDictionary<string, AuthoringEpisode> BuildEpisodeLookup(
            string storyId,
            IReadOnlyList<AuthoringVolume> volumes,
            ValidationReport report)
        {
            var episodes = new Dictionary<string, AuthoringEpisode>(StringComparer.Ordinal);
            var index = 0;
            for (var volumeIndex = 0; volumeIndex < volumes.Count; volumeIndex++)
            {
                var volume = volumes[volumeIndex];
                for (var episodeIndex = 0; episodeIndex < (volume?.Episodes.Count ?? 0); episodeIndex++)
                {
                    var episode = volume.Episodes[episodeIndex];
                    if (episode == null)
                    {
                        report.AddError($"story:{storyId}/episode[{index}]", "Episode cannot be null.");
                        index++;
                        continue;
                    }

                    var episodeId = TrimToNull(episode.EpisodeId);
                    if (string.IsNullOrWhiteSpace(episodeId))
                    {
                        report.AddError($"story:{storyId}/episode[{index}]", "Episode id cannot be empty.");
                        index++;
                        continue;
                    }

                    if (episodes.ContainsKey(episodeId))
                    {
                        report.AddError($"story:{storyId}/episode:{episodeId}", "Duplicate episode id.");
                        index++;
                        continue;
                    }

                    episodes.Add(episodeId, episode);
                    index++;
                }
            }

            return episodes;
        }
    }
}
