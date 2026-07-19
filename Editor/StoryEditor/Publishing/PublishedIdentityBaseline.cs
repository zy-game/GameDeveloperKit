using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Publishing;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Publishing
{
    [Serializable]
    internal sealed class PublishedIdentityBaseline
    {
        [SerializeField] private bool m_HasValue;
        [SerializeField] private string m_StoryId;
        [SerializeField] private string m_Version;
        [SerializeField] private List<string> m_EpisodeIds = new List<string>();
        [SerializeField] private List<string> m_EdgeIds = new List<string>();
        [SerializeField] private List<PublishedExitIdentity> m_Exits = new List<PublishedExitIdentity>();

        public bool HasValue => m_HasValue;

        public bool TryGet(out IdentityManifest manifest, out string error)
        {
            manifest = null;
            error = null;
            if (!m_HasValue)
            {
                return false;
            }

            try
            {
                var exits = new List<ExitIdentity>(m_Exits?.Count ?? 0);
                for (var i = 0; i < (m_Exits?.Count ?? 0); i++)
                {
                    exits.Add(m_Exits[i].ToIdentity());
                }

                manifest = new IdentityManifest(
                    m_StoryId,
                    m_Version,
                    m_EpisodeIds,
                    m_EdgeIds,
                    exits);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public void Set(IdentityManifest manifest)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            m_HasValue = true;
            m_StoryId = manifest.StoryId;
            m_Version = manifest.Version;
            m_EpisodeIds = new List<string>(manifest.EpisodeIds);
            m_EdgeIds = new List<string>(manifest.EdgeIds);
            m_Exits = new List<PublishedExitIdentity>(manifest.Exits.Count);
            for (var i = 0; i < manifest.Exits.Count; i++)
            {
                m_Exits.Add(PublishedExitIdentity.FromIdentity(manifest.Exits[i]));
            }
        }

        public void Clear()
        {
            m_HasValue = false;
            m_StoryId = null;
            m_Version = null;
            m_EpisodeIds = new List<string>();
            m_EdgeIds = new List<string>();
            m_Exits = new List<PublishedExitIdentity>();
        }
    }

    [Serializable]
    internal struct PublishedExitIdentity
    {
        [SerializeField] private string m_EpisodeId;
        [SerializeField] private string m_ExitId;

        public ExitIdentity ToIdentity()
        {
            return new ExitIdentity(m_EpisodeId, m_ExitId);
        }

        public static PublishedExitIdentity FromIdentity(ExitIdentity identity)
        {
            return new PublishedExitIdentity
            {
                m_EpisodeId = identity.EpisodeId,
                m_ExitId = identity.ExitId
            };
        }
    }
}
