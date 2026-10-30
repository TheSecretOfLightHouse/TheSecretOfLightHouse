using System;
using UnityEngine;

namespace Lighthouse.World.Islands.Rules
{
    public sealed class WalkMaskBuilder
    {
        private const int PadCells = 2;

        private readonly IGroundHeightSource _ground;
        private readonly short _islandIndex;
        private readonly Vector2 _origin;
        private readonly float _cellSize;
        private readonly int _size;
        private readonly float _minHeight;
        private readonly int _marginCells;
        private readonly bool[] _cells;
        private int _nextRow;

        public WalkMaskBuilder(
            IGroundHeightSource ground,
            short islandIndex,
            Vector2 center,
            float radius,
            float cellSize,
            float minHeight,
            float marginMeters)
        {
            _ground = ground ?? throw new ArgumentNullException(nameof(ground));
            if (cellSize <= 0f || radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size and radius must be positive.");
            }

            _islandIndex = islandIndex;
            _cellSize = cellSize;
            _minHeight = minHeight;
            _marginCells = Mathf.CeilToInt(Mathf.Max(0f, marginMeters) / cellSize);
            int half = Mathf.CeilToInt(radius / cellSize) + _marginCells + PadCells;
            _size = half * 2;
            _origin = center - Vector2.one * (half * cellSize);
            _cells = new bool[_size * _size];
        }

        public int RowCount => _size;
        public bool IsSampled => _nextRow >= _size;

        public void SampleRows(int count)
        {
            int end = Mathf.Min(_size, _nextRow + Mathf.Max(0, count));
            for (int row = _nextRow; row < end; row++)
            {
                float z = _origin.y + (row + 0.5f) * _cellSize;
                for (int column = 0; column < _size; column++)
                {
                    float x = _origin.x + (column + 0.5f) * _cellSize;
                    _cells[row * _size + column] =
                        _ground.TryGetGroundHeight(_islandIndex, x, z, out float height) && height >= _minHeight;
                }
            }

            _nextRow = end;
        }

        public WalkMask Build()
        {
            SampleRows(_size - _nextRow);
            bool[] cells = _marginCells > 0 ? Dilate() : _cells;
            return new WalkMask(_origin, _cellSize, _size, _size, cells);
        }

        private bool[] Dilate()
        {
            var result = new bool[_cells.Length];
            for (int row = 0; row < _size; row++)
            {
                for (int column = 0; column < _size; column++)
                {
                    if (_cells[row * _size + column])
                    {
                        Stamp(result, column, row);
                    }
                }
            }

            return result;
        }

        private void Stamp(bool[] target, int column, int row)
        {
            int rowStart = Mathf.Max(0, row - _marginCells);
            int rowEnd = Mathf.Min(_size - 1, row + _marginCells);
            int columnStart = Mathf.Max(0, column - _marginCells);
            int columnEnd = Mathf.Min(_size - 1, column + _marginCells);
            for (int y = rowStart; y <= rowEnd; y++)
            {
                for (int x = columnStart; x <= columnEnd; x++)
                {
                    target[y * _size + x] = true;
                }
            }
        }
    }
}
