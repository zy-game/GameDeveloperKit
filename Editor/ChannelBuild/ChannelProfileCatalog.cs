using System;
using System.Collections.Generic;

namespace GameDeveloperKit.ChannelBuild
{
    public sealed class ChannelProfileCatalog
    {
        private readonly Dictionary<string, ChannelProfile> m_ProfilesById;

        public ChannelProfileCatalog(int schemaVersion, IReadOnlyList<ChannelProfile> profiles)
        {
            if (schemaVersion != ChannelProfileSource.CurrentSchemaVersion)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(schemaVersion),
                    schemaVersion,
                    "Only channel profile schema version 1 is supported.");
            }

            if (profiles == null)
            {
                throw new ArgumentNullException(nameof(profiles));
            }

            if (profiles.Count == 0)
            {
                throw new ArgumentException("At least one channel profile is required.", nameof(profiles));
            }

            var profileCopy = new List<ChannelProfile>(profiles.Count);
            m_ProfilesById = new Dictionary<string, ChannelProfile>(StringComparer.Ordinal);
            for (var i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];
                if (profile == null)
                {
                    throw new ArgumentException("Channel profile cannot be null.", nameof(profiles));
                }

                if (m_ProfilesById.ContainsKey(profile.Id))
                {
                    throw new ArgumentException(
                        $"Duplicate channel profile id '{profile.Id}'.",
                        nameof(profiles));
                }

                m_ProfilesById.Add(profile.Id, profile);
                profileCopy.Add(profile);
            }

            SchemaVersion = schemaVersion;
            Profiles = profileCopy.AsReadOnly();
        }

        public int SchemaVersion { get; }

        public IReadOnlyList<ChannelProfile> Profiles { get; }

        public ChannelProfile GetRequired(string id)
        {
            ChannelBuildContext.RequireSafeSegment(id, nameof(id));
            if (m_ProfilesById.TryGetValue(id, out var profile))
            {
                return profile;
            }

            throw new KeyNotFoundException($"Channel profile '{id}' was not found.");
        }
    }
}
