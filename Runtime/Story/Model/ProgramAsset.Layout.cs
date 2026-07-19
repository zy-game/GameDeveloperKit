using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Story.Model
{
    public sealed partial class ProgramAsset
    {
        private sealed partial class VolumeData
        {
            [SerializeField] private List<RouteLayoutData> m_Layouts = new List<RouteLayoutData>();
        }

        [Serializable]
        private sealed class RouteLayoutData
        {
            [SerializeField] private string m_LayoutId;
            [SerializeField] private LayoutOrientation m_Orientation;
            [SerializeField] private int m_ReferenceWidth;
            [SerializeField] private int m_ReferenceHeight;
            [SerializeField] private string m_BackgroundImagePath;
            [SerializeField] private PlacementData m_RootPlacement = new PlacementData();
            [SerializeField] private List<EpisodePlacementData> m_Episodes = new List<EpisodePlacementData>();
            [SerializeField] private List<RouteEdgePlacementData> m_Edges = new List<RouteEdgePlacementData>();

            public static List<RouteLayoutData> FromList(IReadOnlyList<RouteLayout> layouts)
            {
                var result = new List<RouteLayoutData>();
                for (var i = 0; i < (layouts?.Count ?? 0); i++)
                {
                    var layout = layouts[i];
                    if (layout == null)
                    {
                        continue;
                    }

                    result.Add(new RouteLayoutData
                    {
                        m_LayoutId = layout.LayoutId,
                        m_Orientation = layout.Orientation,
                        m_ReferenceWidth = layout.ReferenceWidth,
                        m_ReferenceHeight = layout.ReferenceHeight,
                        m_BackgroundImagePath = layout.BackgroundImagePath,
                        m_RootPlacement = PlacementData.FromPlacement(layout.RootPlacement),
                        m_Episodes = EpisodePlacementData.FromList(layout.Episodes),
                        m_Edges = RouteEdgePlacementData.FromList(layout.Edges)
                    });
                }

                return result;
            }

            public static List<RouteLayout> ToList(IReadOnlyList<RouteLayoutData> layouts)
            {
                var result = new List<RouteLayout>();
                for (var i = 0; i < (layouts?.Count ?? 0); i++)
                {
                    var layout = layouts[i];
                    if (layout == null)
                    {
                        continue;
                    }

                    result.Add(new RouteLayout(
                        layout.m_LayoutId,
                        layout.m_Orientation,
                        layout.m_ReferenceWidth,
                        layout.m_ReferenceHeight,
                        layout.m_BackgroundImagePath,
                        layout.m_RootPlacement?.ToPlacement() ?? default,
                        EpisodePlacementData.ToList(layout.m_Episodes),
                        RouteEdgePlacementData.ToList(layout.m_Edges)));
                }

                return result;
            }
        }

        [Serializable]
        private sealed class PlacementData
        {
            [SerializeField] private float m_X;
            [SerializeField] private float m_Y;

            public static PlacementData FromPlacement(Placement placement)
            {
                return new PlacementData { m_X = placement.X, m_Y = placement.Y };
            }

            public Placement ToPlacement()
            {
                return new Placement(m_X, m_Y);
            }
        }

        [Serializable]
        private sealed class EpisodePlacementData
        {
            [SerializeField] private string m_EpisodeId;
            [SerializeField] private PlacementData m_Position = new PlacementData();

            public static List<EpisodePlacementData> FromList(IReadOnlyList<EpisodePlacement> placements)
            {
                var result = new List<EpisodePlacementData>();
                for (var i = 0; i < (placements?.Count ?? 0); i++)
                {
                    result.Add(new EpisodePlacementData
                    {
                        m_EpisodeId = placements[i].EpisodeId,
                        m_Position = PlacementData.FromPlacement(placements[i].Position)
                    });
                }

                return result;
            }

            public static List<EpisodePlacement> ToList(IReadOnlyList<EpisodePlacementData> placements)
            {
                var result = new List<EpisodePlacement>();
                for (var i = 0; i < (placements?.Count ?? 0); i++)
                {
                    var placement = placements[i];
                    if (placement != null)
                    {
                        result.Add(new EpisodePlacement(
                            placement.m_EpisodeId,
                            placement.m_Position?.ToPlacement() ?? default));
                    }
                }

                return result;
            }
        }

        [Serializable]
        private sealed class RouteEdgePlacementData
        {
            [SerializeField] private string m_EdgeId;
            [SerializeField] private List<PlacementData> m_ControlPoints = new List<PlacementData>();
            [SerializeField] private string m_StyleKey;

            public static List<RouteEdgePlacementData> FromList(IReadOnlyList<RouteEdgePlacement> placements)
            {
                var result = new List<RouteEdgePlacementData>();
                for (var i = 0; i < (placements?.Count ?? 0); i++)
                {
                    var placement = placements[i];
                    if (placement == null)
                    {
                        continue;
                    }

                    result.Add(new RouteEdgePlacementData
                    {
                        m_EdgeId = placement.EdgeId,
                        m_ControlPoints = PlacementDataList.FromList(placement.ControlPoints),
                        m_StyleKey = placement.StyleKey
                    });
                }

                return result;
            }

            public static List<RouteEdgePlacement> ToList(IReadOnlyList<RouteEdgePlacementData> placements)
            {
                var result = new List<RouteEdgePlacement>();
                for (var i = 0; i < (placements?.Count ?? 0); i++)
                {
                    var placement = placements[i];
                    if (placement != null)
                    {
                        result.Add(new RouteEdgePlacement(
                            placement.m_EdgeId,
                            PlacementDataList.ToList(placement.m_ControlPoints),
                            placement.m_StyleKey));
                    }
                }

                return result;
            }
        }

        private static class PlacementDataList
        {
            public static List<PlacementData> FromList(IReadOnlyList<Placement> placements)
            {
                var result = new List<PlacementData>();
                for (var i = 0; i < (placements?.Count ?? 0); i++)
                {
                    result.Add(PlacementData.FromPlacement(placements[i]));
                }

                return result;
            }

            public static List<Placement> ToList(IReadOnlyList<PlacementData> placements)
            {
                var result = new List<Placement>();
                for (var i = 0; i < (placements?.Count ?? 0); i++)
                {
                    if (placements[i] != null)
                    {
                        result.Add(placements[i].ToPlacement());
                    }
                }

                return result;
            }
        }
    }
}
