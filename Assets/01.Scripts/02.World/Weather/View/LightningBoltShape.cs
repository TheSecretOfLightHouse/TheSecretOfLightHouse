using System;
using UnityEngine;

namespace Lighthouse.World.Weather.View
{
    public static class LightningBoltShape
    {
        public const int MainPointCount = 16;
        public const int BranchPointCount = 6;
        public const int BranchCount = 3;

        private const float UnitScale = 1f / 16777216f;
        private const float TopSlantRatio = 0.25f;
        private const float BranchLengthRatio = 0.3f;
        private const float BranchDropRatio = 0.55f;
        private const int FirstBranchIndex = 3;
        private const int BranchIndexRange = 7;

        public static void FillMain(Span<Vector3> points, Vector3 ground, float height,
            float jitter, int mapSeed, long id)
        {
            uint state = Seed(mapSeed, id, 0);
            float angle = NextUnit(ref state) * Mathf.PI * 2f;
            var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var tangent = new Vector3(-radial.z, 0f, radial.x);
            Vector3 top = ground + Vector3.up * height
                + tangent * ((NextUnit(ref state) * 2f - 1f) * height * TopSlantRatio);

            for (int i = 0; i < points.Length; i++)
            {
                float ratio = i / (float)(points.Length - 1);
                Vector3 point = Vector3.Lerp(top, ground, ratio);

                if (i > 0 && i < points.Length - 1)
                {
                    float amplitude = jitter * (0.35f + 0.65f * (1f - ratio));
                    point += radial * ((NextUnit(ref state) * 2f - 1f) * amplitude);
                    point += tangent * ((NextUnit(ref state) * 2f - 1f) * amplitude);
                }

                points[i] = point;
            }
        }

        public static void FillBranch(Span<Vector3> points, ReadOnlySpan<Vector3> main,
            int branchIndex, float height, float jitter, int mapSeed, long id)
        {
            uint state = Seed(mapSeed, id, branchIndex + 1);
            int startIndex = FirstBranchIndex + (int)(NextUnit(ref state) * BranchIndexRange);
            startIndex = Mathf.Min(startIndex, main.Length - 2);
            Vector3 start = main[startIndex];
            Vector3 trunk = main[startIndex + 1] - start;
            var side = new Vector3(-trunk.z, 0f, trunk.x).normalized;
            float sign = NextUnit(ref state) < 0.5f ? -1f : 1f;
            float length = height * BranchLengthRatio * (0.6f + 0.4f * NextUnit(ref state));
            Vector3 direction = (side * sign * (0.5f + 0.5f * NextUnit(ref state))
                + Vector3.down * BranchDropRatio).normalized;

            for (int i = 0; i < points.Length; i++)
            {
                float ratio = i / (float)(points.Length - 1);
                Vector3 point = start + direction * (length * ratio);

                if (i > 0)
                {
                    point += side * ((NextUnit(ref state) * 2f - 1f) * jitter * 0.5f);
                    point += Vector3.up * ((NextUnit(ref state) * 2f - 1f) * jitter * 0.3f);
                }

                points[i] = point;
            }
        }

        private static uint Seed(int mapSeed, long id, int part)
        {
            return unchecked((uint)mapSeed
                ^ (uint)id
                ^ ((uint)(id >> 32) * 0x9E3779B9u)
                ^ ((uint)(part + 1) * 0x85EBCA6Bu));
        }

        private static float NextUnit(ref uint state)
        {
            unchecked
            {
                state += 0x9E3779B9u;
                uint value = state;
                value = (value ^ (value >> 16)) * 0x85EBCA6Bu;
                value = (value ^ (value >> 13)) * 0xC2B2AE35u;
                value ^= value >> 16;
                return (value >> 8) * UnitScale;
            }
        }
    }
}
