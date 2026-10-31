using System.Collections.Generic;
using Lighthouse.World.Islands.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    public static class WorldMapDataBuilder
    {
        public const float DefaultBoundsMargin = 20f;

        public static bool TryBuild(MapLayout layout, MapGenerationConfig config, float boundsMargin, out WorldMapData data)
        {
            data = null;

            if (layout == null || config == null || config.Zones.Count == 0
                || float.IsNaN(boundsMargin) || boundsMargin < 0f)
            {
                return false;
            }

            var zones = new List<MapZoneRing>(config.Zones.Count);
            float extent = 0f;

            for (int i = 0; i < config.Zones.Count; i++)
            {
                ZoneConfig zone = config.Zones[i];
                if (zone == null)
                {
                    return false;
                }

                float inner = zone.InnerRadius * config.RadiusScale;
                float outer = zone.OuterRadius * config.RadiusScale;
                zones.Add(new MapZoneRing(zone.Zone, inner, outer));
                extent = Mathf.Max(extent, outer);
            }

            var islands = new List<MapIslandMark>(layout.Islands.Count);
            for (int i = 0; i < layout.Islands.Count; i++)
            {
                IslandPlacement placement = layout.Islands[i];
                bool hasArchetype = config.TryGetArchetype(placement.ArchetypeId, out IslandArchetype archetype);
                if (!hasArchetype || placement.VariantIndex >= archetype.Variants.Count
                    || archetype.Variants[placement.VariantIndex] == null
                    || !IslandRotationRules.IsValidYaw(archetype.Variants[placement.VariantIndex].Prefab, placement.YawDegrees))
                {
                    return false;
                }

                bool isLighthouse = config.LighthouseIsland != null
                    && config.LighthouseIsland.Id == placement.ArchetypeId;

                islands.Add(new MapIslandMark(placement.Index, placement.ArchetypeId,
                    hasArchetype ? archetype.DisplayName : string.Empty, isLighthouse,
                    placement.Zone, placement.Position, placement.Radius, placement.YawDegrees));

                extent = Mathf.Max(extent, placement.Position.magnitude + placement.Radius);
            }

            for (int i = 0; i < layout.Reefs.Count; i++)
            {
                ReefPlacement reef = layout.Reefs[i];
                extent = Mathf.Max(extent, reef.Position.magnitude + reef.Radius);
            }

            float half = extent + boundsMargin;
            if (half <= 0f || float.IsNaN(half) || float.IsInfinity(half))
            {
                return false;
            }

            data = new WorldMapData(layout.Seed, layout.Hash, new Vector2(-half, -half), new Vector2(half, half),
                zones, islands, layout.Reefs);
            return true;
        }
    }
}
