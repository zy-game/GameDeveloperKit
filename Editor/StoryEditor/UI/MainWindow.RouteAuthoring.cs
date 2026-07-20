using GameDeveloperKit.StoryEditor.Authoring;
using GameDeveloperKit.StoryEditor.Graph;
using GameDeveloperKit.StoryEditor.Model;
using UnityEditor;

namespace GameDeveloperKit.StoryEditor.UI
{
    public sealed partial class MainWindow
    {
        private void AddRootEpisodeFromRoute()
        {
            if (m_SelectedVolume == null)
            {
                return;
            }

            var metadata = NewEpisodeMetadata();
            var result = new RouteMutation(m_Asset).AddRootEpisode(m_SelectedVolume.VolumeId, metadata);
            ApplyAddEpisodeResult(result);
        }

        private void AddChildEpisodeFromRoute(string episodeId, string exitId)
        {
            if (m_SelectedVolume == null)
            {
                return;
            }

            var metadata = NewEpisodeMetadata();
            var result = new RouteMutation(m_Asset).AddChildEpisode(
                m_SelectedVolume.VolumeId,
                episodeId,
                exitId,
                metadata);
            ApplyAddEpisodeResult(result);
        }

        private void RemoveEpisodeFromRoute(string episodeId)
        {
            if (m_SelectedVolume == null)
            {
                return;
            }

            var result = new RouteMutation(m_Asset).RemoveLeafEpisode(
                m_SelectedVolume.VolumeId,
                episodeId,
                false);
            if (result.Succeeded is false &&
                result.ErrorCode == RouteMutation.PublishedIdentityRemoval)
            {
                if (EditorUtility.DisplayDialog(
                        "删除已发布剧情段",
                        result.Message,
                        "确认删除",
                        "取消") is false)
                {
                    RefreshReport("已取消删除剧情段。");
                    return;
                }

                result = new RouteMutation(m_Asset).RemoveLeafEpisode(
                    m_SelectedVolume.VolumeId,
                    episodeId,
                    true);
            }

            if (result.Succeeded is false)
            {
                RefreshReport(result.Message);
                return;
            }

            m_SelectedRouteNodeId = RouteGraphAdapter.GetVirtualRootNodeId(m_SelectedVolume.VolumeId);
            m_SelectedEpisode = m_SelectedVolume.Episodes.Count > 0 ? m_SelectedVolume.Episodes[0] : null;
            m_SelectionKind = SelectionKind.Story;

            RefreshAll(result.Message);
        }

        private void ApplyAddEpisodeResult(RouteMutationResult result)
        {
            if (result.Succeeded is false)
            {
                RefreshReport(result.Message);
                return;
            }

            m_SelectedRouteNodeId = result.EpisodeId;
            m_SelectedEpisode = FindEpisode(m_SelectedVolume, result.EpisodeId);
            m_SelectionKind = SelectionKind.Episode;
            RefreshAll(result.Message);
        }

        private EpisodeMetadata NewEpisodeMetadata()
        {
            var number = (m_SelectedVolume?.Episodes.Count ?? 0) + 1;
            return new EpisodeMetadata($"剧情段 {number}", string.Empty, null);
        }
    }
}
