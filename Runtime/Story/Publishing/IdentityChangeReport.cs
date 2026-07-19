using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story.Publishing
{
    public sealed class IdentityChangeReport
    {
        private IdentityChangeReport(
            IReadOnlyList<string> addedEpisodeIds,
            IReadOnlyList<string> removedEpisodeIds,
            IReadOnlyList<string> addedEdgeIds,
            IReadOnlyList<string> removedEdgeIds,
            IReadOnlyList<ExitIdentity> removedExits)
        {
            AddedEpisodeIds = addedEpisodeIds;
            RemovedEpisodeIds = removedEpisodeIds;
            AddedEdgeIds = addedEdgeIds;
            RemovedEdgeIds = removedEdgeIds;
            RemovedExits = removedExits;
        }

        public IReadOnlyList<string> AddedEpisodeIds { get; }

        public IReadOnlyList<string> RemovedEpisodeIds { get; }

        public IReadOnlyList<string> AddedEdgeIds { get; }

        public IReadOnlyList<string> RemovedEdgeIds { get; }

        public IReadOnlyList<ExitIdentity> RemovedExits { get; }

        public bool HasBreakingChanges =>
            RemovedEpisodeIds.Count > 0 || RemovedEdgeIds.Count > 0 || RemovedExits.Count > 0;

        public static IdentityChangeReport Compare(IdentityManifest baseline, IdentityManifest current)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (baseline == null)
            {
                return new IdentityChangeReport(
                    Copy(current.EpisodeIds),
                    Array.Empty<string>(),
                    Copy(current.EdgeIds),
                    Array.Empty<string>(),
                    Array.Empty<ExitIdentity>());
            }

            if (!string.Equals(baseline.StoryId, current.StoryId, StringComparison.Ordinal))
            {
                return new IdentityChangeReport(
                    Copy(current.EpisodeIds),
                    Copy(baseline.EpisodeIds),
                    Copy(current.EdgeIds),
                    Copy(baseline.EdgeIds),
                    Copy(baseline.Exits));
            }

            return new IdentityChangeReport(
                Added(baseline.EpisodeIds, current.EpisodeIds),
                Removed(baseline.EpisodeIds, current.EpisodeIds),
                Added(baseline.EdgeIds, current.EdgeIds),
                Removed(baseline.EdgeIds, current.EdgeIds),
                FindRemovedExits(baseline.Exits, current.Exits));
        }

        private static IReadOnlyList<string> Added(
            IReadOnlyList<string> baseline,
            IReadOnlyList<string> current)
        {
            return Except(current, baseline);
        }

        private static IReadOnlyList<string> Removed(
            IReadOnlyList<string> baseline,
            IReadOnlyList<string> current)
        {
            return Except(baseline, current);
        }

        private static IReadOnlyList<string> Except(
            IReadOnlyList<string> values,
            IReadOnlyList<string> excluded)
        {
            var excludedSet = new HashSet<string>(excluded, StringComparer.Ordinal);
            var result = new List<string>();
            for (var i = 0; i < values.Count; i++)
            {
                if (!excludedSet.Contains(values[i]))
                {
                    result.Add(values[i]);
                }
            }

            return result.AsReadOnly();
        }

        private static IReadOnlyList<ExitIdentity> FindRemovedExits(
            IReadOnlyList<ExitIdentity> baseline,
            IReadOnlyList<ExitIdentity> current)
        {
            var currentSet = new HashSet<ExitIdentity>(current);
            var result = new List<ExitIdentity>();
            for (var i = 0; i < baseline.Count; i++)
            {
                if (!currentSet.Contains(baseline[i]))
                {
                    result.Add(baseline[i]);
                }
            }

            return result.AsReadOnly();
        }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values)
        {
            return values == null || values.Count == 0
                ? Array.Empty<T>()
                : new List<T>(values).AsReadOnly();
        }
    }
}
