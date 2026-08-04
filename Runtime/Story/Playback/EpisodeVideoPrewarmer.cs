using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Model;
using UnityEngine;
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

            var instructions = CollectInitialVideoInstructions(
                storyModule,
                storyId,
                program,
                volumeId,
                episodeId);
            var requests = new List<VideoPlayableRequest>(instructions.Count);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];
                var request = VideoRequestFactory.Create(
                    instruction.Reference,
                    App.Config.MediaDelivery,
                    instruction.Loop,
                    instruction.Seekable,
                    null,
                    true);
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

        internal static IReadOnlyList<StoryInstruction.PlayVideo> CollectInitialVideoInstructions(
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
                return Array.Empty<StoryInstruction.PlayVideo>();
            }

            var instructions = new List<StoryInstruction.PlayVideo>();
            for (var i = 0; i < frame.Instructions.Count; i++)
            {
                if (frame.Instructions[i] is StoryInstruction.PlayVideo instruction)
                {
                    instructions.Add(instruction);
                }
            }

            return instructions;
        }

        internal static StoryInstruction.PlayVideo FindNextVideoInstruction(Episode episode, Step currentStep)
        {
            return FindNextVideoInstruction(episode, currentStep, out _);
        }

        internal static StoryInstruction.PlayVideo FindNextVideoInstruction(
            Frame currentFrame,
            Step currentStep,
            IFunctionResolver functionResolver = null)
        {
            if (currentFrame == null || currentStep == null)
            {
                return null;
            }

            var instruction = FindNextVideoInstruction(currentFrame.Episode, currentStep, out var transitionExitId);
            if (instruction != null || string.IsNullOrWhiteSpace(transitionExitId))
            {
                return instruction;
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
            return FindVideoInstruction(nextFrame);
        }

        internal static StoryInstruction.PlayVideo FindChoiceVideoInstruction(
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

            return FindChoiceVideoInstruction(currentFrame, selectedChoice, functionResolver);
        }

        private static StoryInstruction.PlayVideo FindChoiceVideoInstruction(
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
            return FindVideoInstruction(nextFrame) ??
                   FindNextVideoInstruction(nextFrame, nextFrame?.AnchorStep, functionResolver);
        }

        internal static IReadOnlyList<StoryInstruction.PlayVideo> CollectChoiceVideoInstructions(
            Frame currentFrame,
            IFunctionResolver functionResolver = null)
        {
            if (currentFrame?.Episode?.Steps == null)
            {
                return Array.Empty<StoryInstruction.PlayVideo>();
            }

            var instructions = new List<StoryInstruction.PlayVideo>();
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
                    // 直接按选项解析，不依赖 currentFrame.Choices（选项可能不在当前帧，如并行帧后的选项步骤）。
                    var instruction = choice == null
                        ? null
                        : FindChoiceVideoInstruction(currentFrame, choice, functionResolver);
                    if (instruction != null && instructions.Contains(instruction) is false)
                    {
                        instructions.Add(instruction);
                    }
                }
            }

            return instructions;
        }

        internal static StoryInstruction.PlayVideo FindNextVideoInstruction(
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
            return FindVideoDownstream(steps, visited, currentStep, out transitionExitId);
        }

        /// <summary>
        /// 沿确定性步骤链向下查找下一段视频；并行步骤沿各轨道查找。
        /// </summary>
        private static StoryInstruction.PlayVideo FindVideoDownstream(
            IReadOnlyDictionary<string, Step> steps,
            HashSet<string> visited,
            Step start,
            out string transitionExitId)
        {
            transitionExitId = null;
            var target = start.Data?.Target;
            while (target?.TargetKind == TargetKind.Step &&
                   steps.TryGetValue(target.StepId, out var step) &&
                   visited.Add(step.StepId))
            {
                switch (step.Kind)
                {
                    case StepKind.PlayVideo:
                        return StoryInstruction.Create(step) as StoryInstruction.PlayVideo;
                    case StepKind.Transition:
                        transitionExitId = step.Data?.ExitId;
                        return null;
                    case StepKind.Parallel:
                        return FindVideoInParallel(steps, visited, step, out transitionExitId);
                    case StepKind.Choice:
                    case StepKind.End:
                        return null;
                    default:
                        if (CanFollowDeterministically(step) is false)
                        {
                            return null;
                        }

                        target = step.Data?.Target;
                        break;
                }
            }

            return null;
        }

        /// <summary>
        /// 并行帧的下一段视频：沿各轨道入口向下查找，取第一条；轨道尽头若有过渡则记录其出口。
        /// </summary>
        private static StoryInstruction.PlayVideo FindVideoInParallel(
            IReadOnlyDictionary<string, Step> steps,
            HashSet<string> visited,
            Step parallelStep,
            out string transitionExitId)
        {
            transitionExitId = null;
            var branches = parallelStep.Data?.Branches;
            if (branches == null || branches.Count == 0)
            {
                return null;
            }

            for (var i = 0; i < branches.Count; i++)
            {
                var entry = branches[i]?.Entry;
                if (entry?.TargetKind != TargetKind.Step ||
                    steps.TryGetValue(entry.StepId, out var branchStart) is false ||
                    visited.Add(branchStart.StepId) is false)
                {
                    continue;
                }

                var instruction = FindVideoInBranch(steps, visited, branchStart, out var branchExit);
                if (instruction != null)
                {
                    return instruction;
                }

                if (string.IsNullOrWhiteSpace(branchExit) is false)
                {
                    transitionExitId = branchExit;
                }
            }

            return null;
        }

        /// <summary>
        /// 轨道内查找下一段视频：入口步骤本身（视频/过渡/选项）或其下游链路。
        /// </summary>
        private static StoryInstruction.PlayVideo FindVideoInBranch(
            IReadOnlyDictionary<string, Step> steps,
            HashSet<string> visited,
            Step branchStart,
            out string transitionExitId)
        {
            transitionExitId = null;
            switch (branchStart.Kind)
            {
                case StepKind.PlayVideo:
                    return StoryInstruction.Create(branchStart) as StoryInstruction.PlayVideo;
                case StepKind.Transition:
                    transitionExitId = branchStart.Data?.ExitId;
                    return null;
                case StepKind.Parallel:
                    return FindVideoInParallel(steps, visited, branchStart, out transitionExitId);
                case StepKind.Choice:
                case StepKind.End:
                    return null;
                default:
                    return FindVideoDownstream(steps, visited, branchStart, out transitionExitId);
            }
        }

        private static StoryInstruction.PlayVideo FindVideoInstruction(Frame frame)
        {
            if (frame?.Instructions == null)
            {
                return null;
            }

            for (var i = 0; i < frame.Instructions.Count; i++)
            {
                if (frame.Instructions[i] is StoryInstruction.PlayVideo instruction)
                {
                    return instruction;
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
                case StepKind.Wait:
                    return true;
                case StepKind.PlayVideo:
                case StepKind.ShowImage:
                case StepKind.PlayAudio:
                case StepKind.Unlock:
                    return true;
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
