using System;
using System.Collections.Generic;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Debris.Rules
{
    public static class ShoreIslandSelector
    {
        public const short LighthouseIslandIndex = 0;

        public static bool TrySelect(
            IReadOnlyList<IslandPlacement> islands,
            Vector3 origin,
            DebrisKind kind,
            SeaZone unlockedZones,
            Predicate<IslandPlacement> extraFilter,
            out IslandPlacement selected)
        {
            selected = default;
            if (islands == null)
            {
                return false;
            }

            bool isFound = false;
            float bestSqrDistance = float.MaxValue;

            for (int i = 0; i < islands.Count; i++)
            {
                IslandPlacement island = islands[i];
                bool isLighthouse = island.Index == LighthouseIslandIndex;

                if (kind == DebrisKind.Module)
                {
                    if (isLighthouse)
                    {
                        selected = island;
                        return true;
                    }

                    continue;
                }

                if (!isLighthouse && !IsCandidate(island, unlockedZones, extraFilter))
                {
                    continue;
                }

                float dx = island.Position.x - origin.x;
                float dz = island.Position.y - origin.z;
                float sqrDistance = dx * dx + dz * dz;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    selected = island;
                    isFound = true;
                }
            }

            return isFound;
        }

        private static bool IsCandidate(
            IslandPlacement island,
            SeaZone unlockedZones,
            Predicate<IslandPlacement> extraFilter)
        {
            if ((island.Zone & unlockedZones) == SeaZone.None)
            {
                return false;
            }

            return extraFilter == null || extraFilter(island);
        }
    }
}
