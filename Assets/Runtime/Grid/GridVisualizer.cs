using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Grid
{
    /// <summary>
    /// 网格可视化组件
    /// </summary>
    public class GridVisualizer : MonoBehaviour
    {
        private IGridLayout _layout;
        private IGridSurface _surface;
        private GridVisualizerConfig _config;
        private Material _lineMaterial;
        private Camera _camera;
        private bool _isVisible;

        private readonly Dictionary<GridCoord, ECellVisualState> _cellStates = new Dictionary<GridCoord, ECellVisualState>();
        private readonly HashSet<GridCoord> _visibleCells = new HashSet<GridCoord>();

        /// <summary>
        /// 可见范围（格子数）
        /// </summary>
        public int VisibleRange { get; set; } = 50;

        /// <summary>
        /// 是否显示
        /// </summary>
        public bool IsVisible => _isVisible;

        public void Initialize(IGridLayout layout, IGridSurface surface, GridVisualizerConfig config = null)
        {
            _layout = layout;
            _surface = surface;
            _config = config ?? new GridVisualizerConfig();
            _camera = Camera.main;
            CreateLineMaterial();
        }

        private void CreateLineMaterial()
        {
            var shader = Shader.Find("Hidden/Internal-Colored");
            _lineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _lineMaterial.SetInt("_ZWrite", 0);
        }

        public void Show() => _isVisible = true;
        public void Hide() => _isVisible = false;

        public void SetCellState(GridCoord coord, ECellVisualState state)
        {
            if (state == ECellVisualState.None || state == ECellVisualState.Normal)
                _cellStates.Remove(coord);
            else
                _cellStates[coord] = state;
        }

        public void HighlightCells(IEnumerable<GridCoord> coords, ECellVisualState state)
        {
            foreach (var coord in coords)
                SetCellState(coord, state);
        }

        public void ClearHighlights()
        {
            _cellStates.Clear();
        }

        public ECellVisualState GetCellState(GridCoord coord)
        {
            return _cellStates.TryGetValue(coord, out var state) ? state : ECellVisualState.Normal;
        }

        private void OnRenderObject()
        {
            if (!_isVisible || _layout == null || _surface == null || _camera == null) return;

            UpdateVisibleCells();
            DrawGrid();
        }

        private void UpdateVisibleCells()
        {
            _visibleCells.Clear();
            var cameraPos = _camera.transform.position;
            var centerCoord = _layout.LocalToCoord(_surface.WorldToLocal(cameraPos));

            foreach (var coord in _layout.GetCoordsInRange(centerCoord, VisibleRange))
            {
                var worldPos = _surface.LocalToWorld(_layout.CoordToLocal(coord));
                if (IsInViewFrustum(worldPos))
                    _visibleCells.Add(coord);
            }
        }

        private bool IsInViewFrustum(Vector3 worldPos)
        {
            var viewportPos = _camera.WorldToViewportPoint(worldPos);
            return viewportPos.z > 0 && viewportPos.x >= -0.1f && viewportPos.x <= 1.1f 
                   && viewportPos.y >= -0.1f && viewportPos.y <= 1.1f;
        }

        private void DrawGrid()
        {
            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);

            var cameraPos = _camera.transform.position;

            foreach (var coord in _visibleCells)
            {
                var state = GetCellState(coord);
                var color = _config.GetStateColor(state);

                if (_config.EnableFade)
                {
                    var worldPos = _surface.LocalToWorld(_layout.CoordToLocal(coord));
                    float dist = Vector3.Distance(cameraPos, worldPos);
                    float fade = 1f - Mathf.InverseLerp(_config.FadeStartDistance, _config.FadeEndDistance, dist);
                    color.a *= fade;
                }

                if (color.a < 0.01f) continue;

                DrawCellOutline(coord, color);
            }

            GL.End();
            GL.PopMatrix();
        }

        private void DrawCellOutline(GridCoord coord, Color color)
        {
            var vertices = _layout.GetCellVertices(coord);
            GL.Color(color);

            for (int i = 0; i < vertices.Length; i++)
            {
                var start = _surface.LocalToWorld(vertices[i]);
                var end = _surface.LocalToWorld(vertices[(i + 1) % vertices.Length]);
                GL.Vertex(start);
                GL.Vertex(end);
            }
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null)
                DestroyImmediate(_lineMaterial);
        }
    }
}
