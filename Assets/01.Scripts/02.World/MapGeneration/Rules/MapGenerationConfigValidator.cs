using System.Collections.Generic;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.Ocean.Rules;

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
            ValidateZones(config, errors);
            ValidateQuotas(config, errors);
            RouteAndFloorValidator.Validate(config, errors);
            SameTypeDistanceValidator.Validate(config, errors);
            return errors.Count == 0;
        }

        private static void ValidateZones(MapGenerationConfig config, List<string> errors)
        {
            IReadOnlyList<ZoneConfig> zones = config.Zones;
            if (zones.Count == 0)
            {
                errors.Add("Zones 비어 있음");
                return;
            }

            float maxRadius = GetMaxArchetypeRadius(config);
            float lighthouseRadius = config.LighthouseIsland != null ? config.LighthouseIsland.Radius : 0f;
            SeaZone usedZones = SeaZone.None;

            for (int i = 0; i < zones.Count; i++)
            {
                ZoneConfig zone = zones[i];
                if (zone == null)
                {
                    errors.Add($"Zones[{i}] 비어 있음");
                    continue;
                }

                string tag = $"Zones[{i}]({zone.Zone})";
                ValidateZoneFlag(zone, tag, usedZones, errors);
                usedZones |= zone.Zone;
                ValidateZoneShape(zone, tag, config.RadiusScale, maxRadius, errors);

                if (i == 0 && zone.InnerRadius * config.RadiusScale < lighthouseRadius + maxRadius)
                {
                    errors.Add($"{tag}: 안쪽 반경이 등대 섬 반경 + 최대 섬 반경보다 작음");
                }

                if (i > 0 && zones[i - 1] != null && zone.InnerRadius < zones[i - 1].OuterRadius)
                {
                    errors.Add($"{tag}: 안쪽 반경이 이전 구역 바깥 반경보다 작음");
                }
            }
        }

        private static void ValidateZoneFlag(ZoneConfig zone, string tag, SeaZone usedZones, List<string> errors)
        {
            int value = (int)zone.Zone;
            bool isSingleFlag = value != 0 && (value & (value - 1)) == 0 && zone.Zone != SeaZone.All;
            if (!isSingleFlag)
            {
                errors.Add($"{tag}: Zone은 Inner, Middle, Outer 중 하나여야 함");
            }
            else if ((usedZones & zone.Zone) != 0)
            {
                errors.Add($"{tag}: Zone 중복");
            }
        }

        private static void ValidateZoneShape(ZoneConfig zone, string tag, float scale, float maxRadius, List<string> errors)
        {
            if (zone.OuterRadius <= zone.InnerRadius)
            {
                errors.Add($"{tag}: 바깥 반경이 안쪽 반경보다 커야 함");
            }

            if (zone.MaxIslands < zone.MinIslands || zone.MaxReefs < zone.MinReefs)
            {
                errors.Add($"{tag}: 최대 < 최소");
            }

            if (zone.MaxIslands > 0 && zone.MinIslandDistance < maxRadius * 2f)
            {
                errors.Add($"{tag}: MinIslandDistance가 최대 섬 반경 × 2({maxRadius * 2f})보다 작음");
            }

            if (zone.MaxReefs > 0 && zone.MinReefDistance <= 0f)
            {
                errors.Add($"{tag}: MinReefDistance는 0보다 커야 함");
            }
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

        private static float GetMaxArchetypeRadius(MapGenerationConfig config)
        {
            float maxRadius = 0f;
            for (int i = 0; i < config.Archetypes.Count; i++)
            {
                if (config.Archetypes[i] != null && config.Archetypes[i].Radius > maxRadius)
                {
                    maxRadius = config.Archetypes[i].Radius;
                }
            }

            return maxRadius;
        }
    }
}
