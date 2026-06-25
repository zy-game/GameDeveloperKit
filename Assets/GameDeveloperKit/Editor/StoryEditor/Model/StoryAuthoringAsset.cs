using System;
using System.Collections.Generic;
using GameDeveloperKit.Story;
using UnityEngine;

namespace GameDeveloperKit.StoryEditor
{
    /// <summary>
    /// Story Editor authoring asset。
    /// </summary>
    public sealed class StoryAuthoringAsset : ScriptableObject
    {
        [SerializeField] private string m_StoryId = "new_story";
        [SerializeField] private string m_Version = "1.0.0";
        [SerializeField] private string m_EntryChapterId = "chapter_01";
        [SerializeField] private string m_RuntimeProgramAssetPath;
        [SerializeField] private List<StoryAuthoringChapter> m_Chapters = new List<StoryAuthoringChapter>();
        [SerializeField] private List<StoryAuthoringVolume> m_Volumes = new List<StoryAuthoringVolume>();
        [SerializeField] private StoryGraphLayout m_Layout = new StoryGraphLayout();

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

        public List<StoryAuthoringChapter> Chapters
        {
            get
            {
                if (m_Volumes != null && m_Volumes.Count > 0)
                {
                    var all = new List<StoryAuthoringChapter>();
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

                m_Chapters ??= new List<StoryAuthoringChapter>();
                return m_Chapters;
            }
        }

        public List<StoryAuthoringVolume> Volumes
        {
            get
            {
                m_Volumes ??= new List<StoryAuthoringVolume>();
                return m_Volumes;
            }
        }

        public StoryAuthoringVolume SelectedVolume
        {
            get
            {
                if (Volumes.Count == 0)
                {
                    Volumes.Add(CreateDefaultVolume("volume_01"));
                }

                return Volumes[0];
            }
        }

        public StoryGraphLayout Layout
        {
            get
            {
                m_Layout ??= new StoryGraphLayout();
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

            m_Volumes ??= new List<StoryAuthoringVolume>();
            m_Chapters ??= new List<StoryAuthoringChapter>();

            if (Volumes.Count == 0 && m_Chapters.Count > 0)
            {
                var defaultVolume = CreateDefaultVolume("volume_01");
                defaultVolume.Chapters.AddRange(m_Chapters);
                m_Chapters.Clear();
                Volumes.Add(defaultVolume);
            }

            if (Volumes.Count == 0)
            {
                Volumes.Add(CreateDefaultVolume("volume_01"));
            }

            var allChapters = new List<StoryAuthoringChapter>();
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
                var defaultChapter = CreateDefaultChapter("chapter_01");
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

        public StoryAuthoringChapter FindChapter(string chapterId)
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

        private static void EnsureChapter(StoryAuthoringChapter chapter, int index)
        {
            if (chapter == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(chapter.ChapterId))
            {
                chapter.ChapterId = $"chapter_{index + 1:00}";
            }

            if (string.IsNullOrWhiteSpace(chapter.Title))
            {
                chapter.Title = chapter.ChapterId;
            }

            EnsureChapterBoundaryNodes(chapter);
        }

        private static void EnsureChapterBoundaryNodes(StoryAuthoringChapter chapter)
        {
            if (chapter == null)
            {
                return;
            }

            var start = FindFirstNodeByKind(chapter, NodeKind.Start);
            if (start == null)
            {
                start = new StoryAuthoringNode
                {
                    NodeId = MakeUniqueNodeId(
                        chapter,
                        string.IsNullOrWhiteSpace(chapter.EntryNodeId) ? $"{chapter.ChapterId}_entry" : chapter.EntryNodeId),
                    Title = "开始",
                    NodeKind = NodeKind.Start
                };
                chapter.Nodes.Insert(0, start);
            }

            var end = FindFirstNodeByKind(chapter, NodeKind.End);
            if (end == null)
            {
                end = new StoryAuthoringNode
                {
                    NodeId = MakeUniqueNodeId(chapter, $"{chapter.ChapterId}_end"),
                    Title = "结束",
                    NodeKind = NodeKind.End
                };
                chapter.Nodes.Add(end);
            }

            chapter.EntryNodeId = start.NodeId;
            RemoveDuplicateBoundaryNodes(chapter, NodeKind.Start, start.NodeId);
            RemoveDuplicateBoundaryNodes(chapter, NodeKind.End, end.NodeId);
        }

        private static void RemoveDuplicateBoundaryNodes(StoryAuthoringChapter chapter, NodeKind kind, string keepNodeId)
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

        private static StoryAuthoringNode FindFirstNodeByKind(StoryAuthoringChapter chapter, NodeKind kind)
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

        private static string MakeUniqueNodeId(StoryAuthoringChapter chapter, string preferredId)
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

        private static bool ContainsNode(StoryAuthoringChapter chapter, string nodeId)
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

        private static StoryAuthoringChapter CreateDefaultChapter(string chapterId)
        {
            var chapter = new StoryAuthoringChapter
            {
                ChapterId = chapterId,
                Title = "第一章",
                EntryNodeId = $"{chapterId}_entry"
            };
            chapter.Nodes.Add(new StoryAuthoringNode
            {
                NodeId = chapter.EntryNodeId,
                Title = "开始",
                NodeKind = NodeKind.Start
            });
            chapter.Nodes.Add(new StoryAuthoringNode
            {
                NodeId = $"{chapterId}_end",
                Title = "结束",
                NodeKind = NodeKind.End
            });
            return chapter;
        }

        private static StoryAuthoringVolume CreateDefaultVolume(string volumeId)
        {
            return new StoryAuthoringVolume
            {
                VolumeId = volumeId,
                Title = "第一卷"
            };
        }
    }

    /// <summary>
    /// authoring 章节图。
    /// </summary>
    [Serializable]
    public sealed class StoryAuthoringChapter
    {
        [SerializeField] private string m_ChapterId;
        [SerializeField] private string m_Title;
        [SerializeField] private string m_EntryNodeId;
        [SerializeField] private List<StoryAuthoringNode> m_Nodes = new List<StoryAuthoringNode>();
        [SerializeField] private List<StoryAuthoringEdge> m_Edges = new List<StoryAuthoringEdge>();

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

        public string EntryNodeId
        {
            get => m_EntryNodeId;
            set => m_EntryNodeId = value;
        }

        public List<StoryAuthoringNode> Nodes
        {
            get
            {
                m_Nodes ??= new List<StoryAuthoringNode>();
                return m_Nodes;
            }
        }

        public List<StoryAuthoringEdge> Edges
        {
            get
            {
                m_Edges ??= new List<StoryAuthoringEdge>();
                return m_Edges;
            }
        }
    }

    /// <summary>
    /// authoring 卷。将章节按卷分组，支持卷名编辑。
    /// </summary>
    [Serializable]
    public sealed class StoryAuthoringVolume
    {
        [SerializeField] private string m_VolumeId;
        [SerializeField] private string m_Title;
        [SerializeField] private List<StoryAuthoringChapter> m_Chapters = new List<StoryAuthoringChapter>();

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

        public List<StoryAuthoringChapter> Chapters
        {
            get
            {
                m_Chapters ??= new List<StoryAuthoringChapter>();
                return m_Chapters;
            }
        }
    }

    /// <summary>
    /// authoring 节点。
    /// </summary>
    [Serializable]
    public sealed class StoryAuthoringNode
    {
        [SerializeField] private string m_NodeId;
        [SerializeField] private string m_Title;
        [SerializeField] private NodeKind m_NodeKind;
        [SerializeField] private List<StoryAuthoringParameter> m_Parameters = new List<StoryAuthoringParameter>();
        [SerializeField] private List<StoryAuthoringCondition> m_Conditions = new List<StoryAuthoringCondition>();

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

        public List<StoryAuthoringParameter> Parameters
        {
            get
            {
                m_Parameters ??= new List<StoryAuthoringParameter>();
                return m_Parameters;
            }
        }

        public List<StoryAuthoringCondition> Conditions
        {
            get
            {
                m_Conditions ??= new List<StoryAuthoringCondition>();
                return m_Conditions;
            }
        }
    }

    /// <summary>
    /// authoring edge。
    /// </summary>
    [Serializable]
    public sealed class StoryAuthoringEdge
    {
        [SerializeField] private string m_EdgeId;
        [SerializeField] private string m_FromNodeId;
        [SerializeField] private string m_FromPortId;
        [SerializeField] private string m_FromPortLabel;
        [SerializeField] private TransitionTargetKind m_TargetKind;
        [SerializeField] private string m_TargetChapterId;
        [SerializeField] private string m_TargetNodeId;
        [SerializeField] private List<StoryAuthoringCondition> m_Conditions = new List<StoryAuthoringCondition>();

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

        public List<StoryAuthoringCondition> Conditions
        {
            get
            {
                m_Conditions ??= new List<StoryAuthoringCondition>();
                return m_Conditions;
            }
        }
    }

    /// <summary>
    /// authoring typed parameter。
    /// </summary>
    [Serializable]
    public sealed class StoryAuthoringParameter
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
    public sealed class StoryAuthoringCondition
    {
        [SerializeField] private string m_ConditionId;
        [SerializeField] private List<StoryAuthoringParameter> m_Parameters = new List<StoryAuthoringParameter>();

        public string ConditionId
        {
            get => m_ConditionId;
            set => m_ConditionId = value;
        }

        public List<StoryAuthoringParameter> Parameters
        {
            get
            {
                m_Parameters ??= new List<StoryAuthoringParameter>();
                return m_Parameters;
            }
        }
    }

    /// <summary>
    /// story graph layout。
    /// </summary>
    [Serializable]
    public sealed class StoryGraphLayout
    {
        [SerializeField] private List<StoryNodeLayout> m_Nodes = new List<StoryNodeLayout>();

        public List<StoryNodeLayout> Nodes
        {
            get
            {
                m_Nodes ??= new List<StoryNodeLayout>();
                return m_Nodes;
            }
        }
    }

    /// <summary>
    /// 节点布局。
    /// </summary>
    [Serializable]
    public sealed class StoryNodeLayout
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
