using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class CoastLagoonModel
    {
        public const float MinNeighbourReach = 0.25f;
        private const float HalfGap = 0.5f;

        public static float EquivalentRadius(Vector3[] contour)
        {
            if (contour == null || contour.Length < 3)
            {
                return 0f;
            }
            float twiceArea = 0f;
            for (int i = 0; i < contour.Length; i++)
            {
                Vector3 a = contour[i];
                Vector3 b = contour[(i + 1) % contour.Length];
                twiceArea += a.x * b.z - b.x * a.z;
            }
            return Mathf.Sqrt(Mathf.Abs(twiceArea) * 0.5f / Mathf.PI);
        }

        public static float ReachFor(float equivalentRadius, float radiusScale, float minReach)
        {
            return Mathf.Max(minReach, equivalentRadius * radiusScale);
        }

        // Both islands of a pair end up at <= gap/2, so the nearest-coast switch line (always >= gap/2 away) sits at full blue.
        public static float LimitByNeighbour(
            float reach,
            Vector2 centerA, float radiusA, ReadOnlySpan<Vector4> edgesA,
            Vector2 centerB, float radiusB, ReadOnlySpan<Vector4> edgesB)
        {
            float lowerBound = Vector2.Distance(centerA, centerB) - radiusA - radiusB;
            if (lowerBound * HalfGap >= reach)
            {
                return reach;
            }
            float gap = Mathf.Sqrt(Mathf.Min(MinSquaredDistance(edgesA, edgesB), MinSquaredDistance(edgesB, edgesA)));
            return Mathf.Min(reach, Mathf.Max(MinNeighbourReach, gap * HalfGap));
        }

        private static float MinSquaredDistance(ReadOnlySpan<Vector4> from, ReadOnlySpan<Vector4> to)
        {
            float best = float.MaxValue;
            for (int i = 0; i < from.Length; i++)
            {
                Vector2 point = new Vector2(from[i].x, from[i].y);
                for (int j = 0; j < to.Length; j++)
                {
                    Vector2 start = new Vector2(to[j].x, to[j].y);
                    Vector2 tangent = new Vector2(to[j].z, to[j].w) - start;
                    float t = Mathf.Clamp01(Vector2.Dot(point - start, tangent) / Mathf.Max(0.0001f, Vector2.Dot(tangent, tangent)));
                    best = Mathf.Min(best, (point - start - t * tangent).sqrMagnitude);
                }
            }
            return best;
        }
    }
}
