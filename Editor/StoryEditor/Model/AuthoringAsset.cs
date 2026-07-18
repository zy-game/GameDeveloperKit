using System;
using System.Collections.Generic;
using GameDeveloperKit.Story;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using GameDeveloperKit.Story.Authoring;

namespace GameDeveloperKit.StoryEditor.Model
{
    /// <summary>
    /// Story Editor authoring asset。
    /// </summary>
    [MovedFrom(true, sourceNamespace: "GameDeveloperKit.StoryEditor", sourceAssembly: "GameDeveloperKit.Editor", sourceClassName: "StoryAuthoringAsset")]
    public sealed class AuthoringAsset : ScriptableObject
    {
        [SerializeField] private string m_StoryId = "new_story";
        [SerializeField] private string m_Version = "1.0.0";
        [SerializeField] private string m_EntryChapterId = "chapter_01";
        [SerializeField] private string m_RuntimeProgramAssetPath;
        [SerializeField] private List<AuthoringChapter> m_Chapters = new List<AuthoringChapter>();
        [SerializeField] private List<AuthoringVolume> m_Volumes = new List<AuthoringVolume>();
        [SerializeField] private GraphLayout m_Layout = new GraphLayout();

        public string StoryId
        {
            get => m_StoryId;
            set => m_StoryId = value;
        }

        public string Version
        {
            get => m_Version;
            set => m_Version = value;
        }

        public string EntryChapterId
        {
            get => m_EntryChapterId;
            set => m_EntryChapterId = value;
        }

        public string RuntimeProgramAssetPath
        {
            get => m_RuntimeProgramAssetPath;
            set => m_RuntimeProgramAssetPath = value;
        }

        public List<AuthoringChapter> Chapters
        {
            get
            {
                if (m_Volumes != null && m_Volumes.Count > 0)
                {
                    var all = new List<AuthoringChapter>();
                    for (var v = 0; v < m_Volumes.Count; v++)
                    {
                        var vol = m_Volumes[v];
                        if (vol?.Chapters != null)
                        {
                            for (var i = 0; i < vol.Chapters.Count; i++)
                            {
                                if (vol.Chapters[i] != null)
                                {
                                    all.Add(vol.Chapters[i]);
                                }
                            }
                        }
                    }

                    return all;
                }

                m_Chapters ??= new List<AuthoringChapter>();
                return m_Chapters;
            }
        }

        public List<AuthoringVolume> Volumes
        {
            get
            {
                m_Volumes ??= new List<AuthoringVolume>();
                return m_Volumes;
            }
        }

        public AuthoringVolume SelectedVolume
        {
            get
            {
                if (Volumes.Count == 0)
                {
                    Volumes.Add(CreateDefaultVolume(NewId()));
                }

                return Volumes[0];
            }
        }

        public GraphLayout Layout
        {
            get
            {
                m_Layout ??= new GraphLayout();
                return m_Layout;
            }
        }

        public void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(m_StoryId))
            {
                m_StoryId = "new_story";
            }

            if (string.IsNullOrWhiteSpace(m_Version))
            {
                m_Version = "1.0.0";
            }

            m_Volumes ??= new List<AuthoringVolume>();
            m_Chapters ??= new List<AuthoringChapter>();

            if (Volumes.Count == 0 && m_Chapters.Count > 0)
            {
                var defaultVolume = CreateDefaultVolume(NewId());
                defaultVolume.Chapters.AddRange(m_Chapters);
                m_Chapters.Clear();
                Volumes.Add(defaultVolume);
            }

            if (Volumes.Count == 0)
            {
                Volumes.Add(CreateDefaultVolume(NewId()));
            }

            var allChapters = new List<AuthoringChapter>();
            for (var v = 0; v < Volumes.Count; v++)
            {
                var volume = Volumes[v];
                if (volume == null)
                {
                    continue;
                }

                allChapters.AddRange(volume.Chapters);
            }

            if (allChapters.Count == 0)
            {
                var defaultChapter = CreateDefaultChapter(NewId());
                Volumes[0].Chapters.Add(defaultChapter);
                allChapters.Add(defaultChapter);
            }

            for (var i = 0; i < allChapters.Count; i++)
            {
                EnsureChapter(allChapters[i], i);
            }

            if (string.IsNullOrWhiteSpace(m_EntryChapterId) || FindChapter(m_EntryChapterId) == null)
            {
                m_EntryChapterId = allChapters[0].ChapterId;
            }
        }

        public AuthoringChapter FindChapter(string chapterId)
        {
            for (var v = 0; v < Volumes.Count; v++)
            {
                var volume = Volumes[v];
                if (volume?.Chapters == null)
                {
                    continue;
                }

                for (var i = 0; i < volume.Chapters.Count; i++)
                {
                    var chapter = volume.Chapters[i];
                    if (chapter != null && string.Equals(chapter.ChapterId, chapterId, StringComparison.Ordinal))
                    {
                        return chapter;
                    }
                }
            }

            return null;
        }

        private static void EnsureChapter(AuthoringChapter chapter, int index)
        {
            if (chapter == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(chapter.ChapterId))
            {
                chapter.ChapterId = NewId();
            }

            if (string.IsNullOrWhiteSpace(chapter.Title))
            {
                chapter.Title = chapter.ChapterId;
            }

            EnsureChapterBoundaryNodes(chapter);
        }

        private static void EnsureChapterBoundaryNodes(AuthoringChapter chapter)
        {
            if (chapter == null)
            {
                return;
            }

            var start = FindFirstNodeByKind(chapter, NodeKind.Start);
            if (start == null)
            {
                var preferredStartId = string.IsNullOrWhiteSpace(chapter.EntryNodeId) ? NewId() : chapter.EntryNodeId;
                start = new AuthoringNode
                {
                    NodeId = MakeUniqueNodeId(chapter, preferredStartId),
                    Title = "开始",
                    NodeKind = NodeKind.Start
                };
                chapter.Nodes.Insert(0, start);
            }

            var end = FindFirstNodeByKind(chapter, NodeKind.End);
            if (end == null)
            {
                end = new AuthoringNode
                {
                    NodeId = MakeUniqueNodeId(chapter, NewId()),
                    Title = "结束",
                    NodeKind = NodeKind.End
                };
                chapter.Nodes.Add(end);
            }

            chapter.EntryNodeId = start.NodeId;
            RemoveDuplicateBoundaryNodes(chapter, NodeKind.Start, start.NodeId);
            RemoveDuplicateBoundaryNodes(chapter, NodeKind.End, end.NodeId);
        }

        private static void RemoveDuplicateBoundaryNodes(AuthoringChapter chapter, NodeKind kind, string keepNodeId)
        {
            if (chapter == null || string.IsNullOrWhiteSpace(keepNodeId))
            {
                return;
            }

            for (var i = chapter.Nodes.Count - 1; i >= 0; i--)
            {
                var node = chapter.Nodes[i];
                if (node == null ||
                    node.NodeKind != kind ||
                    string.Equals(node.NodeId, keepNodeId, StringComparison.Ordinal))
                {
                    continue;
                }

                var nodeId = node.NodeId;
                chapter.Nodes.RemoveAt(i);
                chapter.Edges.RemoveAll(edge =>
                    edge != null &&
                    (string.Equals(edge.FromNodeId, nodeId, StringComparison.Ordinal) ||
                     string.Equals(edge.TargetNodeId, nodeId, StringComparison.Ordinal)));
            }
        }

        private static AuthoringNode FindFirstNodeByKind(AuthoringChapter chapter, NodeKind kind)
        {
            for (var i = 0; i < chapter.Nodes.Count; i++)
            {
                var node = chapter.Nodes[i];
                if (node != null && node.NodeKind == kind)
                {
                    return node;
                }
            }

            return null;
        }

        private static string MakeUniqueNodeId(AuthoringChapter chapter, string preferredId)
        {
            var baseId = string.IsNullOrWhiteSpace(preferredId) ? "node" : preferredId;
            var candidate = baseId;
            var index = 2;
            while (ContainsNode(chapter, candidate))
            {
                candidate = $"{baseId}_{index}";
                index++;
            }

            return candidate;
        }

        private static bool ContainsNode(AuthoringChapter chapter, string nodeId)
        {
            if (chapter == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            for (var i = 0; i < chapter.Nodes.Count; i++)
            {
                var node = chapter.Nodes[i];
                if (node != null && string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static AuthoringChapter CreateDefaultChapter(string chapterId)
        {
            var startId = NewId();
            var chapter = new AuthoringChapter
            {
                ChapterId = chapterId,
                Title = "第一章",
                EntryNodeId = startId
            };
            chapter.Nodes.Add(new AuthoringNode
            {
                NodeId = startId,
                Title = "开始",
                NodeKind = NodeKind.Start
            });
            chapter.Nodes.Add(new AuthoringNode
            {
                NodeId = NewId(),
                Title = "结束",
                NodeKind = NodeKind.End
            });
            return chapter;
        }

        private static AuthoringVolume CreateDefaultVolume(string volumeId)
        {
            return new AuthoringVolume
            {
                VolumeId = volumeId,
                Title = "第一卷"
            };
        }

        private static string NewId()
        {
            return System.Guid.NewGuid().ToString("N");
        }
    }

    /// <summary>
    /// authoring 章节图。
    /// </summary>
    [Serializable]
    public sealed class AuthoringChapter
    {
        [SerializeField] private string m_ChapterId;
        [SerializeField] private string m_Title;
        [SerializeField] private string m_Description;
        [SerializeField] private string m_EntryNodeId;
        [SerializeField] private List<AuthoringNode> m_Nodes = new List<AuthoringNode>();
        [SerializeField] private List<AuthoringEdge> m_Edges = new List<AuthoringEdge>();
        [SerializeField] private Texture2D m_PreviewImage;

        public string ChapterId
        {
            get => m_ChapterId;
            set => m_ChapterId = value;
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
    }

    /// <summary>
    /// authoring 卷。将章节按卷分组，支持卷名编辑。
    /// </summary>
    [Serializable]
    public sealed class AuthoringVolume
    {
        [SerializeField] private string m_VolumeId;
        [SerializeField] private string m_Title;
        [SerializeField] private List<AuthoringChapter> m_Chapters = new List<AuthoringChapter>();

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

        public List<AuthoringChapter> Chapters
        {
            get
            {
                m_Chapters ??= new List<AuthoringChapter>();
                return m_Chapters;
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
        [SerializeField] private string m_TargetChapterId;
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

        public string TargetChapterId
        {
            get => m_TargetChapterId;
            set => m_TargetChapterId = value;
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
    [Serializable]
    public sealed class GraphLayout
    {
        [SerializeField] private List<NodeLayout> m_Nodes = new List<NodeLayout>();

        public List<NodeLayout> Nodes
        {
            get
            {
                m_Nodes ??= new List<NodeLayout>();
                return m_Nodes;
            }
        }
    }

    /// <summary>
    /// 节点布局。
    /// </summary>
    [Serializable]
    public sealed class NodeLayout
    {
        [SerializeField] private string m_GraphId;
        [SerializeField] private string m_NodeId;
        [SerializeField] private Vector2 m_Position;

        public string GraphId
        {
            get => m_GraphId;
            set => m_GraphId = value;
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
