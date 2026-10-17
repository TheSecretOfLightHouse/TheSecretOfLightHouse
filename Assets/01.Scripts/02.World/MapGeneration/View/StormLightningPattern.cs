using System;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.View
{
    public static class StormLightningPattern
    {
        private const float UnitScale = 1f / 16777216f;
        private const float MaxJitterWidthRatio = 0.4f;

        public static void Fill(Span<Vector3> points, float radius, float width,
            float height, float jitter, int mapSeed, long slot, int wallIndex)
        {
            if (points.Length < 3)
            {
                throw new ArgumentException("At least three lightning points are required.");
            }

            uint state = unchecked((uint)mapSeed
                ^ (uint)slot
                ^ ((uint)(slot >> 32) * 0x9E3779B9u)
                ^ ((uint)(wallIndex + 1) * 0x85EBCA6Bu));

            float angle = NextUnit(ref state) * Mathf.PI * 2f;
            var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var tangent = new Vector3(-radial.z, 0f, radial.x);
            Vector3 origin = radial * radius;
            float offsetLimit = Mathf.Min(jitter, width * MaxJitterWidthRatio);

            for (int i = 0; i < points.Length; i++)
            {
                float ratio = i / (float)(points.Length - 1);
                Vector3 point = origin + Vector3.up * (height * (1f - ratio));

                if (i > 0 && i < points.Length - 1)
                {
                    point += radial * ((NextUnit(ref state) * 2f - 1f) * offsetLimit);
                    point += tangent * ((NextUnit(ref state) * 2f - 1f) * offsetLimit);
                }

                points[i] = point;
            }
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
