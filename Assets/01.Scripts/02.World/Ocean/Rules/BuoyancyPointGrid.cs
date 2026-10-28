using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class BuoyancyPointGrid
    {
        private const float CellCenter = 0.5f;

        public static int BuildOffsets(
            Bounds bounds,
            Vector3Int divisions,
            List<Vector3> offsets)
        {
            offsets.Clear();

            if (divisions.x < 1 || divisions.y < 1 || divisions.z < 1)
            {
                return 0;
            }

            Vector3 size = bounds.size;
            Vector3 min = bounds.min;

            for (int y = 0; y < divisions.y; y++)
            {
                for (int z = 0; z < divisions.z; z++)
                {
                    for (int x = 0; x < divisions.x; x++)
                    {
                        offsets.Add(new Vector3(
                            min.x + size.x * (x + CellCenter) / divisions.x,
                            min.y + size.y * (y + CellCenter) / divisions.y,
                            min.z + size.z * (z + CellCenter) / divisions.z));
                    }
                }
            }

            return offsets.Count;
        }

        public static float GetCellHeight(Bounds bounds, Vector3Int divisions)
        {
            return divisions.y < 1 ? 0f : bounds.size.y / divisions.y;
        }
    }
}
