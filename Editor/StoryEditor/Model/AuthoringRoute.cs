using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Model
{
    [Serializable]
    public sealed class AuthoringRoute
    {
        [SerializeField] private List<AuthoringRouteEdge> m_Edges = new List<AuthoringRouteEdge>();

        public List<AuthoringRouteEdge> Edges
        {
            get
            {
                m_Edges ??= new List<AuthoringRouteEdge>();
                return m_Edges;
            }
        }
    }

    [Serializable]
    public sealed class AuthoringRouteEdge
    {
        [SerializeField] private string m_EdgeId;
        [SerializeField] private RouteEdgeSourceKind m_SourceKind;
        [SerializeField] private string m_FromEpisodeId;
        [SerializeField] private string m_FromExitId;
        [SerializeField] private string m_ToEpisodeId;

        public string EdgeId
        {
            get => m_EdgeId;
            set => m_EdgeId = value;
        }

        public RouteEdgeSourceKind SourceKind
        {
            get => m_SourceKind;
            set => m_SourceKind = value;
        }

        public string FromEpisodeId
        {
            get => m_FromEpisodeId;
            set => m_FromEpisodeId = value;
        }

        public string FromExitId
        {
            get => m_FromExitId;
            set => m_FromExitId = value;
        }

        public string ToEpisodeId
        {
            get => m_ToEpisodeId;
            set => m_ToEpisodeId = value;
        }
    }

    public readonly struct EpisodeMetadata
    {
        public EpisodeMetadata(string title, string description, Texture2D previewImage)
        {
            Title = title;
            Description = description;
            PreviewImage = previewImage;
        }

        public string Title { get; }

        public string Description { get; }

        public Texture2D PreviewImage { get; }
    }

    public readonly struct VolumeMetadata
    {
        public VolumeMetadata(string title, string description, Texture2D previewImage)
        {
            Title = title;
            Description = description;
            PreviewImage = previewImage;
        }

        public string Title { get; }

        public string Description { get; }

        public Texture2D PreviewImage { get; }
    }
}
