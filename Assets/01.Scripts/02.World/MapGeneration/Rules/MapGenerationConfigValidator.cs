using System.Collections.Generic;
using Lighthouse.World.Islands.Rules;

namespace Lighthouse.World.MapGeneration.Rules
{
    public static class MapGenerationConfigValidator
    {
        public static bool TryValidate(MapGenerationConfig config, List<string> errors)
        {
            errors.Clear();
            if (config == null)
            {
                errors.Add("MapGenerationConfig 없음");
                return false;
            }

            if (config.RadiusScale <= 0f)
            {
                errors.Add("RadiusScale은 0보다 커야 함");
            }

            if (config.MaxBuildAttempts < 1)
            {
                errors.Add("MaxBuildAttempts는 1 이상이어야 함");
            }

            ResourceContentValidator.Validate(config.Resources, errors);
            ArchetypeContentValidator.Validate(config, errors);
            ZoneConfigValidator.Validate(config, errors);
            ValidateQuotas(config, errors);
            RouteAndFloorValidator.Validate(config, errors);
            SameTypeDistanceValidator.Validate(config, errors);
            return errors.Count == 0;
        }

        private static void ValidateQuotas(MapGenerationConfig config, List<string> errors)
        {
            int totalMin = 0;
            int totalZoneMax = 0;
            int totalZoneMin = 0;
            int limitedCapacity = 0;
            bool hasUnlimited = false;

            for (int i = 0; i < config.Zones.Count; i++)
            {
                totalZoneMax += config.Zones[i] != null ? config.Zones[i].MaxIslands : 0;
                totalZoneMin += config.Zones[i] != null ? config.Zones[i].MinIslands : 0;
            }

            for (int i = 0; i < config.Archetypes.Count; i++)
            {
                IslandArchetype archetype = config.Archetypes[i];
                if (archetype == null)
                {
                    continue;
                }

                totalMin += archetype.MinPerMap;
                hasUnlimited |= !archetype.HasMaxLimit && archetype.Weight > 0f;
                limitedCapacity += archetype.HasMaxLimit ? archetype.MaxPerMap : 0;

                if (archetype.MinPerMap > 0 && !IsAnyZoneAllowed(archetype, config.Zones))
                {
                    errors.Add($"Archetype({archetype.name}): 최소 수량이 있는데 설정된 구역에 등장할 수 없음");
                }
            }

            if (totalMin > totalZoneMax)
            {
                errors.Add($"아키타입 최소 수량 합({totalMin}) > 구역 최대 섬 수 합({totalZoneMax})");
            }

            if (!hasUnlimited && limitedCapacity < totalZoneMin)
            {
                errors.Add($"수량 제한 없는 아키타입이 없고 최대 수량 합({limitedCapacity}) < 구역 최소 섬 수 합({totalZoneMin})");
            }
        }

        private static bool IsAnyZoneAllowed(IslandArchetype archetype, IReadOnlyList<ZoneConfig> zones)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] != null && archetype.AllowsZone(zones[i].Zone))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
