using System.Collections.Generic;
using Lighthouse.World.Ocean.Rules;

namespace Lighthouse.World.MapGeneration.Rules
{
    public static class ZoneConfigValidator
    {
        public static void Validate(MapGenerationConfig config, List<string> errors)
        {
            IReadOnlyList<ZoneConfig> zones = config.Zones;
            if (zones.Count == 0)
            {
                errors.Add("Zones 비어 있음");
                return;
            }

            if (config.SailingChannelWidth < config.ShipClearance * 2f)
            {
                errors.Add($"SailingChannelWidth({config.SailingChannelWidth})가 ShipClearance × 2({config.ShipClearance * 2f})보다 작음");
            }

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
                ValidateFlag(zone, tag, usedZones, errors);
                usedZones |= zone.Zone;
                ValidateShape(config, zone, tag, errors);

                if (i == 0 && zone.InnerRadius * config.RadiusScale < lighthouseRadius + config.MaxIslandRadius)
                {
                    errors.Add($"{tag}: 안쪽 반경이 등대 섬 반경 + 최대 섬 반경보다 작음");
                }

                if (i > 0 && zones[i - 1] != null && zone.InnerRadius < zones[i - 1].OuterRadius)
                {
                    errors.Add($"{tag}: 안쪽 반경이 이전 구역 바깥 반경보다 작음");
                }
            }
        }

        private static void ValidateFlag(ZoneConfig zone, string tag, SeaZone usedZones, List<string> errors)
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

        private static void ValidateShape(MapGenerationConfig config, ZoneConfig zone, string tag, List<string> errors)
        {
            float width = (zone.OuterRadius - zone.InnerRadius) * config.RadiusScale;
            if (zone.OuterRadius <= zone.InnerRadius)
            {
                errors.Add($"{tag}: 바깥 반경이 안쪽 반경보다 커야 함");
            }

            if (zone.MaxIslands < zone.MinIslands || zone.MaxReefs < zone.MinReefs)
            {
                errors.Add($"{tag}: 최대 < 최소");
            }

            if (zone.MaxIslands > 0 && width <= config.IslandZoneMargin * 2f)
            {
                errors.Add($"{tag}: 구역 폭({width})이 섬 마진 × 2({config.IslandZoneMargin * 2f}) 이하");
            }

            if (zone.MaxReefs > 0 && zone.MinReefDistance <= 0f)
            {
                errors.Add($"{tag}: MinReefDistance는 0보다 커야 함");
            }

            if (zone.MaxReefs > 0 && width <= config.GetReefZoneMargin(zone.ReefRadius) * 2f)
            {
                errors.Add($"{tag}: 구역 폭({width})이 암초 마진 × 2 이하");
            }
        }
    }
}
