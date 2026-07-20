using System;
using System.Collections.Generic;
using GameDeveloperKit.EditorNodeGraph;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.StoryEditor.Authoring;
using GameDeveloperKit.StoryEditor.Model;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.StoryEditor.UI
{
    public sealed partial class MainWindow
    {
        private readonly List<string> m_LayoutChoiceIds = new List<string>();
        private string m_SelectedRouteLayoutId;
        private string m_SelectedRouteEdgeId;
        private VisualElement m_RouteLayoutToolbar;
        private DropdownField m_LayoutSelector;
        private Button m_RemoveLayoutButton;
        private bool m_RefreshingLayoutToolbar;

        private VisualElement CreateRouteLayoutToolbar()
        {
            m_RouteLayoutToolbar = new VisualElement();
            m_RouteLayoutToolbar.AddToClassList("story-editor__route-layout-toolbar");
            m_LayoutSelector = new DropdownField { tooltip = "选择当前路线参考布局。" };
            m_LayoutSelector.AddToClassList("story-editor__route-layout-selector");
            m_LayoutSelector.RegisterValueChangedCallback(_ => SelectRouteLayout(m_LayoutSelector.index));
            m_RouteLayoutToolbar.Add(m_LayoutSelector);

            var add = new ToolbarMenu
            {
                text = "新增布局",
                tooltip = "添加路线布局。"
            };
            add.name = "story-route-layout-add";
            add.AddToClassList("story-editor__route-layout-command");
            add.AddToClassList("story-editor__route-layout-add");
            add.menu.AppendAction("横版布局", _ => AddLayout(LayoutOrientation.Landscape));
            add.menu.AppendAction("竖版布局", _ => AddLayout(LayoutOrientation.Portrait));
            add.menu.AppendAction("自定义布局", _ => AddLayout(LayoutOrientation.Custom));
            m_RouteLayoutToolbar.Add(add);
            m_RemoveLayoutButton = CreateLayoutIconButton(RemoveSelectedLayout, "Toolbar Minus", "删除当前路线布局。");
            m_RemoveLayoutButton.name = "story-route-layout-remove";
            m_RemoveLayoutButton.AddToClassList("story-editor__route-layout-command");
            m_RouteLayoutToolbar.Add(m_RemoveLayoutButton);
            return m_RouteLayoutToolbar;
        }

        private void RefreshRouteLayoutToolbar()
        {
            if (m_RouteLayoutToolbar == null)
            {
                return;
            }

            m_RouteLayoutToolbar.style.display = m_EditorMode == EditorMode.Route
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            if (m_EditorMode != EditorMode.Route)
            {
                return;
            }

            EnsureRouteLayoutSelection();
            var labels = new List<string>();
            m_LayoutChoiceIds.Clear();
            for (var i = 0; i < (m_SelectedVolume?.Layouts.Count ?? 0); i++)
            {
                var layout = m_SelectedVolume.Layouts[i];
                if (layout == null)
                {
                    continue;
                }

                labels.Add($"{OrientationLabel(layout.Orientation)}布局 {i + 1}");
                m_LayoutChoiceIds.Add(layout.LayoutId);
            }

            if (labels.Count == 0)
            {
                labels.Add("会话预览（未保存）");
                m_LayoutChoiceIds.Add(null);
            }

            m_RefreshingLayoutToolbar = true;
            m_LayoutSelector.choices = labels;
            var selectedIndex = Math.Max(0, m_LayoutChoiceIds.IndexOf(m_SelectedRouteLayoutId));
            m_LayoutSelector.index = selectedIndex;
            m_LayoutSelector.SetValueWithoutNotify(labels[selectedIndex]);
            m_RemoveLayoutButton.SetEnabled(SelectedRouteLayout() != null);
            m_RefreshingLayoutToolbar = false;
        }

        private void SelectRouteLayout(int index)
        {
            if (m_RefreshingLayoutToolbar || index < 0 || index >= m_LayoutChoiceIds.Count)
            {
                return;
            }

            m_SelectedRouteLayoutId = m_LayoutChoiceIds[index];
            m_SelectedRouteEdgeId = null;
            RefreshAll("已切换路线布局。");
            m_Canvas.schedule.Execute(m_Canvas.FrameAll);
        }

        private void AddLayout(LayoutOrientation orientation)
        {
            if (m_SelectedVolume == null)
            {
                return;
            }

            var result = new LayoutMutation(m_Asset).AddLayout(m_SelectedVolume.VolumeId, orientation);
            if (result.Succeeded)
            {
                m_SelectedRouteLayoutId = result.LayoutId;
                m_SelectedRouteEdgeId = null;
                RefreshAll(result.Message);
                m_Canvas.schedule.Execute(m_Canvas.FrameAll);
                return;
            }

            RefreshReport(result.Message);
        }

        private void RemoveSelectedLayout()
        {
            var layout = SelectedRouteLayout();
            if (layout == null || m_SelectedVolume == null)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "删除路线布局",
                    "删除后将失去当前布局的背景、节点位置和边路径。",
                    "删除",
                    "取消"))
            {
                return;
            }

            var result = new LayoutMutation(m_Asset).RemoveLayout(m_SelectedVolume.VolumeId, layout.LayoutId);
            if (result.Succeeded)
            {
                m_SelectedRouteLayoutId = null;
                m_SelectedRouteEdgeId = null;
                EnsureRouteLayoutSelection();
                RefreshAll(result.Message);
                return;
            }

            RefreshReport(result.Message);
        }

        private void SelectRouteWire(string wireId)
        {
            m_SelectedRouteEdgeId = wireId;
            RefreshRouteInspector();
        }

        private void MoveRouteNodes(IReadOnlyList<EditorNodeGraphMove> moves)
        {
            var layout = SelectedRouteLayout();
            if (layout == null || m_SelectedVolume == null)
            {
                return;
            }

            Placement? root = null;
            var episodes = new List<EpisodePlacement>();
            for (var i = 0; i < (moves?.Count ?? 0); i++)
            {
                var position = new Placement(moves[i].Position.x, moves[i].Position.y);
                if (m_RouteGraphAdapter.IsVirtualRoot(moves[i].NodeId))
                {
                    root = position;
                }
                else
                {
                    episodes.Add(new EpisodePlacement(moves[i].NodeId, position));
                }
            }

            ApplyLayoutResult(new LayoutMutation(m_Asset).MoveNodes(
                m_SelectedVolume.VolumeId,
                layout.LayoutId,
                root,
                episodes));
        }

        private void UpdateRouteEdgePath(string edgeId, IReadOnlyList<Vector2> points, string styleKey)
        {
            var layout = SelectedRouteLayout();
            if (layout == null || m_SelectedVolume == null)
            {
                return;
            }

            var placements = new List<Placement>();
            for (var i = 0; i < (points?.Count ?? 0); i++)
            {
                placements.Add(new Placement(points[i].x, points[i].y));
            }

            ApplyLayoutResult(new LayoutMutation(m_Asset).UpdateEdgePath(
                m_SelectedVolume.VolumeId,
                layout.LayoutId,
                edgeId,
                placements,
                styleKey));
        }

        private void UpdateSelectedLayout(LayoutMetadata metadata)
        {
            var layout = SelectedRouteLayout();
            if (layout == null || m_SelectedVolume == null)
            {
                return;
            }

            ApplyLayoutResult(new LayoutMutation(m_Asset).UpdateLayout(
                m_SelectedVolume.VolumeId,
                layout.LayoutId,
                metadata));
        }

        private void ApplyLayoutResult(LayoutMutationResult result)
        {
            if (result.Succeeded)
            {
                RefreshAll(result.Message);
                return;
            }

            RefreshReport(result.Message);
        }

        private void EnsureRouteLayoutSelection()
        {
            if (FindRouteLayout(m_SelectedVolume, m_SelectedRouteLayoutId) != null)
            {
                return;
            }

            m_SelectedRouteLayoutId = null;
            for (var i = 0; i < (m_SelectedVolume?.Layouts.Count ?? 0); i++)
            {
                if (m_SelectedVolume.Layouts[i] != null)
                {
                    m_SelectedRouteLayoutId = m_SelectedVolume.Layouts[i].LayoutId;
                    break;
                }
            }
        }

        private void EnsureRouteEdgeSelection()
        {
            if (string.IsNullOrWhiteSpace(m_SelectedRouteEdgeId))
            {
                return;
            }

            var layout = SelectedRouteLayout();
            if (FindRouteEdgePlacement(layout, m_SelectedRouteEdgeId) == null)
            {
                m_SelectedRouteEdgeId = null;
            }
        }

        private AuthoringRouteLayout SelectedRouteLayout()
        {
            return FindRouteLayout(m_SelectedVolume, m_SelectedRouteLayoutId);
        }

        private static AuthoringRouteLayout FindRouteLayout(AuthoringVolume volume, string layoutId)
        {
            for (var i = 0; i < (volume?.Layouts.Count ?? 0); i++)
            {
                if (volume.Layouts[i] != null &&
                    string.Equals(volume.Layouts[i].LayoutId, layoutId, StringComparison.Ordinal))
                {
                    return volume.Layouts[i];
                }
            }

            return null;
        }

        private static AuthoringRouteEdgePlacement FindRouteEdgePlacement(
            AuthoringRouteLayout layout,
            string edgeId)
        {
            for (var i = 0; i < (layout?.Edges.Count ?? 0); i++)
            {
                if (layout.Edges[i] != null &&
                    string.Equals(layout.Edges[i].EdgeId, edgeId, StringComparison.Ordinal))
                {
                    return layout.Edges[i];
                }
            }

            return null;
        }

        private static string OrientationLabel(LayoutOrientation orientation)
        {
            return orientation == LayoutOrientation.Landscape
                ? "横版"
                : orientation == LayoutOrientation.Portrait
                    ? "竖版"
                    : "自定义";
        }

        private static Button CreateLayoutIconButton(Action clicked, string iconName, string tooltip)
        {
            var button = new Button(clicked) { tooltip = tooltip };
            var image = new Image
            {
                image = EditorGUIUtility.IconContent(iconName).image,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            image.AddToClassList("story-editor__route-layout-command-icon");
            button.Add(image);
            return button;
        }

        private void BuildRouteLayoutInspector()
        {
            if (m_EditorMode != EditorMode.Route)
            {
                return;
            }

            var layout = SelectedRouteLayout();
            var section = new Label("路线布局");
            section.AddToClassList("story-editor__route-inspector-section");
            m_RouteInspectorContent.Add(section);
            if (layout == null)
            {
                AddInspectorValue("状态", "会话预览（未保存）");
                return;
            }

            AddInspectorValue("布局 ID", layout.LayoutId);
            var orientation = new DropdownField(
                "方向",
                new List<string> { "横版", "竖版", "自定义" },
                (int)layout.Orientation);
            orientation.RegisterValueChangedCallback(_ => UpdateSelectedLayout(new LayoutMetadata(
                (LayoutOrientation)orientation.index,
                layout.BackgroundImage,
                layout.EditorGuideImage)));
            orientation.AddToClassList("story-editor__route-inspector-field");
            m_RouteInspectorContent.Add(orientation);

            var background = CreateTextureField("运行时背景", layout.BackgroundImage);
            background.RegisterValueChangedCallback(evt => UpdateSelectedLayout(new LayoutMetadata(
                layout.Orientation,
                evt.newValue as Texture2D,
                layout.EditorGuideImage)));
            m_RouteInspectorContent.Add(background);

            var guide = CreateTextureField("参考图", layout.EditorGuideImage);
            guide.RegisterValueChangedCallback(evt => UpdateSelectedLayout(new LayoutMetadata(
                layout.Orientation,
                layout.BackgroundImage,
                evt.newValue as Texture2D)));
            m_RouteInspectorContent.Add(guide);
        }

        private bool BuildRouteEdgeInspector()
        {
            if (m_EditorMode != EditorMode.Route || string.IsNullOrWhiteSpace(m_SelectedRouteEdgeId))
            {
                return false;
            }

            var edge = FindRouteEdgePlacement(SelectedRouteLayout(), m_SelectedRouteEdgeId);
            if (edge == null)
            {
                return false;
            }

            var section = new Label("边路径");
            section.AddToClassList("story-editor__route-inspector-section");
            m_RouteInspectorContent.Add(section);
            AddInspectorValue("Edge ID", edge.EdgeId);
            AddInspectorValue("控制点", edge.ControlPoints.Count.ToString());
            var style = CreateTextField("样式 Key", edge.StyleKey, false);
            style.RegisterValueChangedCallback(evt => UpdateRouteEdgeStyle(edge, evt.newValue));
            m_RouteInspectorContent.Add(style);
            return true;
        }

        private void UpdateRouteEdgeStyle(AuthoringRouteEdgePlacement edge, string styleKey)
        {
            var points = new List<Vector2>();
            for (var i = 0; i < edge.ControlPoints.Count; i++)
            {
                if (edge.ControlPoints[i] != null)
                {
                    points.Add(edge.ControlPoints[i].Position);
                }
            }

            UpdateRouteEdgePath(edge.EdgeId, points, styleKey);
        }

        private static ObjectField CreateTextureField(string label, Texture2D image)
        {
            var field = new ObjectField(label)
            {
                objectType = typeof(Texture2D),
                allowSceneObjects = false
            };
            field.SetValueWithoutNotify(image);
            field.AddToClassList("story-editor__route-inspector-field");
            return field;
        }
    }
}
