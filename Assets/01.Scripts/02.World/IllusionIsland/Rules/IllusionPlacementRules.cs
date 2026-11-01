using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.IllusionIsland.Rules
{
    public static class IllusionPlacementRules
    {
        public static bool IsPlacementValid(
            Vector3 candidate,
            IReadOnlyList<Vector3> islands,
            float minIslandDistance,
            IReadOnlyList<Vector3> spawners,
            float minSpawnerDistance,
            IReadOnlyList<Vector3> illusions,
            float minIllusionDistance)
        {
            return IsClearOfAll(candidate, islands, minIslandDistance)
                && IsClearOfAll(candidate, spawners, minSpawnerDistance)
                && IsClearOfAll(candidate, illusions, minIllusionDistance);
        }

        public static bool IsClearOfAll(Vector3 candidate, IReadOnlyList<Vector3> others, float minDistance)
        {
            if (others == null)
            {
                return true;
            }

            float sqrMin = minDistance * minDistance;
            for (int i = 0; i < others.Count; i++)
            {
                float dx = candidate.x - others[i].x;
                float dz = candidate.z - others[i].z;
                if (dx * dx + dz * dz < sqrMin)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
