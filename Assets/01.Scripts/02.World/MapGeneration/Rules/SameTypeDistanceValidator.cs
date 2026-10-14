using System.Collections.Generic;
using Lighthouse.World.Islands.Rules;

namespace Lighthouse.World.MapGeneration.Rules
{
    public static class SameTypeDistanceValidator
    {
        public static void Validate(MapGenerationConfig config, List<string> errors)
        {
            for (int i = 0; i < config.Archetypes.Count; i++)
            {
                IslandArchetype archetype = config.Archetypes[i];
                if (archetype == null)
                {
                    continue;
                }

                if (archetype.MinSameTypeDistance < 0f)
                {
                    errors.Add($"Archetype({archetype.name}): MinSameTypeDistance는 0 이상이어야 함");
                }

                float widest = GetWidestSpan(config, archetype);
                if (archetype.MinPerMap >= 2 && archetype.MinSameTypeDistance > widest)
                {
                    errors.Add($"Archetype({archetype.name}): MinSameTypeDistance({archetype.MinSameTypeDistance})가 등장 구역의 최대 지름({widest})보다 커서 최소 수량을 배치할 수 없음");
                }
            }
        }

        private static float GetWidestSpan(MapGenerationConfig config, IslandArchetype archetype)
        {
            float widest = 0f;
            for (int i = 0; i < config.Zones.Count; i++)
            {
                ZoneConfig zone = config.Zones[i];
                if (zone != null && archetype.AllowsZone(zone.Zone))
                {
                    widest = System.Math.Max(widest, zone.OuterRadius * config.RadiusScale * 2f);
                }
            }

            return widest;
        }
    }
}
