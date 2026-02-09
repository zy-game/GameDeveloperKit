using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Grid
{
    /// <summary>
    /// 密集网格数据存储（适用于小地图、高密度占用场景）
    /// </summary>
    public class DenseGridMap<T> : IGridMap<T> where T : struct
    {
        private readonly T[] _data;
        private readonly int _width;
        private readonly int _height;
        private readonly int _offsetX;
        private readonly int _offsetY;
        private int _count;

        public int Count => _count;
        public int Width => _width;
        public int Height => _height;

        public DenseGridMap(int width, int height, int offsetX = 0, int offsetY = 0)
        {
            _width = width;
            _height = height;
            _offsetX = offsetX;
            _offsetY = offsetY;
            _data = new T[width * height];
            _count = 0;
        }

        private int CoordToIndex(GridCoord coord)
        {
            int x = coord.X - _offsetX;
            int y = coord.Y - _offsetY;
            if (x < 0 || x >= _width || y < 0 || y >= _height)
                return -1;
            return y * _width + x;
        }

        private GridCoord IndexToCoord(int index)
        {
            int x = index % _width + _offsetX;
            int y = index / _width + _offsetY;
            return new GridCoord(x, y);
        }

        public T Get(GridCoord coord)
        {
            int index = CoordToIndex(coord);
            return index >= 0 ? _data[index] : default;
        }

        public void Set(GridCoord coord, T value)
        {
            int index = CoordToIndex(coord);
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(coord), $"Coord {coord} is out of bounds");

            if (EqualityComparer<T>.Default.Equals(_data[index], default) && 
                !EqualityComparer<T>.Default.Equals(value, default))
                _count++;
            else if (!EqualityComparer<T>.Default.Equals(_data[index], default) && 
                     EqualityComparer<T>.Default.Equals(value, default))
                _count--;

            _data[index] = value;
        }

        public bool Contains(GridCoord coord)
        {
            int index = CoordToIndex(coord);
            return index >= 0 && !EqualityComparer<T>.Default.Equals(_data[index], default);
        }

        public bool Remove(GridCoord coord)
        {
            int index = CoordToIndex(coord);
            if (index < 0) return false;
            
            if (!EqualityComparer<T>.Default.Equals(_data[index], default))
            {
                _data[index] = default;
                _count--;
                return true;
            }
            return false;
        }

        public void Clear()
        {
            Array.Clear(_data, 0, _data.Length);
            _count = 0;
        }

        public IEnumerable<GridCoord> GetAllCoords()
        {
            for (int i = 0; i < _data.Length; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(_data[i], default))
                    yield return IndexToCoord(i);
            }
        }

        public bool IsInBounds(GridCoord coord)
        {
            return CoordToIndex(coord) >= 0;
        }
    }
}
