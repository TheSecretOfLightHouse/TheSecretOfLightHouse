using System.Collections.Generic;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Sandbox
{
    public static class MapLayoutRuleChecks
    {
        private const float Epsilon = 0.01f;

        public static bool CheckQuotas(MapGenerationConfig config, IReadOnlyList<MapBuildResult> results)
        {
            int violations = 0;
            for (int r = 0; r < results.Count; r++)
            {
                MapLayout layout = results[r].Layout;
                if (layout == null)
                {
                    violations++;
                    continue;
                }

                violations += CountArchetypeViolations(config, layout) + CountZoneViolations(config, layout);
            }

            bool isPassed = violations == 0;
            MapCheckLog.Report("Quotas (archetype and zone counts)", isPassed, $"violations={violations} over {results.Count} layouts");
            return isPassed;
        }

        public static bool CheckSameTypeSpacing(MapGenerationConfig config, IReadOnlyList<MapBuildResult> results)
        {
            int violations = 0;
            for (int r = 0; r < results.Count; r++)
            {
                MapLayout layout = results[r].Layout;
                violations += layout == null ? 1 : CountSameTypeViolations(config, layout);
            }

            bool isPassed = violations == 0;
            MapCheckLog.Report("Same-type spacing", isPassed, $"violations={violations} over {results.Count} layouts");
            return isPassed;
        }

        private static int CountSameTypeViolations(MapGenerationConfig config, MapLayout layout)
        {
            int violations = 0;
            for (int i = 0; i < layout.Islands.Count; i++)
            {
                for (int j = i + 1; j < layout.Islands.Count; j++)
                {
                    IslandPlacement a = layout.Islands[i];
                    IslandPlacement b = layout.Islands[j];
                    if (a.ArchetypeId != b.ArchetypeId || !config.TryGetArchetype(a.ArchetypeId, out IslandArchetype archetype))
                    {
                        continue;
                    }

                    float minDistance = archetype.MinSameTypeDistance;
                    violations += minDistance > 0f && (a.Position - b.Position).magnitude < minDistance - Epsilon ? 1 : 0;
                }
            }

            return violations;
        }

        public static bool CheckGeometry(MapGenerationConfig config, IReadOnlyList<MapBuildResult> results)
        {
            int indexErrors = 0;
            int ringErrors = 0;
            int islandOverlaps = 0;
            int reefErrors = 0;
            for (int r = 0; r < results.Count; r++)
            {
                MapLayout layout = results[r].Layout;
                if (layout == null)
                {
                    indexErrors++;
                    continue;
                }

                indexErrors += CountIndexErrors(layout);
                ringErrors += CountRingErrors(config, layout);
                islandOverlaps += CountIslandOverlaps(layout, config.SailingChannelWidth);
                reefErrors += CountReefErrors(config, layout);
            }

            bool isPassed = indexErrors + ringErrors + islandOverlaps + reefErrors == 0;
            MapCheckLog.Report("Geometry", isPassed, $"index={indexErrors}, ring={ringErrors}, islandOverlap={islandOverlaps}, reef={reefErrors}");
            return isPassed;
        }

        private static int CountArchetypeViolations(MapGenerationConfig config, MapLayout layout)
        {
            Dictionary<int, int> counts = new Dictionary<int, int>();
            for (int i = 0; i < layout.Islands.Count; i++)
            {
                counts.TryGetValue(layout.Islands[i].ArchetypeId, out int current);
                counts[layout.Islands[i].ArchetypeId] = current + 1;
            }

            int violations = 0;
            for (int i = 0; i < config.Archetypes.Count; i++)
            {
                IslandArchetype archetype = config.Archetypes[i];
                counts.TryGetValue(archetype.Id, out int count);
                if (count < archetype.MinPerMap || (archetype.HasMaxLimit && count > archetype.MaxPerMap))
                {
                    violations++;
                }
            }

            return violations;
        }

        private static int CountZoneViolations(MapGenerationConfig config, MapLayout layout)
        {
            int violations = 0;
            for (int z = 0; z < config.Zones.Count; z++)
            {
                ZoneConfig zone = config.Zones[z];
                int count = 0;
                for (int i = 0; i < layout.Islands.Count; i++)
                {
                    count += layout.Islands[i].Zone == zone.Zone ? 1 : 0;
                }

                if (count < zone.MinIslands || count > zone.MaxIslands)
                {
                    violations++;
                }
            }

            return violations;
        }

        private static int CountIndexErrors(MapLayout layout)
        {
            int errors = 0;
            for (int i = 0; i < layout.Islands.Count; i++)
            {
                errors += layout.Islands[i].Index == i ? 0 : 1;
            }

            bool isLighthouseValid = layout.Islands.Count > 0 && layout.Islands[0].Position == Vector2.zero && layout.Islands[0].Zone == SeaZone.None;
            return errors + (isLighthouseValid ? 0 : 1);
        }

        private static int CountRingErrors(MapGenerationConfig config, MapLayout layout)
        {
            float halfChannel = config.SailingChannelWidth * 0.5f;
            int errors = 0;
            for (int i = 1; i < layout.Islands.Count; i++)
            {
                IslandPlacement island = layout.Islands[i];
                errors += IsInsideZone(config, island.Zone, island.Position, island.Radius + halfChannel) ? 0 : 1;
            }

            for (int i = 0; i < layout.Reefs.Count; i++)
            {
                ReefPlacement reef = layout.Reefs[i];
                errors += IsInsideZone(config, reef.Zone, reef.Position, reef.Radius + halfChannel) ? 0 : 1;
            }

            return errors;
        }

        private static bool IsInsideZone(MapGenerationConfig config, SeaZone zoneId, Vector2 position, float margin)
        {
            ZoneConfig zone = FindZone(config, zoneId);
            if (zone == null)
            {
                return false;
            }

            float distance = position.magnitude;
            return distance >= zone.InnerRadius * config.RadiusScale + margin - Epsilon
                && distance <= zone.OuterRadius * config.RadiusScale - margin + Epsilon;
        }

        private static int CountIslandOverlaps(MapLayout layout, float channelWidth)
        {
            int overlaps = 0;
            for (int i = 0; i < layout.Islands.Count; i++)
            {
                for (int j = i + 1; j < layout.Islands.Count; j++)
                {
                    float reach = layout.Islands[i].Radius + layout.Islands[j].Radius + channelWidth;
                    float distance = (layout.Islands[i].Position - layout.Islands[j].Position).magnitude;
                    overlaps += distance < reach - Epsilon ? 1 : 0;
                }
            }

            return overlaps;
        }

        private static int CountReefErrors(MapGenerationConfig config, MapLayout layout)
        {
            int errors = 0;
            for (int z = 0; z < config.Zones.Count; z++)
            {
                ZoneConfig zone = config.Zones[z];
                int count = 0;
                for (int i = 0; i < layout.Reefs.Count; i++)
                {
                    count += layout.Reefs[i].Zone == zone.Zone ? 1 : 0;
                }

                errors += count < zone.MinReefs || count > zone.MaxReefs ? 1 : 0;
            }

            for (int i = 0; i < layout.Reefs.Count; i++)
            {
                errors += CountReefOverlaps(layout, i);
            }

            return errors;
        }

        private static int CountReefOverlaps(MapLayout layout, int reefIndex)
        {
            ReefPlacement reef = layout.Reefs[reefIndex];
            int errors = 0;
            for (int i = 0; i < layout.Islands.Count; i++)
            {
                float reach = layout.Islands[i].Radius + reef.Radius;
                errors += (reef.Position - layout.Islands[i].Position).magnitude < reach - Epsilon ? 1 : 0;
            }

            return errors;
        }

        private static ZoneConfig FindZone(MapGenerationConfig config, SeaZone zone)
        {
            for (int i = 0; i < config.Zones.Count; i++)
            {
                if (config.Zones[i].Zone == zone)
                {
                    return config.Zones[i];
                }
            }

            return null;
        }
    }
}
