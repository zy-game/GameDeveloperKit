using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameDeveloperKit.StoryEditor.Model
{
    [Serializable]
    public sealed class AuthoringRouteLayout
    {
        [SerializeField] private string m_LayoutId;
        [SerializeField] private LayoutOrientation m_Orientation;
        [FormerlySerializedAs("m_ReferenceWidth")]
        [SerializeField] private int m_LegacyReferenceWidth;
        [FormerlySerializedAs("m_ReferenceHeight")]
        [SerializeField] private int m_LegacyReferenceHeight;
        [FormerlySerializedAs("m_UsesNormalizedCoordinates")]
        [SerializeField] private bool m_UsesRelativeCoordinates;
        [SerializeField] private Texture2D m_BackgroundImage;
        [SerializeField] private Texture2D m_EditorGuideImage;
        [SerializeField] private AuthoringPlacement m_RootPlacement;
        [SerializeField] private List<AuthoringEpisodePlacement> m_Episodes = new List<AuthoringEpisodePlacement>();
        [SerializeField] private List<AuthoringRouteEdgePlacement> m_Edges = new List<AuthoringRouteEdgePlacement>();

        public string LayoutId
        {
            get => m_LayoutId;
            set => m_LayoutId = value;
        }

        public LayoutOrientation Orientation
        {
            get => m_Orientation;
            set => m_Orientation = value;
        }

        public Texture2D BackgroundImage
        {
            get => m_BackgroundImage;
            set => m_BackgroundImage = value;
        }

        public Texture2D EditorGuideImage
        {
            get => m_EditorGuideImage;
            set => m_EditorGuideImage = value;
        }

        public AuthoringPlacement RootPlacement
        {
            get => m_RootPlacement;
            set => m_RootPlacement = value;
        }

        public List<AuthoringEpisodePlacement> Episodes
        {
            get
            {
                m_Episodes ??= new List<AuthoringEpisodePlacement>();
                return m_Episodes;
            }
        }

        public List<AuthoringRouteEdgePlacement> Edges
        {
            get
            {
                m_Edges ??= new List<AuthoringRouteEdgePlacement>();
                return m_Edges;
            }
        }

        internal bool UsesRelativeCoordinates
        {
            get => m_UsesRelativeCoordinates;
            set => m_UsesRelativeCoordinates = value;
        }

        internal int LegacyReferenceWidth
        {
            get => m_LegacyReferenceWidth;
            set => m_LegacyReferenceWidth = value;
        }

        internal int LegacyReferenceHeight
        {
            get => m_LegacyReferenceHeight;
            set => m_LegacyReferenceHeight = value;
        }

        internal void EnsureRelativeCoordinates()
        {
            if (m_UsesRelativeCoordinates)
            {
                return;
            }

            if (m_LegacyReferenceWidth > 0 && m_LegacyReferenceHeight > 0)
            {
                Normalize(RootPlacement, m_LegacyReferenceWidth, m_LegacyReferenceHeight);
                for (var i = 0; i < Episodes.Count; i++)
                {
                    Normalize(Episodes[i]?.Position, m_LegacyReferenceWidth, m_LegacyReferenceHeight);
                }

                for (var i = 0; i < Edges.Count; i++)
                {
                    for (var pointIndex = 0; pointIndex < (Edges[i]?.ControlPoints.Count ?? 0); pointIndex++)
                    {
                        Normalize(Edges[i].ControlPoints[pointIndex], m_LegacyReferenceWidth, m_LegacyReferenceHeight);
                    }
                }
            }

            m_LegacyReferenceWidth = 0;
            m_LegacyReferenceHeight = 0;
            m_UsesRelativeCoordinates = true;
        }

        private static void Normalize(AuthoringPlacement placement, float width, float height)
        {
            if (placement != null)
            {
                placement.Position = new Vector2(placement.Position.x / width, placement.Position.y / height);
            }
        }
    }

    [Serializable]
    public sealed class AuthoringPlacement
    {
        [SerializeField] private Vector2 m_Position;

        public Vector2 Position
        {
            get => m_Position;
            set => m_Position = value;
        }
    }

    [Serializable]
    public sealed class AuthoringEpisodePlacement
    {
        [SerializeField] private string m_EpisodeId;
        [SerializeField] private AuthoringPlacement m_Position;

        public string EpisodeId
        {
            get => m_EpisodeId;
            set => m_EpisodeId = value;
        }

        public AuthoringPlacement Position
        {
            get => m_Position;
            set => m_Position = value;
        }
    }

    [Serializable]
    public sealed class AuthoringRouteEdgePlacement
    {
        [SerializeField] private string m_EdgeId;
        [SerializeField] private List<AuthoringPlacement> m_ControlPoints = new List<AuthoringPlacement>();
        [SerializeField] private string m_StyleKey;

        public string EdgeId
        {
            get => m_EdgeId;
            set => m_EdgeId = value;
        }

        public List<AuthoringPlacement> ControlPoints
        {
            get
            {
                m_ControlPoints ??= new List<AuthoringPlacement>();
                return m_ControlPoints;
            }
        }

        public string StyleKey
        {
            get => m_StyleKey;
            set => m_StyleKey = value;
        }
    }

    public readonly struct LayoutMetadata
    {
        public LayoutMetadata(
            LayoutOrientation orientation,
            Texture2D backgroundImage,
            Texture2D editorGuideImage)
        {
            Orientation = orientation;
            BackgroundImage = backgroundImage;
            EditorGuideImage = editorGuideImage;
        }

        public LayoutOrientation Orientation { get; }

        public Texture2D BackgroundImage { get; }

        public Texture2D EditorGuideImage { get; }
    }
}
