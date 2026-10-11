using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public static class WaveMeshBuilder
    {
        private const int IndicesPerCell = 6;

        public static void FillVertices(
            Vector3[] vertices,
            in WaveGrid grid,
            float time,
            ReadOnlySpan<WaveParams> waves,
            ReadOnlySpan<WaveModifierData> modifiers)
        {
            Vector3 origin = grid.Origin;

            for (int i = 0; i < grid.VertexCount; i++)
            {
                Vector2 local = grid.GetLocalPoint(i);
                float restX = origin.x + local.x;
                float restZ = origin.z + local.y;

                Vector3 displacement = WaveModel.DisplacementAt(waves, restX, restZ, time);
                float height = displacement.y + WaveModifierModel.HeightAt(modifiers, restX + displacement.x, restZ + displacement.z);
                vertices[i] = new Vector3(local.x + displacement.x, height, local.y + displacement.z);
            }
        }

        public static int[] BuildTriangles(in WaveGrid grid)
        {
            int cells = grid.Cells;
            int side = grid.Side;
            int cellCount = cells * cells;
            int[] triangles = new int[cellCount * IndicesPerCell];
            int index = 0;

            for (int cell = 0; cell < cellCount; cell++)
            {
                int corner = cell / cells * side + cell % cells;
                triangles[index++] = corner;
                triangles[index++] = corner + side;
                triangles[index++] = corner + 1;
                triangles[index++] = corner + 1;
                triangles[index++] = corner + side;
                triangles[index++] = corner + side + 1;
            }

            return triangles;
        }
    }
}
