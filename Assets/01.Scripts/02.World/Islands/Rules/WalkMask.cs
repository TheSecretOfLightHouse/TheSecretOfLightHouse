using System;
using UnityEngine;

namespace Lighthouse.World.Islands.Rules
{
    public sealed class WalkMask
    {
        private readonly Vector2 _origin;
        private readonly float _cellSize;
        private readonly int _width;
        private readonly int _height;
        private readonly bool[] _cells;

        public WalkMask(Vector2 origin, float cellSize, int width, int height, bool[] cells)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            if (cellSize <= 0f || width < 1 || height < 1 || cells.Length != width * height)
            {
                throw new ArgumentException("Invalid walk mask layout.");
            }

            _origin = origin;
            _cellSize = cellSize;
            _width = width;
            _height = height;
            _cells = cells;
        }

        public bool Contains(Vector2 point)
        {
            return TryGetIndex(point, out _);
        }

        public bool IsWalkable(Vector2 point)
        {
            return TryGetIndex(point, out int index) && _cells[index];
        }

        private bool TryGetIndex(Vector2 point, out int index)
        {
            int x = Mathf.FloorToInt((point.x - _origin.x) / _cellSize);
            int y = Mathf.FloorToInt((point.y - _origin.y) / _cellSize);
            if (x < 0 || x >= _width || y < 0 || y >= _height)
            {
                index = -1;
                return false;
            }

            index = y * _width + x;
            return true;
        }
    }
}
