using System.Collections.Generic;

namespace GameDeveloperKit.Grid
{
    /// <summary>
    /// 稀疏网格数据存储（适用于大地图、少量占用场景）
    /// </summary>
    public class SparseGridMap<T> : IGridMap<T> where T : struct
    {
        private readonly Dictionary<GridCoord, T> _data = new Dictionary<GridCoord, T>();
        private readonly T _defaultValue;

        public int Count => _data.Count;

        public SparseGridMap() : this(default) { }

        public SparseGridMap(T defaultValue)
        {
            _defaultValue = defaultValue;
        }

        public T Get(GridCoord coord)
        {
            return _data.TryGetValue(coord, out var value) ? value : _defaultValue;
        }

        public void Set(GridCoord coord, T value)
        {
            _data[coord] = value;
        }

        public bool Contains(GridCoord coord)
        {
            return _data.ContainsKey(coord);
        }

        public bool Remove(GridCoord coord)
        {
            return _data.Remove(coord);
        }

        public void Clear()
        {
            _data.Clear();
        }

        public IEnumerable<GridCoord> GetAllCoords()
        {
            return _data.Keys;
        }
    }
}
