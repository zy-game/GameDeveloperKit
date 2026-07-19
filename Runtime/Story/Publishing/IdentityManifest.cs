using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;

namespace GameDeveloperKit.Story.Publishing
{
    public readonly struct ExitIdentity : IEquatable<ExitIdentity>
    {
        public ExitIdentity(string episodeId, string exitId)
        {
            Validate(episodeId, nameof(episodeId));
            Validate(exitId, nameof(exitId));
            EpisodeId = episodeId;
            ExitId = exitId;
        }

        public string EpisodeId { get; }

        public string ExitId { get; }

        public bool Equals(ExitIdentity other)
        {
            return string.Equals(EpisodeId, other.EpisodeId, StringComparison.Ordinal) &&
                   string.Equals(ExitId, other.ExitId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ExitIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((EpisodeId != null ? StringComparer.Ordinal.GetHashCode(EpisodeId) : 0) * 397) ^
                       (ExitId != null ? StringComparer.Ordinal.GetHashCode(ExitId) : 0);
            }
        }

        private static void Validate(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Identity cannot be empty.", parameterName);
            }
        }
    }

    public sealed class IdentityManifest
    {
        public IdentityManifest(
            string storyId,
            string version,
            IReadOnlyList<string> episodeIds,
            IReadOnlyList<string> edgeIds,
            IReadOnlyList<ExitIdentity> exits)
        {
            Validate(storyId, nameof(storyId));
            Validate(version, nameof(version));

            StoryId = storyId;
            Version = version;
            EpisodeIds = CopyIds(episodeIds, nameof(episodeIds));
            EdgeIds = CopyIds(edgeIds, nameof(edgeIds));
            Exits = CopyExits(exits, EpisodeIds);
        }

        public string StoryId { get; }

        public string Version { get; }

        public IReadOnlyList<string> EpisodeIds { get; }

        public IReadOnlyList<string> EdgeIds { get; }

        public IReadOnlyList<ExitIdentity> Exits { get; }

        public static IdentityManifest Create(Program program)
        {
            if (program == null)
            {
                throw new ArgumentNullException(nameof(program));
            }

            var episodeIds = new List<string>();
            var edgeIds = new List<string>();
            var exits = new List<ExitIdentity>();
            for (var volumeIndex = 0; volumeIndex < program.Volumes.Count; volumeIndex++)
            {
                var volume = program.Volumes[volumeIndex] ??
                             throw new ArgumentException($"Story volume cannot be null. index:{volumeIndex}", nameof(program));
                for (var episodeIndex = 0; episodeIndex < volume.Episodes.Count; episodeIndex++)
                {
                    var episode = volume.Episodes[episodeIndex] ??
                                  throw new ArgumentException(
                                      $"Story episode cannot be null. volume:{volume.VolumeId} index:{episodeIndex}",
                                      nameof(program));
                    episodeIds.Add(episode.EpisodeId);
                    for (var exitIndex = 0; exitIndex < episode.Exits.Count; exitIndex++)
                    {
                        exits.Add(new ExitIdentity(episode.EpisodeId, episode.Exits[exitIndex].ExitId));
                    }
                }

                for (var edgeIndex = 0; edgeIndex < volume.Route.Edges.Count; edgeIndex++)
                {
                    edgeIds.Add(volume.Route.Edges[edgeIndex].EdgeId);
                }
            }

            return new IdentityManifest(program.StoryId, program.Version, episodeIds, edgeIds, exits);
        }

        private static IReadOnlyList<string> CopyIds(IReadOnlyList<string> values, string parameterName)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new List<string>(values.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                Validate(value, parameterName);
                if (!seen.Add(value))
                {
                    throw new ArgumentException($"Identity must be unique. id:{value}", parameterName);
                }

                copy.Add(value);
            }

            copy.Sort(StringComparer.Ordinal);
            return copy.AsReadOnly();
        }

        private static IReadOnlyList<ExitIdentity> CopyExits(
            IReadOnlyList<ExitIdentity> exits,
            IReadOnlyList<string> episodeIds)
        {
            if (exits == null || exits.Count == 0)
            {
                return Array.Empty<ExitIdentity>();
            }

            var episodes = new HashSet<string>(episodeIds, StringComparer.Ordinal);
            var copy = new List<ExitIdentity>(exits.Count);
            var seen = new HashSet<ExitIdentity>();
            for (var i = 0; i < exits.Count; i++)
            {
                var exit = exits[i];
                Validate(exit.EpisodeId, nameof(exits));
                Validate(exit.ExitId, nameof(exits));
                if (!episodes.Contains(exit.EpisodeId))
                {
                    throw new ArgumentException(
                        $"Exit identity must reference a declared episode. episode:{exit.EpisodeId} exit:{exit.ExitId}",
                        nameof(exits));
                }

                if (!seen.Add(exit))
                {
                    throw new ArgumentException(
                        $"Exit identity must be unique. episode:{exit.EpisodeId} exit:{exit.ExitId}",
                        nameof(exits));
                }

                copy.Add(exit);
            }

            copy.Sort(CompareExits);
            return copy.AsReadOnly();
        }

        private static int CompareExits(ExitIdentity left, ExitIdentity right)
        {
            var episode = string.Compare(left.EpisodeId, right.EpisodeId, StringComparison.Ordinal);
            return episode != 0
                ? episode
                : string.Compare(left.ExitId, right.ExitId, StringComparison.Ordinal);
        }

        private static void Validate(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Identity cannot be empty.", parameterName);
            }
        }
    }
}
