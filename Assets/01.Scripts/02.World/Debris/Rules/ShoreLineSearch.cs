using Lighthouse.World.Islands.Rules;
using UnityEngine;

namespace Lighthouse.World.Debris.Rules
{
    public static class ShoreLineSearch
    {
        private const float MinDirectionSqr = 1e-6f;
        private const int TurnDirections = 12;
        private const float TurnRadians = 6.2831853f / TurnDirections;

        public static bool TryFindNearestShore(
            IGroundHeightSource ground,
            short islandIndex,
            Vector2 center,
            Vector2 direction,
            float maxDistance,
            float step,
            float minHeight,
            int missCountToExit,
            out Vector2 foundDirection,
            out float shoreDistance)
        {
            foundDirection = direction;
            shoreDistance = 0f;
            if (direction.sqrMagnitude < MinDirectionSqr)
            {
                return false;
            }

            direction.Normalize();
            for (int i = 0; i < TurnDirections; i++)
            {
                int rank = (i + 1) / 2;
                float angle = rank * ((i & 1) == 1 ? TurnRadians : -TurnRadians);
                Vector2 candidate = Rotate(direction, angle);
                if (TryFindShore(ground, islandIndex, center, candidate, maxDistance, step, minHeight, missCountToExit, out shoreDistance)
                    && shoreDistance > 0f)
                {
                    foundDirection = candidate;
                    return true;
                }
            }

            shoreDistance = 0f;
            return false;
        }

        private static Vector2 Rotate(Vector2 vector, float radians)
        {
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
        }

        public static bool TryFindShore(
            IGroundHeightSource ground,
            short islandIndex,
            Vector2 center,
            Vector2 direction,
            float maxDistance,
            float step,
            float minHeight,
            int missCountToExit,
            out float shoreDistance)
        {
            shoreDistance = 0f;
            if (ground == null || step <= 0f || maxDistance <= 0f || missCountToExit < 1
                || direction.sqrMagnitude < MinDirectionSqr)
            {
                return false;
            }

            direction.Normalize();
            int stepCount = (int)(maxDistance / step);
            bool isOnGround = false;
            float lastGround = 0f;
            int missCount = 0;

            for (int i = 0; i <= stepCount; i++)
            {
                float distance = i * step;
                float x = center.x + direction.x * distance;
                float z = center.y + direction.y * distance;
                bool hasGround = ground.TryGetGroundHeight(islandIndex, x, z, out float height)
                    && height >= minHeight;

                if (hasGround)
                {
                    isOnGround = true;
                    lastGround = distance;
                    missCount = 0;
                }
                else if (isOnGround)
                {
                    missCount++;
                    if (missCount >= missCountToExit)
                    {
                        break;
                    }
                }
            }

            shoreDistance = lastGround;
            return isOnGround;
        }
    }
}
