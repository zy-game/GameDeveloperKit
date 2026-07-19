using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Model;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor.Model
{
    [Serializable]
    public sealed class AuthoringRouteLayout
    {
        [SerializeField] private string m_LayoutId;
        [SerializeField] private LayoutOrientation m_Orientation;
        [SerializeField] private int m_ReferenceWidth;
        [SerializeField] private int m_ReferenceHeight;
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

        public int ReferenceWidth
        {
            get => m_ReferenceWidth;
            set => m_ReferenceWidth = value;
        }

        public int ReferenceHeight
        {
            get => m_ReferenceHeight;
            set => m_ReferenceHeight = value;
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
            int referenceWidth,
            int referenceHeight,
            Texture2D backgroundImage,
            Texture2D editorGuideImage)
        {
            Orientation = orientation;
            ReferenceWidth = referenceWidth;
            ReferenceHeight = referenceHeight;
            BackgroundImage = backgroundImage;
            EditorGuideImage = editorGuideImage;
        }

        public LayoutOrientation Orientation { get; }

        public int ReferenceWidth { get; }

        public int ReferenceHeight { get; }

        public Texture2D BackgroundImage { get; }

        public Texture2D EditorGuideImage { get; }
    }
}
