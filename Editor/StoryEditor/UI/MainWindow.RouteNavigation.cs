using System;
using System.Linq;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.StoryEditor.Compiler;
using GameDeveloperKit.StoryEditor.Graph;
using GameDeveloperKit.StoryEditor.Authoring;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Validation;
using UnityEngine.UIElements;

namespace GameDeveloperKit.StoryEditor.UI
{
    public sealed partial class MainWindow
    {
        private enum EditorMode
        {
            Route,
            EpisodeDetail
        }

        private EditorMode m_EditorMode = EditorMode.Route;
        private AuthoringVolume m_SelectedVolume;
        private string m_SelectedRouteNodeId;
        private RouteGraphAdapter m_RouteGraphAdapter;
        private Program m_RouteProgram;
        private ValidationReport m_RouteReport = new ValidationReport();
        private VisualElement m_Breadcrumb;

        private void InitializeRouteNavigation()
        {
            m_RouteGraphAdapter = new RouteGraphAdapter(new RouteGraphActions
            {
                SelectedNode = SelectRouteNode,
                ActivatedNode = ActivateRouteNode,
                AddRootEpisode = AddRootEpisodeFromRoute,
                AddChildEpisode = AddChildEpisodeFromRoute,
                RemoveEpisode = RemoveEpisodeFromRoute,
                SelectedWire = SelectRouteWire,
                MoveNodes = MoveRouteNodes,
                UpdateEdgePath = UpdateRouteEdgePath
            });
        }

        private void SelectDefaultRoute()
        {
            m_SelectedVolume = FindVolume(m_SelectedChapter) ?? FirstVolume();
            m_EditorMode = EditorMode.Route;
            m_SelectedRouteNodeId = RouteGraphAdapter.GetVirtualRootNodeId(m_SelectedVolume?.VolumeId);
            m_SelectedRouteEdgeId = null;
            EnsureRouteLayoutSelection();
        }

        private void EnsureRouteSelection()
        {
            if (m_SelectedVolume == null || m_Asset.Volumes.Contains(m_SelectedVolume) is false)
            {
                m_SelectedVolume = FindVolume(m_SelectedChapter) ?? FirstVolume();
            }

            if (m_EditorMode == EditorMode.EpisodeDetail)
            {
                var detailVolume = FindVolume(m_SelectedChapter);
                if (detailVolume != null)
                {
                    m_SelectedVolume = detailVolume;
                    m_SelectedRouteNodeId = m_SelectedChapter.ChapterId;
                }

                return;
            }

            EnsureRouteLayoutSelection();
            EnsureRouteEdgeSelection();

            if (IsEpisodeInVolume(m_SelectedRouteNodeId, m_SelectedVolume) is false &&
                string.Equals(
                    m_SelectedRouteNodeId,
                    RouteGraphAdapter.GetVirtualRootNodeId(m_SelectedVolume?.VolumeId),
                    StringComparison.Ordinal) is false)
            {
                m_SelectedRouteNodeId = RouteGraphAdapter.GetVirtualRootNodeId(m_SelectedVolume?.VolumeId);
            }
        }

        private void RefreshNavigationCanvas()
        {
            if (m_EditorMode == EditorMode.EpisodeDetail)
            {
                m_Canvas.RemoveFromClassList("story-editor__route-canvas");
                m_Canvas.SetAdapter(m_GraphAdapter);
                return;
            }

            m_RouteProgram = ProgramCompiler.Compile(m_Asset, out m_RouteReport);
            var compiledVolume = FindCompiledVolume(m_SelectedVolume?.VolumeId);
            m_RouteGraphAdapter.SetRoute(
                m_SelectedVolume,
                compiledVolume,
                m_RouteReport,
                m_SelectedRouteNodeId,
                SelectedRouteLayout(),
                m_SelectedRouteEdgeId);
            m_Canvas.AddToClassList("story-editor__route-canvas");
            m_Canvas.SetAdapter(m_RouteGraphAdapter);
            var portDots = m_Canvas.Query<VisualElement>(className: "editor-node-graph-node__port-dot").ToList();
            for (var i = 0; i < portDots.Count; i++)
            {
                portDots[i].pickingMode = PickingMode.Ignore;
            }
        }

        private void SelectVolume(AuthoringVolume volume)
        {
            if (volume == null || m_Asset.Volumes.Contains(volume) is false)
            {
                return;
            }

            m_SelectedVolume = volume;
            m_EditorMode = EditorMode.Route;
            m_SelectedRouteNodeId = RouteGraphAdapter.GetVirtualRootNodeId(volume.VolumeId);
            m_SelectedRouteEdgeId = null;
            m_SelectedRouteLayoutId = null;
            EnsureRouteLayoutSelection();
            m_SelectedChapter = volume.Chapters.Count > 0 ? volume.Chapters[0] : null;
            ClearDetailSelection();
            m_SelectionKind = SelectionKind.Story;
            RefreshAll();
        }

        private void SelectRouteNode(string nodeId)
        {
            if (m_EditorMode != EditorMode.Route || m_RouteGraphAdapter == null)
            {
                return;
            }

            m_SelectedRouteNodeId = nodeId;
            m_SelectedRouteEdgeId = null;
            if (m_RouteGraphAdapter.ContainsEpisode(nodeId))
            {
                m_SelectedChapter = FindEpisode(m_SelectedVolume, nodeId);
                m_SelectionKind = SelectionKind.Chapter;
            }
            else
            {
                m_SelectionKind = SelectionKind.Story;
            }

            ClearDetailSelection();
            RefreshNavigationChrome();
        }

        private void ActivateRouteNode(string nodeId)
        {
            var episode = FindEpisode(m_SelectedVolume, nodeId);
            if (episode != null)
            {
                EnterEpisodeDetail(episode);
            }
        }

        private void EnterEpisodeDetail(AuthoringChapter episode)
        {
            var volume = FindVolume(episode);
            if (episode == null || volume == null)
            {
                return;
            }

            m_SelectedVolume = volume;
            m_SelectedChapter = episode;
            m_SelectedRouteNodeId = episode.ChapterId;
            m_EditorMode = EditorMode.EpisodeDetail;
            m_SelectedRouteEdgeId = null;
            ClearDetailSelection();
            m_SelectionKind = SelectionKind.Chapter;
            RefreshAll();
        }

        private void ReturnToRouteMode()
        {
            if (m_EditorMode == EditorMode.Route)
            {
                return;
            }

            m_EditorMode = EditorMode.Route;
            if (m_SelectedChapter != null && FindVolume(m_SelectedChapter) == m_SelectedVolume)
            {
                m_SelectedRouteNodeId = m_SelectedChapter.ChapterId;
            }
            else
            {
                m_SelectedRouteNodeId = RouteGraphAdapter.GetVirtualRootNodeId(m_SelectedVolume?.VolumeId);
            }

            ClearDetailSelection();
            m_SelectionKind = m_SelectedChapter == null ? SelectionKind.Story : SelectionKind.Chapter;
            RefreshAll();
        }

        private void ClearDetailSelection()
        {
            m_SelectedNode = null;
            m_SelectedEdge = null;
            m_SelectedNodeIds.Clear();
        }

        private VisualElement CreateNavigationHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("story-editor__navigation-header");
            m_Breadcrumb = new VisualElement();
            m_Breadcrumb.AddToClassList("story-editor__breadcrumb");
            header.Add(m_Breadcrumb);
            header.Add(CreateRouteLayoutToolbar());
            return header;
        }

        private void RefreshNavigationChrome()
        {
            RefreshBreadcrumb();
            RefreshRouteLayoutToolbar();
            RefreshRouteInspector();
        }

        private void RefreshBreadcrumb()
        {
            if (m_Breadcrumb == null)
            {
                return;
            }

            m_Breadcrumb.Clear();
            var volumeTitle = SafeText(m_SelectedVolume?.Title, m_SelectedVolume?.VolumeId);
            var volumeButton = new Button(ReturnToRouteMode)
            {
                name = "story-route-volume-breadcrumb",
                text = volumeTitle,
                tooltip = "返回当前卷路线。"
            };
            volumeButton.AddToClassList("story-editor__breadcrumb-item");
            m_Breadcrumb.Add(volumeButton);

            var episode = SelectedRouteEpisode();
            if (episode == null)
            {
                return;
            }

            var separator = new Label(">");
            separator.AddToClassList("story-editor__breadcrumb-separator");
            m_Breadcrumb.Add(separator);

            var episodeLabel = new Label(SafeText(episode.Title, episode.ChapterId));
            episodeLabel.AddToClassList("story-editor__breadcrumb-current");
            m_Breadcrumb.Add(episodeLabel);
        }

        private AuthoringChapter SelectedRouteEpisode()
        {
            if (m_EditorMode == EditorMode.EpisodeDetail)
            {
                return m_SelectedChapter;
            }

            return FindEpisode(m_SelectedVolume, m_SelectedRouteNodeId);
        }

        private AuthoringVolume FindVolume(AuthoringChapter episode)
        {
            if (episode == null || m_Asset?.Volumes == null)
            {
                return null;
            }

            for (var i = 0; i < m_Asset.Volumes.Count; i++)
            {
                var volume = m_Asset.Volumes[i];
                if (volume?.Chapters != null && volume.Chapters.Contains(episode))
                {
                    return volume;
                }
            }

            return null;
        }

        private AuthoringVolume FirstVolume()
        {
            if (m_Asset?.Volumes == null)
            {
                return null;
            }

            for (var i = 0; i < m_Asset.Volumes.Count; i++)
            {
                if (m_Asset.Volumes[i] != null)
                {
                    return m_Asset.Volumes[i];
                }
            }

            return null;
        }

        private Volume FindCompiledVolume(string volumeId)
        {
            if (m_RouteProgram == null || string.IsNullOrWhiteSpace(volumeId))
            {
                return null;
            }

            for (var i = 0; i < m_RouteProgram.Volumes.Count; i++)
            {
                var volume = m_RouteProgram.Volumes[i];
                if (volume != null && string.Equals(volume.VolumeId, volumeId, StringComparison.Ordinal))
                {
                    return volume;
                }
            }

            return null;
        }

        private static AuthoringChapter FindEpisode(AuthoringVolume volume, string episodeId)
        {
            if (volume?.Chapters == null || string.IsNullOrWhiteSpace(episodeId))
            {
                return null;
            }

            for (var i = 0; i < volume.Chapters.Count; i++)
            {
                var episode = volume.Chapters[i];
                if (episode != null && string.Equals(episode.ChapterId, episodeId, StringComparison.Ordinal))
                {
                    return episode;
                }
            }

            return null;
        }

        private static bool IsEpisodeInVolume(string episodeId, AuthoringVolume volume)
        {
            return FindEpisode(volume, episodeId) != null;
        }
    }
}
