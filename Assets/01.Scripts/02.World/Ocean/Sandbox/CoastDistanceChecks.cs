using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public static class CoastDistanceChecks
    {
        private const float Radius = 50f;
        private const float Width = 7f;
        private const float Range = 24f;
        private const float DistanceTolerance = 0.01f;
        private const float ParityTolerance = 0.000001f;

        public static bool RunAll()
        {
            var bounds = new Vector4[2];
            var edges = new Vector4[2 * CoastWaveRegistry.Segments];
            var profiles = new Vector4[2];
            BuildCircle(bounds, edges);

            bool isAllPassed = CheckDistance(bounds, edges);

            isAllPassed &= CheckParity(bounds, edges, profiles, "Plain");
            profiles[0] = new Vector4(0f, 0f, 1f, 0f);
            isAllPassed &= CheckParity(bounds, edges, profiles, "BeachProfile");

            return isAllPassed;
        }

        private static bool CheckDistance(Vector4[] bounds, Vector4[] edges)
        {
            float inside = CoastWaveModel.DistanceAt(bounds, edges, 0f, 0f, Range);
            float outside = CoastWaveModel.DistanceAt(bounds, edges, Radius + 10f, 0f, Range);
            float inRange = CoastWaveModel.DistanceAt(bounds, edges, Radius + 20f, 0f, Range);
            float beyond = CoastWaveModel.DistanceAt(bounds, edges, Radius + 30f, 0f, Range);
            float emptySlot = CoastWaveModel.DistanceAt(new Vector4[1], edges, 0f, 0f, Range);

            bool isPassed = inside == 0f
                && Mathf.Abs(outside - 10f) <= DistanceTolerance
                && Mathf.Abs(inRange - 20f) <= DistanceTolerance
                && beyond == float.MaxValue
                && emptySlot == float.MaxValue;
            WaveCheckLog.Report("Coast.DistanceAt", isPassed, $"inside={inside}, out={outside:F3}, inRange={inRange:F3}, beyond={beyond}");
            return isPassed;
        }

        private static bool CheckParity(Vector4[] bounds, Vector4[] edges, Vector4[] profiles, string label)
        {
            float maxDifference = 0f;
            for (float x = -90f; x <= 90f; x += 7.5f)
            {
                for (float z = -90f; z <= 90f; z += 7.5f)
                {
                    float height = 0.3f * Mathf.Sin(x * 0.1f) + 0.2f * Mathf.Cos(z * 0.13f);
                    float expected = Reference(bounds, edges, profiles, x, z, 12.5f, height, 0.5f);
                    float actual = CoastWaveModel.Apply(bounds, edges, profiles, x, z, 12.5f, height, 0.5f);
                    maxDifference = Mathf.Max(maxDifference, Mathf.Abs(expected - actual));
                }
            }

            bool isPassed = maxDifference <= ParityTolerance;
            WaveCheckLog.Report($"Coast.ApplyParity.{label}", isPassed, $"maxDiff={maxDifference}");
            return isPassed;
        }

        private static void BuildCircle(Vector4[] bounds, Vector4[] edges)
        {
            int count = CoastWaveRegistry.Segments;
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                float b = (i + 1) * Mathf.PI * 2f / count;
                edges[i] = new Vector4(Radius * Mathf.Cos(a), Radius * Mathf.Sin(a), Radius * Mathf.Cos(b), Radius * Mathf.Sin(b));
            }

            bounds[0] = new Vector4(0f, 0f, Radius + Width * CoastWaveRegistry.ReachWidthScale, Width);
        }

        private static float Reference(ReadOnlySpan<Vector4> bounds, ReadOnlySpan<Vector4> edges, ReadOnlySpan<Vector4> profiles,
            float x, float z, float time, float height, float energy)
        {
            float nearest = float.MaxValue;
            float width = 0f;
            float distance = 0f;
            for (int island = 0; island < bounds.Length; island++)
            {
                Vector4 area = bounds[island];
                float dx = x - area.x;
                float dz = z - area.y;
                if (area.w <= 0f || dx * dx + dz * dz > area.z * area.z)
                {
                    continue;
                }
                float best = float.MaxValue;
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
                    if (square < best)
                    {
                        best = square;
                        signed = (px * ez - pz * ex >= 0f ? 1f : -1f) * Mathf.Sqrt(square);
                    }
                }
                if (best < nearest)
                {
                    nearest = best;
                    width = CoastWaveModel.WidthAt(area.w, profiles[island], x, z);
                    distance = signed;
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
            float result = Mathf.Lerp(shore, height, blend);
            float safeDepth = 0.025f + 0.012f * d + 0.015f * Mathf.Max(0f, d - width);
            float release = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(width * 1.3f, width * 2.2f, d));
            return Mathf.Lerp(Mathf.Max(result, -safeDepth), result, release);
        }
    }
}
