using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class BreakerApproach
    {
        public const float ProbeDistance = 6f;
        public const float CoastRange = 24f;
        private const int DirectionCount = 16;
        private const float SwellBias = 2f;
        private const float MinExposure = 0.35f;

        public static Vector2 Resolve(
            ReadOnlySpan<Vector4> bounds,
            ReadOnlySpan<Vector4> edges,
            Vector2 center,
            Vector2 swellDirection,
            out float exposure)
        {
            Vector2 bestOutward = -swellDirection;
            float bestScore = float.MinValue;
            for (int i = 0; i < DirectionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / DirectionCount;
                Vector2 outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 probe = center + outward * ProbeDistance;
                float distance = CoastWaveModel.DistanceAt(bounds, edges, probe.x, probe.y, CoastRange);
                float score = Mathf.Min(distance, CoastRange) + SwellBias * Vector2.Dot(-swellDirection, outward);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestOutward = outward;
                }
            }

            Vector2 travel = -bestOutward;
            exposure = Mathf.Clamp01(MinExposure + (1f - MinExposure) * Vector2.Dot(swellDirection, travel));
            return travel;
        }
    }
}
