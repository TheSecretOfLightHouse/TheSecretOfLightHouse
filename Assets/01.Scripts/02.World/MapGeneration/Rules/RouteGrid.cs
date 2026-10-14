using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class RouteGrid
    {
        private readonly float _cellSize;
        private readonly float _origin;
        private readonly int _size;
        private readonly bool[] _blocked;
        private readonly bool[] _visited;
        private readonly int[] _queue;

        public RouteGrid(float halfExtent, float cellSize)
        {
            _cellSize = cellSize;
            _origin = -halfExtent;
            _size = Mathf.Max(1, Mathf.CeilToInt(halfExtent * 2f / cellSize));
            int count = _size * _size;
            _blocked = new bool[count];
            _visited = new bool[count];
            _queue = new int[count];
        }

        public void BlockCircle(Vector2 center, float radius)
        {
            GetRange(center.x, radius, out int minX, out int maxX);
            GetRange(center.y, radius, out int minZ, out int maxZ);
            float radiusSqr = radius * radius;

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if ((CellCenter(x, z) - center).sqrMagnitude < radiusSqr)
                    {
                        _blocked[z * _size + x] = true;
                    }
                }
            }
        }

        public void FloodFromRing(Vector2 center, float innerRadius, float outerRadius)
        {
            int tail = 0;
            GetRange(center.x, outerRadius, out int minX, out int maxX);
            GetRange(center.y, outerRadius, out int minZ, out int maxZ);
            float innerSqr = innerRadius * innerRadius;
            float outerSqr = outerRadius * outerRadius;

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float distanceSqr = (CellCenter(x, z) - center).sqrMagnitude;
                    if (distanceSqr >= innerSqr && distanceSqr <= outerSqr)
                    {
                        tail = Enqueue(x, z, tail);
                    }
                }
            }

            for (int head = 0; head < tail; head++)
            {
                int x = _queue[head] % _size;
                int z = _queue[head] / _size;
                tail = Enqueue(x + 1, z, tail);
                tail = Enqueue(x - 1, z, tail);
                tail = Enqueue(x, z + 1, tail);
                tail = Enqueue(x, z - 1, tail);
            }
        }

        public bool IsReachedNear(Vector2 center, float radius)
        {
            GetRange(center.x, radius, out int minX, out int maxX);
            GetRange(center.y, radius, out int minZ, out int maxZ);
            float radiusSqr = radius * radius;

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (_visited[z * _size + x] && (CellCenter(x, z) - center).sqrMagnitude <= radiusSqr)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private int Enqueue(int x, int z, int tail)
        {
            if (x < 0 || z < 0 || x >= _size || z >= _size)
            {
                return tail;
            }

            int index = z * _size + x;
            if (_blocked[index] || _visited[index])
            {
                return tail;
            }

            _visited[index] = true;
            _queue[tail] = index;
            return tail + 1;
        }

        private void GetRange(float center, float radius, out int min, out int max)
        {
            min = Mathf.Clamp(Mathf.FloorToInt((center - radius - _origin) / _cellSize), 0, _size - 1);
            max = Mathf.Clamp(Mathf.FloorToInt((center + radius - _origin) / _cellSize), 0, _size - 1);
        }

        private Vector2 CellCenter(int x, int z)
        {
            return new Vector2(_origin + (x + 0.5f) * _cellSize, _origin + (z + 0.5f) * _cellSize);
        }
    }
}
