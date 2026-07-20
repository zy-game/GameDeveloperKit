using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Event;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情运行时模块的 Program 校验逻辑。
    /// </summary>
    public sealed partial class StoryModule
    {
        private static void ValidateProgram(Program program)
        {
            ValidateId(program.StoryId, "story", program.StoryId);
            ValidateId(program.Version, "version", program.StoryId);
            if (program.Volumes.Count == 0)
            {
                throw new GameException($"Story program must contain at least one volume. story:{program.StoryId}");
            }

            var volumeIds = new HashSet<string>(StringComparer.Ordinal);
            var episodeIds = new HashSet<string>(StringComparer.Ordinal);
            var edgeIds = new HashSet<string>(StringComparer.Ordinal);
            for (var volumeIndex = 0; volumeIndex < program.Volumes.Count; volumeIndex++)
            {
                var volume = program.Volumes[volumeIndex];
                if (volume == null)
                {
                    throw new GameException($"Story volume cannot be null. story:{program.StoryId} index:{volumeIndex}");
                }

                ValidateId(volume.VolumeId, "volume", program.StoryId);
                if (!volumeIds.Add(volume.VolumeId))
                {
                    throw new GameException($"Duplicate story volume id. story:{program.StoryId} volume:{volume.VolumeId}");
                }

                ValidateVolume(program, volume, episodeIds, edgeIds);
            }
        }

        private static void ValidateVolume(
            Program program,
            Volume volume,
            ISet<string> programEpisodeIds,
            ISet<string> programEdgeIds)
        {
            if (volume.Episodes.Count == 0)
            {
                throw new GameException($"Story volume must contain at least one episode. story:{program.StoryId} volume:{volume.VolumeId}");
            }

            var episodes = new Dictionary<string, Episode>(StringComparer.Ordinal);
            var stepMaps = new Dictionary<string, Dictionary<string, Step>>(StringComparer.Ordinal);
            var exitMaps = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            for (var i = 0; i < volume.Episodes.Count; i++)
            {
                var episode = volume.Episodes[i];
                if (episode == null)
                {
                    throw new GameException($"Story episode cannot be null. story:{program.StoryId} volume:{volume.VolumeId} index:{i}");
                }

                ValidateId(episode.EpisodeId, "episode", program.StoryId);
                if (!programEpisodeIds.Add(episode.EpisodeId))
                {
                    throw new GameException($"Duplicate story episode id. story:{program.StoryId} volume:{volume.VolumeId} episode:{episode.EpisodeId}");
                }

                episodes.Add(episode.EpisodeId, episode);
                stepMaps.Add(episode.EpisodeId, BuildStepMap(program.StoryId, volume.VolumeId, episode));
                exitMaps.Add(episode.EpisodeId, BuildExitMap(program.StoryId, volume.VolumeId, episode));
            }

            ValidateRoute(program.StoryId, volume, episodes, exitMaps, programEdgeIds);
            ValidateLayouts(program.StoryId, volume, episodes);
            for (var i = 0; i < volume.Episodes.Count; i++)
            {
                ValidateEpisode(program, volume, volume.Episodes[i], stepMaps[volume.Episodes[i].EpisodeId], exitMaps[volume.Episodes[i].EpisodeId]);
            }
        }

        private static void ValidateLayouts(
            string storyId,
            Volume volume,
            IReadOnlyDictionary<string, Episode> episodes)
        {
            var routeEdgeIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < volume.Route.Edges.Count; i++)
            {
                routeEdgeIds.Add(volume.Route.Edges[i].EdgeId);
            }

            var layoutIds = new HashSet<string>(StringComparer.Ordinal);
            for (var layoutIndex = 0; layoutIndex < volume.Layouts.Count; layoutIndex++)
            {
                var layout = volume.Layouts[layoutIndex];
                if (layout == null)
                {
                    throw new GameException($"Story route layout cannot be null. story:{storyId} volume:{volume.VolumeId} index:{layoutIndex}");
                }

                ValidateId(layout.LayoutId, "routeLayout", storyId);
                if (!layoutIds.Add(layout.LayoutId))
                {
                    throw new GameException($"Duplicate story route layout id. story:{storyId} volume:{volume.VolumeId} layout:{layout.LayoutId}");
                }

                if (!Enum.IsDefined(typeof(LayoutOrientation), layout.Orientation))
                {
                    throw new GameException($"Story route layout orientation is invalid. story:{storyId} volume:{volume.VolumeId} layout:{layout.LayoutId} orientation:{layout.Orientation}");
                }

                ValidatePlacement(storyId, volume.VolumeId, layout, "root", layout.RootPlacement);
                ValidateEpisodePlacements(storyId, volume, layout, episodes);
                ValidateEdgePlacements(storyId, volume, layout, routeEdgeIds);
            }
        }

        private static void ValidateEpisodePlacements(
            string storyId,
            Volume volume,
            RouteLayout layout,
            IReadOnlyDictionary<string, Episode> episodes)
        {
            var placed = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < layout.Episodes.Count; i++)
            {
                var placement = layout.Episodes[i];
                if (string.IsNullOrWhiteSpace(placement.EpisodeId) || !episodes.ContainsKey(placement.EpisodeId))
                {
                    throw new GameException($"Story route layout episode placement references an unknown episode. story:{storyId} volume:{volume.VolumeId} layout:{layout.LayoutId} episode:{placement.EpisodeId}");
                }

                if (!placed.Add(placement.EpisodeId))
                {
                    throw new GameException($"Story route layout episode placement must be unique. story:{storyId} volume:{volume.VolumeId} layout:{layout.LayoutId} episode:{placement.EpisodeId}");
                }

                ValidatePlacement(storyId, volume.VolumeId, layout, $"episode:{placement.EpisodeId}", placement.Position);
            }

            foreach (var episodeId in episodes.Keys)
            {
                if (!placed.Contains(episodeId))
                {
                    throw new GameException($"Story route layout must place every episode. story:{storyId} volume:{volume.VolumeId} layout:{layout.LayoutId} episode:{episodeId}");
                }
            }
        }

        private static void ValidateEdgePlacements(
            string storyId,
            Volume volume,
            RouteLayout layout,
            ISet<string> routeEdgeIds)
        {
            var placed = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < layout.Edges.Count; i++)
            {
                var placement = layout.Edges[i];
                if (placement == null || string.IsNullOrWhiteSpace(placement.EdgeId) || !routeEdgeIds.Contains(placement.EdgeId))
                {
                    throw new GameException($"Story route layout edge placement references an unknown edge. story:{storyId} volume:{volume.VolumeId} layout:{layout.LayoutId} edge:{placement?.EdgeId}");
                }

                if (!placed.Add(placement.EdgeId))
                {
                    throw new GameException($"Story route layout edge placement must be unique. story:{storyId} volume:{volume.VolumeId} layout:{layout.LayoutId} edge:{placement.EdgeId}");
                }

                for (var pointIndex = 0; pointIndex < placement.ControlPoints.Count; pointIndex++)
                {
                    ValidatePlacement(
                        storyId,
                        volume.VolumeId,
                        layout,
                        $"edge:{placement.EdgeId}/point:{pointIndex}",
                        placement.ControlPoints[pointIndex]);
                }
            }

            foreach (var edgeId in routeEdgeIds)
            {
                if (!placed.Contains(edgeId))
                {
                    throw new GameException($"Story route layout must place every route edge. story:{storyId} volume:{volume.VolumeId} layout:{layout.LayoutId} edge:{edgeId}");
                }
            }
        }

        private static void ValidatePlacement(
            string storyId,
            string volumeId,
            RouteLayout layout,
            string element,
            Placement placement)
        {
            if (float.IsNaN(placement.X) || float.IsInfinity(placement.X) ||
                float.IsNaN(placement.Y) || float.IsInfinity(placement.Y) ||
                placement.X < 0f || placement.X > 1f ||
                placement.Y < 0f || placement.Y > 1f)
            {
                throw new GameException($"Story route layout placement must be finite and normalized to [0,1]. story:{storyId} volume:{volumeId} layout:{layout.LayoutId} element:{element} position:({placement.X},{placement.Y})");
            }
        }

        private static Dictionary<string, Step> BuildStepMap(string storyId, string volumeId, Episode episode)
        {
            var steps = new Dictionary<string, Step>(StringComparer.Ordinal);
            for (var i = 0; i < episode.Steps.Count; i++)
            {
                var step = episode.Steps[i];
                if (step == null)
                {
                    throw new GameException($"Story step cannot be null. story:{storyId} volume:{volumeId} episode:{episode.EpisodeId} index:{i}");
                }

                ValidateId(step.StepId, "step", storyId);
                if (!steps.TryAdd(step.StepId, step))
                {
                    throw new GameException($"Duplicate story step id. story:{storyId} volume:{volumeId} episode:{episode.EpisodeId} step:{step.StepId}");
                }
            }

            return steps;
        }

        private static HashSet<string> BuildExitMap(string storyId, string volumeId, Episode episode)
        {
            var exits = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < episode.Exits.Count; i++)
            {
                var exit = episode.Exits[i];
                ValidateId(exit.ExitId, "episodeExit", storyId);
                if (!exits.Add(exit.ExitId))
                {
                    throw new GameException($"Duplicate story episode exit id. story:{storyId} volume:{volumeId} episode:{episode.EpisodeId} exit:{exit.ExitId}");
                }
            }

            return exits;
        }

        private static void ValidateRoute(
            string storyId,
            Volume volume,
            IReadOnlyDictionary<string, Episode> episodes,
            IReadOnlyDictionary<string, HashSet<string>> exitMaps,
            ISet<string> programEdgeIds)
        {
            if (volume.Route == null)
            {
                throw new GameException($"Story volume route cannot be null. story:{storyId} volume:{volume.VolumeId}");
            }

            var incoming = new Dictionary<string, int>(StringComparer.Ordinal);
            var outgoing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var boundExits = new HashSet<string>(StringComparer.Ordinal);
            var roots = new List<string>();
            foreach (var episodeId in episodes.Keys)
            {
                incoming.Add(episodeId, 0);
                outgoing.Add(episodeId, new List<string>());
            }

            for (var i = 0; i < volume.Route.Edges.Count; i++)
            {
                var edge = volume.Route.Edges[i];
                ValidateId(edge.EdgeId, "routeEdge", storyId);
                if (!programEdgeIds.Add(edge.EdgeId))
                {
                    throw new GameException($"Duplicate story route edge id. story:{storyId} volume:{volume.VolumeId} edge:{edge.EdgeId}");
                }

                if (string.IsNullOrWhiteSpace(edge.ToEpisodeId) || !episodes.ContainsKey(edge.ToEpisodeId))
                {
                    throw new GameException($"Story route target episode does not exist. story:{storyId} volume:{volume.VolumeId} edge:{edge.EdgeId} episode:{edge.ToEpisodeId}");
                }

                incoming[edge.ToEpisodeId]++;
                if (incoming[edge.ToEpisodeId] > 1)
                {
                    throw new GameException($"Story episode cannot have multiple incoming route edges. story:{storyId} volume:{volume.VolumeId} episode:{edge.ToEpisodeId}");
                }

                switch (edge.SourceKind)
                {
                    case RouteEdgeSourceKind.Root:
                        if (!string.IsNullOrWhiteSpace(edge.FromEpisodeId) || !string.IsNullOrWhiteSpace(edge.FromExitId))
                        {
                            throw new GameException($"Story root route edge cannot declare an episode exit. story:{storyId} volume:{volume.VolumeId} edge:{edge.EdgeId}");
                        }

                        roots.Add(edge.ToEpisodeId);
                        break;
                    case RouteEdgeSourceKind.EpisodeExit:
                        if (string.IsNullOrWhiteSpace(edge.FromEpisodeId) || !episodes.ContainsKey(edge.FromEpisodeId))
                        {
                            throw new GameException($"Story route source episode does not exist. story:{storyId} volume:{volume.VolumeId} edge:{edge.EdgeId} episode:{edge.FromEpisodeId}");
                        }

                        if (string.IsNullOrWhiteSpace(edge.FromExitId) || !exitMaps[edge.FromEpisodeId].Contains(edge.FromExitId))
                        {
                            throw new GameException($"Story route source exit does not exist. story:{storyId} volume:{volume.VolumeId} edge:{edge.EdgeId} episode:{edge.FromEpisodeId} exit:{edge.FromExitId}");
                        }

                        var exitKey = edge.FromEpisodeId + "\n" + edge.FromExitId;
                        if (!boundExits.Add(exitKey))
                        {
                            throw new GameException($"Story episode exit cannot target multiple episodes. story:{storyId} volume:{volume.VolumeId} episode:{edge.FromEpisodeId} exit:{edge.FromExitId}");
                        }

                        outgoing[edge.FromEpisodeId].Add(edge.ToEpisodeId);
                        break;
                    default:
                        throw new GameException($"Story route edge source kind is invalid. story:{storyId} volume:{volume.VolumeId} edge:{edge.EdgeId} kind:{edge.SourceKind}");
                }
            }

            foreach (var pair in incoming)
            {
                if (pair.Value != 1)
                {
                    throw new GameException($"Story episode must have exactly one incoming route edge. story:{storyId} volume:{volume.VolumeId} episode:{pair.Key}");
                }
            }

            if (roots.Count == 0)
            {
                throw new GameException($"Story volume route must have at least one root edge. story:{storyId} volume:{volume.VolumeId}");
            }

            var states = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var episodeId in episodes.Keys)
            {
                VisitRoute(storyId, volume.VolumeId, episodeId, outgoing, states);
            }

            var reachable = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < roots.Count; i++)
            {
                MarkReachable(roots[i], outgoing, reachable);
            }

            foreach (var episodeId in episodes.Keys)
            {
                if (!reachable.Contains(episodeId))
                {
                    throw new GameException($"Story episode is not reachable from the volume root. story:{storyId} volume:{volume.VolumeId} episode:{episodeId}");
                }
            }
        }

        private static void VisitRoute(
            string storyId,
            string volumeId,
            string episodeId,
            IReadOnlyDictionary<string, List<string>> outgoing,
            IDictionary<string, int> states)
        {
            if (states.TryGetValue(episodeId, out var state))
            {
                if (state == 1)
                {
                    throw new GameException($"Story volume route cannot contain a cycle. story:{storyId} volume:{volumeId} episode:{episodeId}");
                }

                return;
            }

            states[episodeId] = 1;
            var children = outgoing[episodeId];
            for (var i = 0; i < children.Count; i++)
            {
                VisitRoute(storyId, volumeId, children[i], outgoing, states);
            }

            states[episodeId] = 2;
        }

        private static void MarkReachable(
            string episodeId,
            IReadOnlyDictionary<string, List<string>> outgoing,
            ISet<string> reachable)
        {
            if (!reachable.Add(episodeId))
            {
                return;
            }

            var children = outgoing[episodeId];
            for (var i = 0; i < children.Count; i++)
            {
                MarkReachable(children[i], outgoing, reachable);
            }
        }

        private static void ValidateEpisode(
            Program program,
            Volume volume,
            Episode episode,
            IReadOnlyDictionary<string, Step> steps,
            ISet<string> exits)
        {
            if (!steps.TryGetValue(episode.EntryStepId, out var entryStep) || entryStep.Kind != StepKind.Start)
            {
                throw new GameException($"Story episode entry must reference its Start step. story:{program.StoryId} volume:{volume.VolumeId} episode:{episode.EpisodeId} step:{episode.EntryStepId}");
            }

            var startCount = 0;
            var usedExits = new HashSet<string>(StringComparer.Ordinal);
            var choiceIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < episode.Steps.Count; i++)
            {
                var step = episode.Steps[i];
                if (step.Kind == StepKind.Start)
                {
                    startCount++;
                }

                ValidateStep(program, volume, episode, step, steps, exits, usedExits, choiceIds);
            }

            if (startCount != 1)
            {
                throw new GameException($"Story episode must contain exactly one Start step. story:{program.StoryId} volume:{volume.VolumeId} episode:{episode.EpisodeId}");
            }

            foreach (var exitId in exits)
            {
                if (!usedExits.Contains(exitId))
                {
                    throw new GameException($"Story episode exit is not declared by a Choice or End terminal. story:{program.StoryId} volume:{volume.VolumeId} episode:{episode.EpisodeId} exit:{exitId}");
                }
            }
        }

        private static void ValidateStep(
            Program program,
            Volume volume,
            Episode episode,
            Step step,
            IReadOnlyDictionary<string, Step> steps,
            ISet<string> exits,
            ISet<string> usedExits,
            ISet<string> choiceIds)
        {
            var storyId = program.StoryId;
            var episodeId = episode.EpisodeId;
            switch (step.Kind)
            {
                case StepKind.Start:
                    ValidateTarget(storyId, volume.VolumeId, episodeId, step.StepId, step.Data.Target, steps, "start target");
                    break;
                case StepKind.Line:
                    ValidateLineStep(storyId, volume.VolumeId, episodeId, step);
                    ValidateTarget(storyId, volume.VolumeId, episodeId, step.StepId, step.Data.Target, steps, "line target");
                    break;
                case StepKind.Choice:
                    ValidateChoiceStep(storyId, volume.VolumeId, episodeId, step, exits, usedExits, choiceIds);
                    break;
                case StepKind.Command:
                    ValidateCommandStep(storyId, volume.VolumeId, episodeId, step, steps, program);
                    break;
                case StepKind.Branch:
                    ValidateBranchStep(storyId, volume.VolumeId, episodeId, step, steps);
                    break;
                case StepKind.Jump:
                    ValidateJumpStep(storyId, volume.VolumeId, episodeId, step, steps);
                    break;
                case StepKind.Wait:
                    if (!TimeRules.IsFiniteNonNegative(step.Data.WaitSeconds))
                    {
                        throw new GameException($"Story wait seconds must be finite and non-negative. story:{storyId} volume:{volume.VolumeId} episode:{episodeId} step:{step.StepId}");
                    }

                    ValidateTarget(storyId, volume.VolumeId, episodeId, step.StepId, step.Data.Target, steps, "wait target");
                    break;
                case StepKind.End:
                    if (string.IsNullOrWhiteSpace(step.Data.ExitId) || !exits.Contains(step.Data.ExitId))
                    {
                        throw new GameException($"Story End step must reference a declared episode exit. story:{storyId} volume:{volume.VolumeId} episode:{episodeId} step:{step.StepId} exit:{step.Data.ExitId}");
                    }

                    if (!usedExits.Add(step.Data.ExitId))
                    {
                        throw new GameException($"Story episode exit must be declared by exactly one Choice or End terminal. story:{storyId} volume:{volume.VolumeId} episode:{episodeId} exit:{step.Data.ExitId}");
                    }

                    break;
                case StepKind.Parallel:
                    ValidateParallelStep(storyId, volume.VolumeId, episodeId, step, steps);
                    break;
                case StepKind.Merge:
                    ValidateMergeStep(storyId, volume.VolumeId, episodeId, step, steps);
                    ValidateTarget(storyId, volume.VolumeId, episodeId, step.StepId, step.Data.Target, steps, "merge target");
                    break;
                default:
                    throw new GameException($"Story step kind is invalid. story:{storyId} volume:{volume.VolumeId} episode:{episodeId} step:{step.StepId} kind:{step.Kind}");
            }
        }

        private static void ValidateLineStep(string storyId, string volumeId, string episodeId, Step step)
        {
            if (string.IsNullOrWhiteSpace(step.Data.TextKey))
            {
                throw new GameException($"Story line text key cannot be empty. story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId}");
            }
        }

        private static void ValidateChoiceStep(
            string storyId,
            string volumeId,
            string episodeId,
            Step step,
            ISet<string> exits,
            ISet<string> usedExits,
            ISet<string> choiceIds)
        {
            if (step.Choices.Count == 0)
            {
                throw new GameException($"Story choice step has no options. story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId}");
            }

            for (var i = 0; i < step.Choices.Count; i++)
            {
                var choice = step.Choices[i];
                if (choice == null)
                {
                    throw new GameException($"Story choice cannot be null. story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId} index:{i}");
                }

                ValidateId(choice.ChoiceId, "choice", storyId);
                if (!choiceIds.Add(choice.ChoiceId))
                {
                    throw new GameException($"Duplicate story choice id. story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId} choice:{choice.ChoiceId}");
                }

                if (string.IsNullOrWhiteSpace(choice.ExitId) || !exits.Contains(choice.ExitId))
                {
                    throw new GameException($"Story Choice must reference a declared episode exit. story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId} choice:{choice.ChoiceId} exit:{choice.ExitId}");
                }

                if (!usedExits.Add(choice.ExitId))
                {
                    throw new GameException($"Story episode exit must be declared by exactly one Choice or End terminal. story:{storyId} volume:{volumeId} episode:{episodeId} exit:{choice.ExitId}");
                }
            }
        }

        private static void ValidateCommandStep(
            string storyId,
            string volumeId,
            string episodeId,
            Step step,
            IReadOnlyDictionary<string, Step> steps,
            Program program)
        {
            if (step.Data.Command == null)
            {
                throw new GameException($"Story command cannot be null. story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId}");
            }

            CommandDefinition commandDefinition = null;
            if (program.CommandSchema?.Definitions != null)
            {
                for (var i = 0; i < program.CommandSchema.Definitions.Count; i++)
                {
                    var definition = program.CommandSchema.Definitions[i];
                    if (definition != null && string.Equals(definition.Name, step.Data.Command.Name, StringComparison.Ordinal))
                    {
                        commandDefinition = definition;
                        break;
                    }
                }

                if (commandDefinition == null)
                {
                    if (EventCommandCodec.HasEventMarker(step.Data.Command))
                    {
                        throw new GameException($"Story event definition is not registered. story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId} event:{step.Data.Command.Name}");
                    }

                    throw new GameException($"Story command schema is not registered. story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId} command:{step.Data.Command.Name}");
                }
            }

            if (commandDefinition != null)
            {
                ValidateCommandArguments(storyId, volumeId, episodeId, step, commandDefinition);
                ValidateCommandOutcomePorts(storyId, volumeId, episodeId, step, commandDefinition);
            }

            ValidateBuiltInCommand(storyId, volumeId, episodeId, step, commandDefinition);
            ValidateTarget(storyId, volumeId, episodeId, step.StepId, step.Data.Target, steps, "command target");
            foreach (var pair in step.Data.Command.OutcomeTargets)
            {
                ValidateTarget(storyId, volumeId, episodeId, step.StepId, pair.Value, steps, $"command outcome:{pair.Key}");
            }
        }

        private static void ValidateBuiltInCommand(
            string storyId,
            string volumeId,
            string episodeId,
            Step step,
            CommandDefinition commandDefinition)
        {
            var command = step.Data.Command;
            if (EventCommandCodec.HasEventMarker(command))
            {
                ValidateEventCommand(storyId, volumeId, episodeId, step, commandDefinition);
                return;
            }

            if (string.Equals(command.Name, MediaCommandNames.PlayVideo, StringComparison.Ordinal))
            {
                if (!Media.VideoReferenceCodec.TryDeserializeCommand(command.Arguments, out _, out _, out var error))
                {
                    throw new GameException($"Story video command is invalid. story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId} command:{command.Name} reason:{error}");
                }

                return;
            }

        }

        private static void ValidateEventCommand(
            string storyId,
            string volumeId,
            string episodeId,
            Step step,
            CommandDefinition definition)
        {
            var command = step.Data.Command;
            var source = $"story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId} event:{command.Name}";
            if (!EventCommandCodec.TryDecode(command, out var request, out var error))
            {
                throw new GameException($"Story event command is invalid. {source} reason:{error}");
            }

            if (definition == null)
            {
                throw new GameException($"Story event definition is not registered. {source}");
            }

            foreach (var pair in request.Arguments.Values)
            {
                if (!ContainsCommandArgument(definition, pair.Key))
                {
                    throw new GameException($"Story event argument is not declared. {source} argument:{pair.Key}");
                }
            }

            if (request.Mode == EventMode.Notify)
            {
                if (step.Data.Target?.TargetKind != TargetKind.Step || command.OutcomeTargets.Count != 0)
                {
                    throw new GameException($"Story Notify event must continue to exactly one step. {source}");
                }

                return;
            }

            if (step.Data.Target != null || command.OutcomeTargets.Count != request.Outcomes.Count)
            {
                throw new GameException($"Story Request event must advance only through declared outcomes. {source}");
            }

            for (var i = 0; i < request.Outcomes.Count; i++)
            {
                var outcome = request.Outcomes[i];
                if (!command.OutcomeTargets.TryGetValue(outcome, out var target) ||
                    target?.TargetKind != TargetKind.Step)
                {
                    throw new GameException($"Story Request event outcome target is missing or invalid. {source} outcome:{outcome}");
                }
            }
        }

        private static bool ContainsCommandArgument(CommandDefinition definition, string key)
        {
            for (var i = 0; i < definition.ArgumentDefinitions.Count; i++)
            {
                var argument = definition.ArgumentDefinitions[i];
                if (argument != null && string.Equals(argument.Key, key, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateCommandOutcomePorts(
            string storyId,
            string volumeId,
            string episodeId,
            Step step,
            CommandDefinition definition)
        {
            var command = step.Data.Command;
            for (var i = 0; i < command.OutcomePorts.Count; i++)
            {
                if (!ContainsOutcomePort(definition, command.OutcomePorts[i]))
                {
                    throw new GameException($"Story command outcome is not declared in schema. story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId} command:{command.Name} outcome:{command.OutcomePorts[i]}");
                }
            }

            foreach (var pair in command.OutcomeTargets)
            {
                if (!ContainsOutcomePort(definition, pair.Key))
                {
                    throw new GameException($"Story command outcome is not declared in schema. story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId} command:{command.Name} outcome:{pair.Key}");
                }
            }
        }

        private static bool ContainsOutcomePort(CommandDefinition definition, string outcomePort)
        {
            if (definition.OutcomePorts == null || string.IsNullOrWhiteSpace(outcomePort))
            {
                return false;
            }

            for (var i = 0; i < definition.OutcomePorts.Count; i++)
            {
                if (string.Equals(definition.OutcomePorts[i], outcomePort, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateCommandArguments(
            string storyId,
            string volumeId,
            string episodeId,
            Step step,
            CommandDefinition definition)
        {
            for (var i = 0; i < definition.ArgumentDefinitions.Count; i++)
            {
                var argumentDefinition = definition.ArgumentDefinitions[i];
                if (argumentDefinition == null || string.IsNullOrWhiteSpace(argumentDefinition.Key))
                {
                    continue;
                }

                var source = $"story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId} command:{step.Data.Command.Name} argument:{argumentDefinition.Key}";
                if (!step.Data.Command.Arguments.TryGetValue(argumentDefinition.Key, out var value))
                {
                    if (argumentDefinition.Required)
                    {
                        throw new GameException($"Story command required argument is missing. {source}");
                    }

                    continue;
                }

                if (argumentDefinition.Required && IsEmptyArgument(value))
                {
                    throw new GameException($"Story command required argument is empty. {source}");
                }

                if (!IsCommandArgumentTypeValid(argumentDefinition, value))
                {
                    throw new GameException($"Story command argument type is invalid. {source}");
                }
            }
        }

        private static bool IsEmptyArgument(Value value)
        {
            return value.IsNull || (value.IsString && string.IsNullOrWhiteSpace(value.StringValue));
        }

        private static bool IsCommandArgumentTypeValid(CommandArgumentDefinition definition, Value value)
        {
            if (value.IsNull)
            {
                return !definition.Required;
            }

            switch (definition.ValueType)
            {
                case ParameterValueType.Number:
                    return value.IsNumber;
                case ParameterValueType.Boolean:
                    return value.IsBoolean;
                case ParameterValueType.String:
                case ParameterValueType.AssetReference:
                    return value.IsString;
                case ParameterValueType.Option:
                    return value.IsString && IsOptionArgumentValueValid(definition, value.StringValue);
                default:
                    return value.IsString;
            }
        }

        private static bool IsOptionArgumentValueValid(CommandArgumentDefinition definition, string value)
        {
            if (definition.Options == null || definition.Options.Count == 0)
            {
                return true;
            }

            for (var i = 0; i < definition.Options.Count; i++)
            {
                if (string.Equals(definition.Options[i], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateBranchStep(
            string storyId,
            string volumeId,
            string episodeId,
            Step step,
            IReadOnlyDictionary<string, Step> steps)
        {
            if (step.Data.Condition == null)
            {
                throw new GameException($"Story branch condition cannot be null. story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId}");
            }

            ValidateTarget(storyId, volumeId, episodeId, step.StepId, step.Data.Target, steps, "branch target");
        }

        private static void ValidateJumpStep(
            string storyId,
            string volumeId,
            string episodeId,
            Step step,
            IReadOnlyDictionary<string, Step> steps)
        {
            if (step.Data.Target == null || step.Data.Target.TargetKind != TargetKind.Step)
            {
                throw new GameException($"Story Jump step must target a step in the same episode. story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId}");
            }

            ValidateTarget(storyId, volumeId, episodeId, step.StepId, step.Data.Target, steps, "jump target");
        }

        private static void ValidateParallelStep(
            string storyId,
            string volumeId,
            string episodeId,
            Step step,
            IReadOnlyDictionary<string, Step> steps)
        {
            if (step.Data.Branches.Count < 2)
            {
                throw new GameException($"Story parallel step must have at least two branches. story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId}");
            }

            var branchIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < step.Data.Branches.Count; i++)
            {
                var branch = step.Data.Branches[i];
                if (branch == null || !branchIds.Add(branch.BranchId))
                {
                    throw new GameException($"Story parallel branch is null or duplicated. story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId} index:{i}");
                }

                if (branch.Entry == null || branch.Entry.TargetKind != TargetKind.Step || !steps.ContainsKey(branch.Entry.StepId))
                {
                    throw new GameException($"Story parallel branch entry must target a step in the same episode. story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId} branch:{branch.BranchId}");
                }
            }
        }

        private static void ValidateMergeStep(
            string storyId,
            string volumeId,
            string episodeId,
            Step step,
            IReadOnlyDictionary<string, Step> steps)
        {
            if (step.Data.MergePolicy != MergePolicy.All)
            {
                throw new GameException($"Story merge policy is invalid. story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId} policy:{step.Data.MergePolicy}");
            }

            if (string.IsNullOrWhiteSpace(step.Data.ParallelStepId) ||
                !steps.TryGetValue(step.Data.ParallelStepId, out var parallelStep) ||
                parallelStep.Kind != StepKind.Parallel)
            {
                throw new GameException($"Story merge parallel step does not exist. story:{storyId} volume:{volumeId} episode:{episodeId} step:{step.StepId} parallel:{step.Data.ParallelStepId}");
            }
        }

        private static void ValidateTarget(
            string storyId,
            string volumeId,
            string episodeId,
            string sourceStepId,
            Target target,
            IReadOnlyDictionary<string, Step> steps,
            string label)
        {
            if (target == null)
            {
                return;
            }

            switch (target.TargetKind)
            {
                case TargetKind.EpisodeEnd:
                    return;
                case TargetKind.Step:
                    if (string.IsNullOrWhiteSpace(target.StepId) || !steps.ContainsKey(target.StepId))
                    {
                        throw new GameException($"Story target step does not exist. story:{storyId} volume:{volumeId} episode:{episodeId} step:{sourceStepId} {label} targetStep:{target.StepId}");
                    }

                    return;
                default:
                    throw new GameException($"Story target kind is invalid. story:{storyId} volume:{volumeId} episode:{episodeId} step:{sourceStepId} {label} kind:{target.TargetKind}");
            }
        }

        private static void ValidateText(string value, string parameterName, string message)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(message, parameterName);
            }
        }

        private static void ValidateId(string value, string fieldName, string storyId)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new GameException($"Story {fieldName} cannot be empty. story:{storyId}");
            }
        }
    }
}
