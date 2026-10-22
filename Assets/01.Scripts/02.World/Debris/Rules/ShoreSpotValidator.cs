using Lighthouse.World.Islands.Rules;
using UnityEngine;

namespace Lighthouse.World.Debris.Rules
{
    public static class ShoreSpotValidator
    {
        private const float ProbeDistance = 0.5f;

        public static bool TryValidate(
            IGroundHeightSource ground,
            short islandIndex,
            Vector2 point,
            float minHeight,
            float maxSlopeDegrees,
            out float height)
        {
            height = 0f;
            if (ground == null
                || !ground.TryGetGroundHeight(islandIndex, point.x, point.y, out height)
                || height < minHeight)
            {
                return false;
            }

            float maxRise = Mathf.Tan(maxSlopeDegrees * Mathf.Deg2Rad) * ProbeDistance;
            return IsGentle(ground, islandIndex, point.x + ProbeDistance, point.y, height, maxRise)
                && IsGentle(ground, islandIndex, point.x - ProbeDistance, point.y, height, maxRise)
                && IsGentle(ground, islandIndex, point.x, point.y + ProbeDistance, height, maxRise)
                && IsGentle(ground, islandIndex, point.x, point.y - ProbeDistance, height, maxRise);
        }

        private static bool IsGentle(
            IGroundHeightSource ground,
            short islandIndex,
            float x,
            float z,
            float centerHeight,
            float maxRise)
        {
            return ground.TryGetGroundHeight(islandIndex, x, z, out float neighbor)
                && Mathf.Abs(neighbor - centerHeight) <= maxRise;
        }
    }
}
