using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Publishing;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Publishing
{
    internal static class IdentityJson
    {
        public static string SerializeManifest(IdentityManifest manifest)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            return JsonUtility.ToJson(ManifestDocument.FromManifest(manifest), true);
        }

        public static string SerializeChangeReport(
            IdentityManifest baseline,
            IdentityManifest current,
            IdentityChangeReport report)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            return JsonUtility.ToJson(ChangeReportDocument.FromReport(baseline, current, report), true);
        }

        [Serializable]
        private sealed class ManifestDocument
        {
            public string storyId;
            public string version;
            public List<string> episodeIds = new List<string>();
            public List<string> edgeIds = new List<string>();
            public List<ExitDocument> exits = new List<ExitDocument>();

            public static ManifestDocument FromManifest(IdentityManifest manifest)
            {
                var result = new ManifestDocument
                {
                    storyId = manifest.StoryId,
                    version = manifest.Version,
                    episodeIds = new List<string>(manifest.EpisodeIds),
                    edgeIds = new List<string>(manifest.EdgeIds)
                };
                for (var i = 0; i < manifest.Exits.Count; i++)
                {
                    result.exits.Add(ExitDocument.FromIdentity(manifest.Exits[i]));
                }

                return result;
            }
        }

        [Serializable]
        private sealed class ChangeReportDocument
        {
            public string previousStoryId;
            public string previousVersion;
            public string storyId;
            public string version;
            public List<string> addedEpisodeIds = new List<string>();
            public List<string> removedEpisodeIds = new List<string>();
            public List<string> addedEdgeIds = new List<string>();
            public List<string> removedEdgeIds = new List<string>();
            public List<ExitDocument> removedExits = new List<ExitDocument>();

            public static ChangeReportDocument FromReport(
                IdentityManifest baseline,
                IdentityManifest current,
                IdentityChangeReport report)
            {
                var result = new ChangeReportDocument
                {
                    previousStoryId = baseline?.StoryId,
                    previousVersion = baseline?.Version,
                    storyId = current.StoryId,
                    version = current.Version,
                    addedEpisodeIds = new List<string>(report.AddedEpisodeIds),
                    removedEpisodeIds = new List<string>(report.RemovedEpisodeIds),
                    addedEdgeIds = new List<string>(report.AddedEdgeIds),
                    removedEdgeIds = new List<string>(report.RemovedEdgeIds)
                };
                for (var i = 0; i < report.RemovedExits.Count; i++)
                {
                    result.removedExits.Add(ExitDocument.FromIdentity(report.RemovedExits[i]));
                }

                return result;
            }
        }

        [Serializable]
        private sealed class ExitDocument
        {
            public string episodeId;
            public string exitId;

            public static ExitDocument FromIdentity(ExitIdentity identity)
            {
                return new ExitDocument
                {
                    episodeId = identity.EpisodeId,
                    exitId = identity.ExitId
                };
            }
        }
    }
}
