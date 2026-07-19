using System;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Publishing;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;

namespace GameDeveloperKit.StoryEditor.Compiler
{
    public static partial class ProgramCompiler
    {
        private static void AddPublishedIdentityIssues(
            AuthoringAsset asset,
            Program program,
            ValidationReport report)
        {
            if (asset == null || program == null || report == null)
            {
                return;
            }

            if (!asset.TryGetPublishedIdentity(out var baseline, out var baselineError))
            {
                if (!string.IsNullOrWhiteSpace(baselineError))
                {
                    report.AddError(
                        $"story:{asset.StoryId}/identity/baseline",
                        $"Published identity baseline is invalid. reason:{baselineError}");
                }

                return;
            }

            IdentityManifest current;
            try
            {
                current = IdentityManifest.Create(program);
            }
            catch (Exception exception)
            {
                report.AddError(
                    $"story:{asset.StoryId}/identity/manifest",
                    $"Identity manifest cannot be built. reason:{exception.Message}");
                return;
            }

            var changes = IdentityChangeReport.Compare(baseline, current);
            var baselineStoryId = baseline.StoryId;
            for (var i = 0; i < changes.RemovedEpisodeIds.Count; i++)
            {
                var episodeId = changes.RemovedEpisodeIds[i];
                report.AddWarning(
                    $"story:{baselineStoryId}/identity/episode:{episodeId}",
                    "Published episode identity will be removed and may invalidate external state.");
            }

            for (var i = 0; i < changes.RemovedEdgeIds.Count; i++)
            {
                var edgeId = changes.RemovedEdgeIds[i];
                report.AddWarning(
                    $"story:{baselineStoryId}/identity/edge:{edgeId}",
                    "Published route edge identity will be removed and may invalidate layout or external data.");
            }

            for (var i = 0; i < changes.RemovedExits.Count; i++)
            {
                var exit = changes.RemovedExits[i];
                report.AddWarning(
                    $"story:{baselineStoryId}/identity/episode:{exit.EpisodeId}/exit:{exit.ExitId}",
                    "Published episode exit identity will be removed and may invalidate external state.");
            }
        }
    }
}
