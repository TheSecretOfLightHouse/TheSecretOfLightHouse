using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class PoissonGrid
    {
        private const int EmptyCell = -1;
        private const int NeighborRange = 2;
        private const float SquareRootOfTwo = 1.41421356f;

        private readonly List<Vector2> _points;
        private readonly List<int> _overflow = new List<int>();
        private readonly int[] _cells;
        private readonly Vector2 _origin;
        private readonly float _cellSize;
        private readonly float _minDistanceSqr;
        private readonly int _columns;
        private readonly int _rows;

        public PoissonGrid(Rect bounds, float minDistance, List<Vector2> points)
        {
            _points = points;
            _minDistanceSqr = minDistance * minDistance;
            _cellSize = minDistance / SquareRootOfTwo;
            _origin = new Vector2(bounds.xMin - minDistance, bounds.yMin - minDistance);
            _columns = Mathf.CeilToInt((bounds.width + minDistance * 2f) / _cellSize) + 1;
            _rows = Mathf.CeilToInt((bounds.height + minDistance * 2f) / _cellSize) + 1;
            _cells = new int[_columns * _rows];
            Array.Fill(_cells, EmptyCell);
        }

        public bool IsFarEnough(Vector2 point)
        {
            if (!TryGetCell(point, out int column, out int row))
            {
                return false;
            }

            int rowMin = Math.Max(0, row - NeighborRange);
            int rowMax = Math.Min(_rows - 1, row + NeighborRange);
            int columnMin = Math.Max(0, column - NeighborRange);
            int columnMax = Math.Min(_columns - 1, column + NeighborRange);

            for (int r = rowMin; r <= rowMax; r++)
            {
                for (int c = columnMin; c <= columnMax; c++)
                {
                    int index = _cells[r * _columns + c];
                    if (index != EmptyCell && IsTooClose(point, _points[index]))
                    {
                        return false;
                    }
                }
            }

            for (int i = 0; i < _overflow.Count; i++)
            {
                if (IsTooClose(point, _points[_overflow[i]]))
                {
                    return false;
                }
            }

            return true;
        }

        public void Add(int pointIndex)
        {
            if (!TryGetCell(_points[pointIndex], out int column, out int row))
            {
                return;
            }

            int cell = row * _columns + column;
            if (_cells[cell] == EmptyCell)
            {
                _cells[cell] = pointIndex;
            }
            else
            {
                _overflow.Add(pointIndex);
            }
        }

        private bool IsTooClose(Vector2 a, Vector2 b)
        {
            return (a - b).sqrMagnitude < _minDistanceSqr;
        }

        private bool TryGetCell(Vector2 point, out int column, out int row)
        {
            float x = (point.x - _origin.x) / _cellSize;
            float y = (point.y - _origin.y) / _cellSize;
            column = 0;
            row = 0;

            if (x < 0f || y < 0f)
            {
                return false;
            }

            column = (int)x;
            row = (int)y;
            return column < _columns && row < _rows;
        }
    }
}
