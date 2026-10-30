using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Islands.View
{
    public static class TerrainCoastContour
    {
        private const float ScanStep = 0.1f;
        private const int RefineSteps = 8;

        public static Vector3[] Compute(Terrain terrain, float seaLevel, float shoreHeight, float shoreOverlap)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            Vector3 center = origin + size * 0.5f;
            center.y = seaLevel;
            var contour = new Vector3[CoastWaveRegistry.Segments];
            float height = seaLevel + shoreHeight - origin.y;

            for (int i = 0; i < contour.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / contour.Length;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                float reachX = Mathf.Abs(x) > 0.0001f ? size.x * 0.5f / Mathf.Abs(x) : float.MaxValue;
                float reachZ = Mathf.Abs(z) > 0.0001f ? size.z * 0.5f / Mathf.Abs(z) : float.MaxValue;
                float reach = Mathf.Min(reachX, reachZ);
                float inside = 0f;

                // Scan inward from the boundary: an inland pond must not become the outer coast.
                for (float r = reach; r > 0f; r -= ScanStep)
                {
                    if (IsLand(terrain, center + new Vector3(x * r, 0f, z * r), height))
                    {
                        inside = r;
                        break;
                    }
                }

                float outside = Mathf.Min(inside + ScanStep, reach);
                for (int step = 0; step < RefineSteps; step++)
                {
                    float r = (inside + outside) * 0.5f;
                    if (IsLand(terrain, center + new Vector3(x * r, 0f, z * r), height))
                    {
                        inside = r;
                    }
                    else
                    {
                        outside = r;
                    }
                }

                contour[i] = center + new Vector3(x, 0f, z) * Mathf.Max(0f, inside - shoreOverlap);
            }

            return contour;
        }

        private static bool IsLand(Terrain terrain, Vector3 point, float height)
        {
            TerrainData data = terrain.terrainData;
            Vector3 local = point - terrain.transform.position;
            int x = Mathf.Clamp((int)(local.x / data.size.x * data.holesResolution), 0, data.holesResolution - 1);
            int z = Mathf.Clamp((int)(local.z / data.size.z * data.holesResolution), 0, data.holesResolution - 1);
            return !data.IsHole(x, z) && terrain.SampleHeight(point) > height;
        }
    }
}
