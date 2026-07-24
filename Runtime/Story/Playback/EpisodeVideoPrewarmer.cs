using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Protocol;
using UnityEngine;
using StoryCommand = GameDeveloperKit.Story.Model.Command;
using StoryProgram = GameDeveloperKit.Story.Model.Program;

namespace GameDeveloperKit.Story.Playback
{
    public static class EpisodeVideoPrewarmer
    {
        public static EpisodeVideoPrewarmSession PrewarmEpisode(
            StoryModule storyModule,
            PlayableModule playableModule,
            string storyId,
            string volumeId,
            string episodeId)
        {
            if (storyModule == null)
            {
                throw new ArgumentNullException(nameof(storyModule));
            }

            if (playableModule == null)
            {
                throw new ArgumentNullException(nameof(playableModule));
            }

            ValidateId(storyId, nameof(storyId));
            ValidateId(volumeId, nameof(volumeId));
            ValidateId(episodeId, nameof(episodeId));
            if (storyModule.TryGetProgram(storyId, out var program) is false)
            {
                throw new GameException($"Story program is not registered. story:{storyId}");
            }

            var commands = CollectInitialVideoCommands(
                storyModule,
                storyId,
                program,
                volumeId,
                episodeId);
            var requests = new List<VideoPlayableRequest>(commands.Count);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < commands.Count; i++)
            {
                var request = MediaCommandHandler.CreateVideoRequest(commands[i], null, true);
                if (paths.Add(request.Path))
                {
                    requests.Add(request);
                }
            }

            return new EpisodeVideoPrewarmSession(
                playableModule.Video,
                storyId,
                volumeId,
                episodeId,
                requests);
        }

        internal static IReadOnlyList<StoryCommand> CollectInitialVideoCommands(
            StoryModule storyModule,
            string storyId,
            StoryProgram program,
            string volumeId,
            string episodeId)
        {
            if (storyModule == null)
            {
                throw new ArgumentNullException(nameof(storyModule));
            }

            if (program == null)
            {
                throw new GameException($"Story program is not registered. story:{storyId}");
            }

            ValidateEpisode(program, storyId, volumeId, episodeId);
            var previewRunner = new Runner(program, storyModule.FunctionResolver);
            var frame = previewRunner.Start(volumeId, episodeId);
            if (frame?.Tracks == null)
            {
                return Array.Empty<StoryCommand>();
            }

            var commands = new List<StoryCommand>();
            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                var command = track?.Command;
                if (track?.Kind == FrameTrackKind.Command &&
                    command != null &&
                    string.Equals(command.Name, MediaCommandNames.PlayVideo, StringComparison.Ordinal))
                {
                    commands.Add(command);
                }
            }

            return commands;
        }

        internal static StoryCommand FindNextVideoCommand(Episode episode, Step currentStep)
        {
            return FindNextVideoCommand(episode, currentStep, out _);
        }

        internal static StoryCommand FindNextVideoCommand(
            Frame currentFrame,
            Step currentStep,
            IFunctionResolver functionResolver = null)
        {
            if (currentFrame == null || currentStep == null)
            {
                return null;
            }

            var command = FindNextVideoCommand(currentFrame.Episode, currentStep, out var transitionExitId);
            if (command != null || string.IsNullOrWhiteSpace(transitionExitId))
            {
                return command;
            }

            var routeEdge = FindRouteEdge(
                currentFrame.Volume,
                currentFrame.Episode?.EpisodeId,
                transitionExitId);
            if (routeEdge.HasValue is false)
            {
                return null;
            }

            var previewRunner = new Runner(currentFrame.Program, functionResolver);
            var nextFrame = previewRunner.Start(currentFrame.Volume.VolumeId, routeEdge.Value.ToEpisodeId);
            return FindVideoCommand(nextFrame);
        }

        internal static StoryCommand FindChoiceVideoCommand(
            Frame currentFrame,
            string choiceId,
            IFunctionResolver functionResolver = null)
        {
            if (currentFrame?.Choices == null ||
                currentFrame.Volume == null ||
                string.IsNullOrWhiteSpace(choiceId))
            {
                return null;
            }

            Choice selectedChoice = null;
            for (var i = 0; i < currentFrame.Choices.Count; i++)
            {
                var choice = currentFrame.Choices[i];
                if (choice != null && string.Equals(choice.ChoiceId, choiceId, StringComparison.Ordinal))
                {
                    selectedChoice = choice;
                    break;
                }
            }

            if (selectedChoice == null)
            {
                return null;
            }

            return FindChoiceVideoCommand(currentFrame, selectedChoice, functionResolver);
        }

        private static StoryCommand FindChoiceVideoCommand(
            Frame currentFrame,
            Choice selectedChoice,
            IFunctionResolver functionResolver)
        {
            if (currentFrame?.Volume == null || selectedChoice == null)
            {
                return null;
            }

            var routeEdge = FindRouteEdge(
                currentFrame.Volume,
                currentFrame.Episode?.EpisodeId,
                selectedChoice.ExitId);
            if (routeEdge.HasValue is false)
            {
                return null;
            }

            var previewRunner = new Runner(currentFrame.Program, functionResolver);
            var nextFrame = previewRunner.Start(currentFrame.Volume.VolumeId, routeEdge.Value.ToEpisodeId);
            return FindVideoCommand(nextFrame) ??
                   FindNextVideoCommand(nextFrame, nextFrame?.AnchorStep, functionResolver);
        }

        internal static IReadOnlyList<StoryCommand> CollectChoiceVideoCommands(
            Frame currentFrame,
            IFunctionResolver functionResolver = null)
        {
            if (currentFrame?.Episode?.Steps == null)
            {
                return Array.Empty<StoryCommand>();
            }

            var commands = new List<StoryCommand>();
            for (var stepIndex = 0; stepIndex < currentFrame.Episode.Steps.Count; stepIndex++)
            {
                var step = currentFrame.Episode.Steps[stepIndex];
                if (step?.Kind != StepKind.Choice || step.Choices == null)
                {
                    continue;
                }

                for (var choiceIndex = 0; choiceIndex < step.Choices.Count; choiceIndex++)
                {
                    var choice = step.Choices[choiceIndex];
                    var command = choice == null
                        ? null
                        : FindChoiceVideoCommand(currentFrame, choice, functionResolver);
                    if (command != null && commands.Contains(command) is false)
                    {
                        commands.Add(command);
                    }
                }
            }

            return commands;
        }

        private static StoryCommand FindNextVideoCommand(
            Episode episode,
            Step currentStep,
            out string transitionExitId)
        {
            transitionExitId = null;
            if (episode == null || currentStep == null)
            {
                return null;
            }

            var steps = new Dictionary<string, Step>(StringComparer.Ordinal);
            for (var i = 0; i < episode.Steps.Count; i++)
            {
                var step = episode.Steps[i];
                if (step != null)
                {
                    steps[step.StepId] = step;
                }
            }

            var visited = new HashSet<string>(StringComparer.Ordinal) { currentStep.StepId };
            var target = currentStep.Data?.Target;
            while (target?.TargetKind == TargetKind.Step &&
                   steps.TryGetValue(target.StepId, out var step) &&
                   visited.Add(step.StepId))
            {
                var command = step.Data?.Command;
                if (step.Kind == StepKind.Command &&
                    command != null &&
                    string.Equals(command.Name, MediaCommandNames.PlayVideo, StringComparison.Ordinal))
                {
                    return command;
                }

                if (step.Kind == StepKind.Transition)
                {
                    transitionExitId = step.Data?.ExitId;
                    return null;
                }

                if (CanFollowDeterministically(step) is false)
                {
                    return null;
                }

                target = step.Data?.Target;
            }

            return null;
        }

        private static StoryCommand FindVideoCommand(Frame frame)
        {
            if (frame?.Tracks == null)
            {
                return null;
            }

            for (var i = 0; i < frame.Tracks.Count; i++)
            {
                var track = frame.Tracks[i];
                if (track?.Kind == FrameTrackKind.Command &&
                    track.Command != null &&
                    string.Equals(track.Command.Name, MediaCommandNames.PlayVideo, StringComparison.Ordinal))
                {
                    return track.Command;
                }
            }

            return null;
        }

        private static RouteEdge? FindRouteEdge(Volume volume, string episodeId, string exitId)
        {
            if (volume?.Route?.Edges == null ||
                string.IsNullOrWhiteSpace(episodeId) ||
                string.IsNullOrWhiteSpace(exitId))
            {
                return null;
            }

            for (var i = 0; i < volume.Route.Edges.Count; i++)
            {
                var edge = volume.Route.Edges[i];
                if (edge.SourceKind == RouteEdgeSourceKind.EpisodeExit &&
                    string.Equals(edge.FromEpisodeId, episodeId, StringComparison.Ordinal) &&
                    string.Equals(edge.FromExitId, exitId, StringComparison.Ordinal))
                {
                    return edge;
                }
            }

            return null;
        }

        private static bool CanFollowDeterministically(Step step)
        {
            switch (step.Kind)
            {
                case StepKind.Start:
                case StepKind.Line:
                case StepKind.Jump:
                case StepKind.Wait:
                    return true;
                case StepKind.Command:
                    return step.Data?.Command?.OutcomeTargets.Count == 0;
                default:
                    return false;
            }
        }

        private static void ValidateEpisode(
            StoryProgram program,
            string storyId,
            string volumeId,
            string episodeId)
        {
            for (var volumeIndex = 0; volumeIndex < program.Volumes.Count; volumeIndex++)
            {
                var volume = program.Volumes[volumeIndex];
                if (volume?.VolumeId != volumeId)
                {
                    continue;
                }

                for (var episodeIndex = 0; episodeIndex < volume.Episodes.Count; episodeIndex++)
                {
                    if (volume.Episodes[episodeIndex]?.EpisodeId == episodeId)
                    {
                        return;
                    }
                }

                throw new GameException(
                    $"Story episode does not belong to the volume. " +
                    $"story:{storyId} volume:{volumeId} episode:{episodeId}");
            }

            throw new GameException(
                $"Story volume is not registered. story:{storyId} volume:{volumeId} episode:{episodeId}");
        }

        private static void ValidateId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Story playback id cannot be empty.", parameterName);
            }
        }
    }

    public sealed class EpisodeVideoPrewarmSession : IDisposable
    {
        private readonly VideoPlayable m_Video;
        private readonly IReadOnlyList<VideoPlayableRequest> m_Requests;
        private readonly UniTaskCompletionSource m_Completion = new UniTaskCompletionSource();
        private CancellationTokenSource m_Cancellation = new CancellationTokenSource();
        private bool m_PreserveForPlayback;
        private bool m_Disposed;

        internal EpisodeVideoPrewarmSession(
            VideoPlayable video,
            string storyId,
            string volumeId,
            string episodeId,
            IReadOnlyList<VideoPlayableRequest> requests)
        {
            m_Video = video ?? throw new ArgumentNullException(nameof(video));
            m_Requests = requests ?? throw new ArgumentNullException(nameof(requests));
            StoryId = storyId;
            VolumeId = volumeId;
            EpisodeId = episodeId;
            PrewarmAsync().Forget(Debug.LogException);
        }

        public string StoryId { get; }

        public string VolumeId { get; }

        public string EpisodeId { get; }

        public int VideoCount => m_Requests.Count;

        public UniTask Completion => m_Completion.Task;

        public void PreserveForPlayback()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(nameof(EpisodeVideoPrewarmSession));
            }

            m_PreserveForPlayback = true;
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            if (m_PreserveForPlayback is false)
            {
                m_Cancellation?.Cancel();
                for (var i = 0; i < m_Requests.Count; i++)
                {
                    m_Video.ReleasePreload(m_Requests[i].Path);
                }
            }
        }

        private async UniTask PrewarmAsync()
        {
            var cancellation = m_Cancellation;
            try
            {
                for (var i = 0; i < m_Requests.Count; i++)
                {
                    await m_Video.PreloadAsync(m_Requests[i], cancellation.Token);
                    cancellation.Token.ThrowIfCancellationRequested();
                }

                m_Completion.TrySetResult();
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                m_Completion.TrySetCanceled(cancellation.Token);
            }
            catch (Exception exception)
            {
                m_Completion.TrySetException(exception);
            }
            finally
            {
                cancellation.Dispose();
                if (ReferenceEquals(m_Cancellation, cancellation))
                {
                    m_Cancellation = null;
                }
            }
        }
    }
}
