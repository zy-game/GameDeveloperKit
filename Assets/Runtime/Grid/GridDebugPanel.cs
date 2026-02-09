using GameDeveloperKit.Log;
using UnityEngine;

namespace GameDeveloperKit.Grid
{
    /// <summary>
    /// 网格调试面板 - IMGUI 版本
    /// </summary>
    public class GridDebugPanel : IDebugPanelIMGUI
    {
        public string Name => "Grid";
        public int Order => 100;

        private readonly GridModule _module;
        private Vector2 _scrollPosition;

        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private bool _stylesInitialized;

        public GridDebugPanel(GridModule module)
        {
            _module = module;
        }

        public void OnGUI()
        {
            InitStyles();

            if (_module == null) { GUILayout.Label("GridModule not available"); return; }

            GUILayout.Label("Grid Module", _headerStyle);
            GUILayout.Space(5);
            GUILayout.Label($"Total Grids: {_module.GridCount}", _labelStyle);
            GUILayout.Space(10);

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            foreach (var name in _module.GetAllGridNames())
            {
                var visualizer = _module.GetVisualizer(name);

                GUILayout.BeginVertical("box");
                GUILayout.Label(name, _headerStyle);

                if (visualizer != null)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"Visible: {visualizer.IsVisible}", _labelStyle, GUILayout.Width(100));
                    if (GUILayout.Button(visualizer.IsVisible ? "Hide" : "Show", GUILayout.Width(60)))
                    {
                        if (visualizer.IsVisible) visualizer.Hide();
                        else visualizer.Show();
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Label($"Visible Range: {visualizer.VisibleRange}", _labelStyle);
                }

                GUILayout.EndVertical();
                GUILayout.Space(5);
            }

            GUILayout.EndScrollView();
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;
            _headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.4f, 0.8f, 1f) } };
            _labelStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
        }
    }
}
