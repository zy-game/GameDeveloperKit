using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Authoring;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;

namespace GameDeveloperKit.StoryEditor.Model
{
    /// <summary>
    /// authoring 章节图。
    /// </summary>
    [MovedFrom(true, sourceNamespace: "GameDeveloperKit.StoryEditor.Model", sourceAssembly: "GameDeveloperKit.Editor", sourceClassName: "AuthoringChapter")]
    [Serializable]
    public sealed class AuthoringEpisode
    {
        [FormerlySerializedAs("m_ChapterId")]
        [SerializeField] private string m_EpisodeId;
        [SerializeField] private string m_Title;
        [SerializeField] private string m_Description;
        [SerializeField] private string m_EntryNodeId;
        [SerializeField] private List<AuthoringNode> m_Nodes = new List<AuthoringNode>();
        [SerializeField] private List<AuthoringEdge> m_Edges = new List<AuthoringEdge>();
        [SerializeField] private Texture2D m_PreviewImage;
        [SerializeField] private EpisodeDetailLayout m_DetailLayout = new EpisodeDetailLayout();

        public string EpisodeId
        {
            get => m_EpisodeId;
            set => m_EpisodeId = value;
        }

        public string Title
        {
            get => m_Title;
            set => m_Title = value;
        }

        public string Description
        {
            get => m_Description;
            set => m_Description = value;
        }

        public string EntryNodeId
        {
            get => m_EntryNodeId;
            set => m_EntryNodeId = value;
        }

        public Texture2D PreviewImage
        {
            get => m_PreviewImage;
            set => m_PreviewImage = value;
        }

        public List<AuthoringNode> Nodes
        {
            get
            {
                m_Nodes ??= new List<AuthoringNode>();
                return m_Nodes;
            }
        }

        public List<AuthoringEdge> Edges
        {
            get
            {
                m_Edges ??= new List<AuthoringEdge>();
                return m_Edges;
            }
        }

        public EpisodeDetailLayout DetailLayout
        {
            get
            {
                m_DetailLayout ??= new EpisodeDetailLayout();
                return m_DetailLayout;
            }
        }
    }

    /// <summary>
    /// authoring 卷。将章节按卷分组，支持卷名编辑。
    /// </summary>
    [Serializable]
    public sealed class AuthoringVolume
    {
        [SerializeField] private string m_VolumeId;
        [SerializeField] private string m_Title;
        [SerializeField] private string m_Description;
        [SerializeField] private Texture2D m_PreviewImage;
        [FormerlySerializedAs("m_Chapters")]
        [SerializeField] private List<AuthoringEpisode> m_Episodes = new List<AuthoringEpisode>();
        [SerializeField] private AuthoringRoute m_Route;
        [SerializeField] private List<AuthoringRouteLayout> m_Layouts = new List<AuthoringRouteLayout>();

        public string VolumeId
        {
            get => m_VolumeId;
            set => m_VolumeId = value;
        }

        public string Title
        {
            get => m_Title;
            set => m_Title = value;
        }

        public string Description
        {
            get => m_Description;
            set => m_Description = value;
        }

        public Texture2D PreviewImage
        {
            get => m_PreviewImage;
            set => m_PreviewImage = value;
        }

        public List<AuthoringEpisode> Episodes
        {
            get
            {
                m_Episodes ??= new List<AuthoringEpisode>();
                return m_Episodes;
            }
        }

        public AuthoringRoute Route
        {
            get => m_Route;
            set => m_Route = value;
        }

        public List<AuthoringRouteLayout> Layouts
        {
            get
            {
                m_Layouts ??= new List<AuthoringRouteLayout>();
                return m_Layouts;
            }
        }
    }

    /// <summary>
    /// authoring 节点。
    /// </summary>
    [Serializable]
    public sealed class AuthoringNode
    {
        [SerializeField] private string m_NodeId;
        [SerializeField] private string m_Title;
        [SerializeField] private NodeKind m_NodeKind;
        [SerializeField] private List<AuthoringParameter> m_Parameters = new List<AuthoringParameter>();
        [SerializeField] private List<AuthoringCondition> m_Conditions = new List<AuthoringCondition>();

        public string NodeId
        {
            get => m_NodeId;
            set => m_NodeId = value;
        }

        public string Title
        {
            get => m_Title;
            set => m_Title = value;
        }

        public NodeKind NodeKind
        {
            get => m_NodeKind;
            set => m_NodeKind = value;
        }

        public List<AuthoringParameter> Parameters
        {
            get
            {
                m_Parameters ??= new List<AuthoringParameter>();
                return m_Parameters;
            }
        }

        public List<AuthoringCondition> Conditions
        {
            get
            {
                m_Conditions ??= new List<AuthoringCondition>();
                return m_Conditions;
            }
        }
    }

    /// <summary>
    /// authoring edge。
    /// </summary>
    [Serializable]
    public sealed class AuthoringEdge
    {
        [SerializeField] private string m_EdgeId;
        [SerializeField] private string m_FromNodeId;
        [SerializeField] private string m_FromPortId;
        [SerializeField] private string m_FromPortLabel;
        [SerializeField] private TransitionTargetKind m_TargetKind;
        [FormerlySerializedAs("m_TargetEpisodeId")]
        [FormerlySerializedAs("m_TargetChapterId")]
        [SerializeField] private string m_LegacyTargetEpisodeId;
        [SerializeField] private string m_TargetNodeId;
        [SerializeField] private List<AuthoringCondition> m_Conditions = new List<AuthoringCondition>();

        public string EdgeId
        {
            get => m_EdgeId;
            set => m_EdgeId = value;
        }

        public string FromNodeId
        {
            get => m_FromNodeId;
            set => m_FromNodeId = value;
        }

        public string FromPortId
        {
            get => m_FromPortId;
            set => m_FromPortId = value;
        }

        public string FromPortLabel
        {
            get => m_FromPortLabel;
            set => m_FromPortLabel = value;
        }

        public TransitionTargetKind TargetKind
        {
            get => m_TargetKind;
            set => m_TargetKind = value;
        }

        internal string LegacyTargetEpisodeId
        {
            get => m_LegacyTargetEpisodeId;
            set => m_LegacyTargetEpisodeId = value;
        }

        public string TargetNodeId
        {
            get => m_TargetNodeId;
            set => m_TargetNodeId = value;
        }

        public List<AuthoringCondition> Conditions
        {
            get
            {
                m_Conditions ??= new List<AuthoringCondition>();
                return m_Conditions;
            }
        }
    }

    /// <summary>
    /// authoring typed parameter。
    /// </summary>
    [Serializable]
    public sealed class AuthoringParameter
    {
        [SerializeField] private string m_Key;
        [SerializeField] private string m_Value;

        public string Key
        {
            get => m_Key;
            set => m_Key = value;
        }

        public string Value
        {
            get => m_Value;
            set => m_Value = value;
        }
    }

    /// <summary>
    /// authoring condition。
    /// </summary>
    [Serializable]
    public sealed class AuthoringCondition
    {
        [SerializeField] private string m_ConditionId;
        [SerializeField] private List<AuthoringParameter> m_Parameters = new List<AuthoringParameter>();

        public string ConditionId
        {
            get => m_ConditionId;
            set => m_ConditionId = value;
        }

        public List<AuthoringParameter> Parameters
        {
            get
            {
                m_Parameters ??= new List<AuthoringParameter>();
                return m_Parameters;
            }
        }
    }

    /// <summary>
    /// story graph layout。
    /// </summary>
    [MovedFrom(true, sourceNamespace: "GameDeveloperKit.StoryEditor.Model", sourceAssembly: "GameDeveloperKit.Editor", sourceClassName: "GraphLayout")]
    [Serializable]
    public sealed class EpisodeDetailLayout
    {
        [SerializeField] private List<EpisodeNodePlacement> m_Nodes = new List<EpisodeNodePlacement>();

        public List<EpisodeNodePlacement> Nodes
        {
            get
            {
                m_Nodes ??= new List<EpisodeNodePlacement>();
                return m_Nodes;
            }
        }
    }

    /// <summary>
    /// 节点布局。
    /// </summary>
    [MovedFrom(true, sourceNamespace: "GameDeveloperKit.StoryEditor.Model", sourceAssembly: "GameDeveloperKit.Editor", sourceClassName: "NodeLayout")]
    [Serializable]
    public sealed class EpisodeNodePlacement
    {
        [FormerlySerializedAs("m_GraphId")]
        [SerializeField] private string m_LegacyGraphId;
        [SerializeField] private string m_NodeId;
        [SerializeField] private Vector2 m_Position;

        internal string LegacyGraphId
        {
            get => m_LegacyGraphId;
            set => m_LegacyGraphId = value;
        }

        public string NodeId
        {
            get => m_NodeId;
            set => m_NodeId = value;
        }

        public Vector2 Position
        {
            get => m_Position;
            set => m_Position = value;
        }
    }
}
