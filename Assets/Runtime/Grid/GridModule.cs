using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Grid
{
    /// <summary>
    /// 网格模块
    /// </summary>
    public sealed class GridModule :  IGridManager
    {
        private readonly Dictionary<string, object> _grids = new Dictionary<string, object>();
        private readonly Dictionary<string, GridVisualizer> _visualizers = new Dictionary<string, GridVisualizer>();
        private Transform _root;

        public void OnStartup()
        {
            var rootObj = new GameObject("GridModule");
            Object.DontDestroyOnLoad(rootObj);
            _root = rootObj.transform;

            // 注册调试面板
            if (Game.Debug is Log.LoggerModule loggerModule)
            {
                loggerModule.RegisterPanel(new GridDebugPanel(this));
            }
        }

        public void OnUpdate(float elapseSeconds)
        {
            // 网格模块不需要每帧更新
        }

        public void OnClearup()
        {
            foreach (var visualizer in _visualizers.Values)
            {
                if (visualizer != null)
                    Object.Destroy(visualizer.gameObject);
            }
            _visualizers.Clear();
            _grids.Clear();

            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
                _root = null;
            }
        }

        public Grid<T> CreateSquareGrid<T>(string name, float cellSize, ESquareNeighborMode neighborMode = ESquareNeighborMode.FourWay) where T : struct
        {
            var layout = new SquareGridLayout(cellSize, neighborMode);
            var surface = new PlaneSurface();
            var data = new SparseGridMap<T>();
            return CreateGrid(name, layout, surface, data);
        }

        public Grid<T> CreateHexGrid<T>(string name, float cellSize, EHexOrientation orientation = EHexOrientation.PointyTop) where T : struct
        {
            var layout = new HexGridLayout(cellSize, orientation);
            var surface = new PlaneSurface();
            var data = new SparseGridMap<T>();
            return CreateGrid(name, layout, surface, data);
        }

        public Grid<T> CreateRhombusGrid<T>(string name, float cellSize, float angle = 45f) where T : struct
        {
            var layout = new RhombusGridLayout(cellSize, angle);
            var surface = new PlaneSurface();
            var data = new SparseGridMap<T>();
            return CreateGrid(name, layout, surface, data);
        }

        public Grid<T> CreateGrid<T>(string name, IGridLayout layout, IGridSurface surface, IGridMap<T> data) where T : struct
        {
            if (_grids.ContainsKey(name))
            {
                Game.Debug?.Warning($"Grid '{name}' already exists, destroying old one");
                DestroyGrid(name);
            }

            var grid = new Grid<T>(name, layout, surface, data);
            _grids[name] = grid;

            // 创建可视化器
            var visualizerObj = new GameObject($"GridVisualizer_{name}");
            visualizerObj.transform.SetParent(_root);
            var visualizer = visualizerObj.AddComponent<GridVisualizer>();
            visualizer.Initialize(layout, surface);
            _visualizers[name] = visualizer;

            return grid;
        }

        public Grid<T> GetGrid<T>(string name) where T : struct
        {
            return _grids.TryGetValue(name, out var grid) ? grid as Grid<T> : null;
        }

        public GridVisualizer GetVisualizer(string gridName)
        {
            return _visualizers.TryGetValue(gridName, out var visualizer) ? visualizer : null;
        }

        public void DestroyGrid(string name)
        {
            if (_visualizers.TryGetValue(name, out var visualizer))
            {
                if (visualizer != null)
                    Object.Destroy(visualizer.gameObject);
                _visualizers.Remove(name);
            }
            _grids.Remove(name);
        }

        public bool HasGrid(string name) => _grids.ContainsKey(name);

        public IEnumerable<string> GetAllGridNames() => _grids.Keys;
        public int GridCount => _grids.Count;
    }
}
