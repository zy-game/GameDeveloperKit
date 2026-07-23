using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Publishing;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Model
{
    public sealed class AuthoringVolumeAsset : ScriptableObject
    {
        [SerializeField] private AuthoringVolume m_Volume = new AuthoringVolume();

        public AuthoringVolume Volume
        {
            get
            {
                m_Volume ??= new AuthoringVolume();
                return m_Volume;
            }
        }

        internal void SetVolume(AuthoringVolume volume)
        {
            m_Volume = volume ?? new AuthoringVolume();
        }

        internal static AuthoringVolume CreateDefaultVolume(string volumeId, string title)
        {
            var episodeId = IdentityId.New();
            var startId = IdentityId.New();
            var episode = new AuthoringEpisode
            {
                EpisodeId = episodeId,
                Title = "第一章",
                EntryNodeId = startId
            };
            episode.Nodes.Add(new AuthoringNode
            {
                NodeId = startId,
                Title = "开始",
                NodeKind = NodeKind.Start
            });

            var volume = new AuthoringVolume
            {
                VolumeId = volumeId,
                Title = title,
                Route = new AuthoringRoute()
            };
            volume.Episodes.Add(episode);
            volume.Route.Edges.Add(new AuthoringRouteEdge
            {
                EdgeId = IdentityId.RootEdge(episodeId),
                SourceKind = RouteEdgeSourceKind.Root,
                ToEpisodeId = episodeId
            });
            return volume;
        }

        internal static AuthoringVolumeAsset CreateDefault(string volumeId, string title)
        {
            var asset = CreateInstance<AuthoringVolumeAsset>();
            asset.SetVolume(CreateDefaultVolume(volumeId, title));
            return asset;
        }
    }
}
