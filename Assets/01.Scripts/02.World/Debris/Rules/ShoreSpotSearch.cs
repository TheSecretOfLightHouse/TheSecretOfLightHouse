using System;
using Lighthouse.World.Islands.Rules;
using UnityEngine;

namespace Lighthouse.World.Debris.Rules
{
    public static class ShoreSpotSearch
    {
        private const int RingDirections = 12;
        private const float RadiusEpsilon = 1e-4f;
        private const float FullTurn = 6.2831853f;

        public static bool TryFind(
            IGroundHeightSource ground,
            short islandIndex,
            Vector2 preferred,
            Vector2 inward,
            float minHeight,
            float maxSlopeDegrees,
            float searchRadius,
            float step,
            ReadOnlySpan<Vector2> taken,
            float minSeparation,
            out Vector2 point,
            out float height)
        {
            point = preferred;
            if (IsFree(preferred, taken, minSeparation)
                && ShoreSpotValidator.TryValidate(ground, islandIndex, preferred, minHeight, maxSlopeDegrees, out height))
            {
                return true;
            }

            height = 0f;
            if (ground == null || searchRadius <= 0f || step <= 0f)
            {
                return false;
            }

            float baseAngle = Mathf.Atan2(inward.y, inward.x);
            for (float radius = step; radius <= searchRadius + RadiusEpsilon; radius += step)
            {
                for (int i = 0; i < RingDirections; i++)
                {
                    Vector2 candidate = preferred + GetDirection(baseAngle, i) * radius;
                    if (IsFree(candidate, taken, minSeparation)
                        && ShoreSpotValidator.TryValidate(ground, islandIndex, candidate, minHeight, maxSlopeDegrees, out height))
                    {
                        point = candidate;
                        return true;
                    }
                }
            }

            height = 0f;
            return false;
        }

        private static bool IsFree(Vector2 point, ReadOnlySpan<Vector2> taken, float minSeparation)
        {
            float minSqr = minSeparation * minSeparation;
            for (int i = 0; i < taken.Length; i++)
            {
                if ((taken[i] - point).sqrMagnitude < minSqr)
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector2 GetDirection(float baseAngle, int index)
        {
            int rank = (index + 1) / 2;
            float sign = (index & 1) == 1 ? 1f : -1f;
            float angle = baseAngle + rank * sign * (FullTurn / RingDirections);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }
}
