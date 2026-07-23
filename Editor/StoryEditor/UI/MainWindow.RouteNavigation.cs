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
            Overview,
            Route,
            EpisodeDetail
        }

        private EditorMode m_EditorMode = EditorMode.Overview;
        private AuthoringVolumeAsset m_SelectedVolumeAsset;
        private AuthoringVolume m_SelectedVolume;
        private string m_SelectedRouteNodeId;
        private RouteGraphAdapter m_RouteGraphAdapter;
        private Volume m_RouteVolume;
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
                CanConnect = CanConnectRoute,
                Connect = ConnectRoute,
                Disconnect = DisconnectRoute,
                MoveNodes = MoveRouteNodes,
                UpdateEdgePath = UpdateRouteEdgePath
            });
        }

        private void SelectDefaultRoute()
        {
            m_SelectedVolumeAsset = null;
            m_SelectedVolume = null;
            m_SelectedEpisode = null;
            m_EditorMode = EditorMode.Overview;
            m_SelectedRouteNodeId = null;
            m_SelectedRouteEdgeId = null;
            m_SelectedRouteLayoutId = null;
        }

        private void EnsureRouteSelection()
        {
            if (m_EditorMode == EditorMode.Overview)
            {
                return;
            }

            if (IsReferencedVolumeAsset(m_SelectedVolumeAsset) is false ||
                ReferenceEquals(m_SelectedVolumeAsset.Volume, m_SelectedVolume) is false)
            {
                SelectDefaultRoute();
                return;
            }

            if (m_EditorMode == EditorMode.EpisodeDetail)
            {
                if (m_SelectedEpisode != null && m_SelectedVolume.Episodes.Contains(m_SelectedEpisode))
                {
                    m_SelectedRouteNodeId = m_SelectedEpisode.EpisodeId;
                }
                else
                {
                    m_EditorMode = EditorMode.Route;
                    m_SelectedEpisode = m_SelectedVolume.Episodes.FirstOrDefault();
                    m_SelectedRouteNodeId = RouteGraphAdapter.GetVirtualRootNodeId(m_SelectedVolume.VolumeId);
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
            if (m_EditorMode == EditorMode.Overview)
            {
                return;
            }

            if (m_EditorMode == EditorMode.EpisodeDetail)
            {
                m_Canvas.RemoveFromClassList("story-editor__route-canvas");
                m_Canvas.SetAdapter(m_GraphAdapter);
                return;
            }

            m_RouteVolume = ProgramCompiler.CompileVolume(m_Asset, m_SelectedVolumeAsset, out m_RouteReport);
            m_RouteGraphAdapter.SetRoute(
                m_SelectedVolume,
                m_RouteVolume,
                m_RouteReport,
                m_SelectedRouteNodeId,
                SelectedRouteLayout(),
                m_SelectedRouteEdgeId);
            m_Canvas.AddToClassList("story-editor__route-canvas");
            m_Canvas.SetAdapter(m_RouteGraphAdapter);
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
                m_SelectedEpisode = FindEpisode(m_SelectedVolume, nodeId);
                m_SelectionKind = SelectionKind.Episode;
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

        private void EnterEpisodeDetail(AuthoringEpisode episode)
        {
            if (episode == null || m_SelectedVolume?.Episodes.Contains(episode) is false)
            {
                return;
            }

            m_SelectedEpisode = episode;
            m_SelectedRouteNodeId = episode.EpisodeId;
            m_EditorMode = EditorMode.EpisodeDetail;
            m_SelectedRouteEdgeId = null;
            ClearDetailSelection();
            m_SelectionKind = SelectionKind.Episode;
            RefreshAll();
        }

        private void ReturnToRouteMode()
        {
            if (m_EditorMode == EditorMode.Route || m_EditorMode == EditorMode.Overview)
            {
                return;
            }

            m_EditorMode = EditorMode.Route;
            if (m_SelectedEpisode != null && m_SelectedVolume?.Episodes.Contains(m_SelectedEpisode) == true)
            {
                m_SelectedRouteNodeId = m_SelectedEpisode.EpisodeId;
            }
            else
            {
                m_SelectedRouteNodeId = RouteGraphAdapter.GetVirtualRootNodeId(m_SelectedVolume?.VolumeId);
            }

            ClearDetailSelection();
            m_SelectionKind = m_SelectedEpisode == null ? SelectionKind.Story : SelectionKind.Episode;
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
            var overviewButton = new Button(ReturnToOverview)
            {
                name = "story-overview-breadcrumb",
                text = "剧情总览",
                tooltip = "返回剧情总览。"
            };
            overviewButton.AddToClassList("story-editor__breadcrumb-item");
            m_Breadcrumb.Add(overviewButton);

            var overviewSeparator = new Label(">");
            overviewSeparator.AddToClassList("story-editor__breadcrumb-separator");
            m_Breadcrumb.Add(overviewSeparator);
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

            var episodeLabel = new Label(SafeText(episode.Title, episode.EpisodeId));
            episodeLabel.AddToClassList("story-editor__breadcrumb-current");
            m_Breadcrumb.Add(episodeLabel);
        }

        private AuthoringEpisode SelectedRouteEpisode()
        {
            if (m_EditorMode == EditorMode.EpisodeDetail)
            {
                return m_SelectedEpisode;
            }

            return FindEpisode(m_SelectedVolume, m_SelectedRouteNodeId);
        }

        private AuthoringVolume FindVolume(AuthoringEpisode episode)
        {
            if (episode == null || m_SelectedVolume?.Episodes == null)
            {
                return null;
            }

            return m_SelectedVolume.Episodes.Contains(episode) ? m_SelectedVolume : null;
        }

        private bool IsReferencedVolumeAsset(AuthoringVolumeAsset volumeAsset)
        {
            if (volumeAsset == null || m_Asset == null)
            {
                return false;
            }

            for (var i = 0; i < m_Asset.VolumeAssets.Count; i++)
            {
                if (m_Asset.VolumeAssets[i] == volumeAsset)
                {
                    return true;
                }
            }

            return false;
        }

        private static AuthoringEpisode FindEpisode(AuthoringVolume volume, string episodeId)
        {
            if (volume?.Episodes == null || string.IsNullOrWhiteSpace(episodeId))
            {
                return null;
            }

            for (var i = 0; i < volume.Episodes.Count; i++)
            {
                var episode = volume.Episodes[i];
                if (episode != null && string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal))
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
