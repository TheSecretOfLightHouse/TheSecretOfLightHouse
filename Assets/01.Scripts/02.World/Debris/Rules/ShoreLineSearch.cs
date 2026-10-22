using Lighthouse.World.Islands.Rules;
using UnityEngine;

namespace Lighthouse.World.Debris.Rules
{
    public static class ShoreLineSearch
    {
        private const float MinDirectionSqr = 1e-6f;

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
