using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class CoastWaveModel
    {
        public static float WidthAt(float width, Vector4 profile, float x, float z)
        {
            if (profile.z * profile.z + profile.w * profile.w < 0.1f)
            {
                return width;
            }
            float dx = x - profile.x;
            float dz = z - profile.y;
            float length = Mathf.Sqrt(dx * dx + dz * dz);
            float facing = (dx * profile.z + dz * profile.w) / Mathf.Max(0.001f, length);
            float beach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.8f, facing));
            return width * Mathf.Lerp(0.14f, 1f, beach);
        }

        public static float DistanceAt(ReadOnlySpan<Vector4> bounds, ReadOnlySpan<Vector4> edges,
            float x, float z, float range)
        {
            float nearest = float.MaxValue;
            float distance = float.MaxValue;
            for (int island = 0; island < bounds.Length; island++)
            {
                Vector4 area = bounds[island];
                if (area.w <= 0f)
                {
                    continue;
                }
                float reach = Mathf.Max(0f, area.z - area.w * CoastWaveRegistry.ReachWidthScale) + range;
                float dx = x - area.x;
                float dz = z - area.y;
                if (dx * dx + dz * dz > reach * reach)
                {
                    continue;
                }
                float signed = NearestSignedDistance(edges, island, x, z, out float square);
                if (square < nearest)
                {
                    nearest = square;
                    distance = Mathf.Max(0f, signed);
                }
            }
            return distance;
        }

        public static float Apply(ReadOnlySpan<Vector4> bounds, ReadOnlySpan<Vector4> edges, ReadOnlySpan<Vector4> profiles,
            float x, float z, float time, float height, float energy, ReadOnlySpan<Vector4> lagoons = default)
        {
            float nearest = float.MaxValue;
            float width = 0f;
            float distance = 0f;
            float shoreLift = 0f;
            for (int island = 0; island < bounds.Length; island++)
            {
                Vector4 area = bounds[island];
                float dx = x - area.x;
                float dz = z - area.y;
                if (area.w <= 0f || dx * dx + dz * dz > area.z * area.z)
                {
                    continue;
                }
                float signed = NearestSignedDistance(edges, island, x, z, out float best);
                if (best < nearest)
                {
                    nearest = best;
                    width = WidthAt(area.w, profiles[island], x, z);
                    distance = signed;
                    shoreLift = island < lagoons.Length ? lagoons[island].y : 0f;
                }
            }
            if (width <= 0f)
            {
                return height;
            }
            float d = Mathf.Max(0f, distance);
            float fetch = Mathf.Max(7f, width);
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fetch * 2.2f, fetch * 5f, d));
            float beach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 5f, width));
            float amplitude = Mathf.Min(Mathf.Min(0.55f, energy * 0.55f), Mathf.Lerp(0.28f, 0.035f + d * 0.06f, beach))
                * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Mathf.Lerp(-0.1f, 0.1f, beach), Mathf.Lerp(0.3f, 2f, beach), d));
            float phase = d * 0.55f + time * 0.9f;
            float shore = amplitude * (0.7f * Mathf.Sin(phase) - 0.4f * Mathf.Cos(phase * 2f) - 0.22f * Mathf.Sin(phase * 3f));
            float runup = shoreLift * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.6f, d))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f, 5f, d)));
            float result = Mathf.Lerp(shore, height, blend) + runup;
            float safeDepth = 0.025f + 0.012f * d + 0.015f * Mathf.Max(0f, d - width);
            float release = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(width * 1.3f, width * 2.2f, d));
            return Mathf.Lerp(Mathf.Max(result, -safeDepth), result, release);
        }

        private static float NearestSignedDistance(ReadOnlySpan<Vector4> edges, int island, float x, float z, out float squared)
        {
            squared = float.MaxValue;
            float signed = 0f;
            for (int i = 0; i < CoastWaveRegistry.Segments; i++)
            {
                Vector4 edge = edges[island * CoastWaveRegistry.Segments + i];
                float ex = edge.z - edge.x;
                float ez = edge.w - edge.y;
                float t = Mathf.Clamp01(((x - edge.x) * ex + (z - edge.y) * ez) / Mathf.Max(0.0001f, ex * ex + ez * ez));
                float px = x - edge.x - t * ex;
                float pz = z - edge.y - t * ez;
                float square = px * px + pz * pz;
                if (square < squared)
                {
                    squared = square;
                    signed = (px * ez - pz * ex >= 0f ? 1f : -1f) * Mathf.Sqrt(square);
                }
            }
            return signed;
        }
    }
}
