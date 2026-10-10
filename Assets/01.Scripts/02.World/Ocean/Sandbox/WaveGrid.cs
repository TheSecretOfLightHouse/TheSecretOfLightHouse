using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public readonly struct WaveGrid
    {
        private const float Half = 0.5f;

        public WaveGrid(int cells, float cellSize, Vector3 origin)
        {
            Cells = cells;
            CellSize = cellSize;
            Origin = origin;
        }

        public int Cells { get; }

        public float CellSize { get; }

        public Vector3 Origin { get; }

        public int Side => Cells + 1;

        public int VertexCount => Side * Side;

        public Vector2 GetLocalPoint(int index)
        {
            int side = Side;
            float offset = Cells * Half;
            return new Vector2((index % side - offset) * CellSize, (index / side - offset) * CellSize);
        }

        public Vector2 GetWorldPoint(int index)
        {
            Vector2 local = GetLocalPoint(index);
            return new Vector2(Origin.x + local.x, Origin.z + local.y);
        }
    }
}
