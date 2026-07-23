using System;
using GameDeveloperKit.Story.Execution;
using GameDeveloperKit.Story.Model;

namespace GameDeveloperKit.Story
{
    public sealed partial class StoryModule
    {
        /// <summary>
        /// 剧情段完成时触发。
        /// </summary>
        public event Action<EpisodeCompletion> EpisodeCompleted;

        private Frame AdvanceCurrent(Func<Frame> advance)
        {
            var runner = CurrentRunner;
            var wasCompleted = runner.Completed;
            var frame = advance();
            return ProcessCompletion(runner, wasCompleted, frame);
        }

        private Frame ProcessCompletion(Runner runner, bool wasCompleted, Frame frame)
        {
            if (runner == null || wasCompleted || frame == null || frame.IsCompleted is false)
            {
                return frame;
            }

            while (frame != null && frame.IsCompleted)
            {
                var routeEdge = FindRouteEdge(runner.CurrentVolume, runner.CurrentEpisodeId, frame.CompletedExitId);
                if (frame.CompletedKind == EpisodeCompletionKind.Transition && routeEdge == null)
                {
                    throw new GameException($"Story Transition route is missing. story:{runner.StoryId} volume:{runner.CurrentVolumeId} episode:{runner.CurrentEpisodeId} exit:{frame.CompletedExitId}");
                }

                EpisodeCompleted?.Invoke(new EpisodeCompletion(
                    runner.StoryId,
                    runner.CurrentVolumeId,
                    runner.CurrentEpisodeId,
                    frame.CompletedKind,
                    frame.CompletedExitId,
                    frame.CompletedSettlementId,
                    routeEdge?.EdgeId,
                    routeEdge?.ToEpisodeId));

                if (frame.CompletedKind != EpisodeCompletionKind.Transition)
                {
                    return frame;
                }

                var nextRunner = new Runner(runner.Program, FunctionResolver);
                frame = nextRunner.Start(runner.CurrentVolumeId, routeEdge.Value.ToEpisodeId);
                ReplaceCurrentRunner(nextRunner);
                runner = nextRunner;
            }

            return frame;
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
    }
}
