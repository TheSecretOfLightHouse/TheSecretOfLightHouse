using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Rules;

namespace Lighthouse.World.MapGeneration.Rules
{
    public static class MapContentHasher
    {
        public static uint Compute(MapGenerationConfig config)
        {
            StableHash hash = new StableHash();
            if (config == null)
            {
                return hash.Value;
            }

            hash.Add(config.RadiusScale);
            hash.Add(config.MaxBuildAttempts);
            AppendResources(hash, config.Resources);
            AppendArchetypes(hash, config);
            AppendZones(hash, config.Zones);
            return hash.Value;
        }

        private static void AppendResources(StableHash hash, ResourceCatalog catalog)
        {
            List<ResourceDefinition> sorted = new List<ResourceDefinition>();
            if (catalog != null)
            {
                catalog.CollectSortedById(sorted);
            }

            hash.Add(sorted.Count);
            for (int i = 0; i < sorted.Count; i++)
            {
                hash.Add(sorted[i].Id);
                hash.Add((int)sorted[i].Rarity);
                hash.Add((int)sorted[i].SpotCategory);
                hash.Add(sorted[i].IsUbiquitous ? 1 : 0);
                AppendDeposit(hash, sorted[i].DefaultDeposit);
            }
        }

        private static void AppendArchetypes(StableHash hash, MapGenerationConfig config)
        {
            List<IslandArchetype> sorted = new List<IslandArchetype>();
            if (config.LighthouseIsland != null)
            {
                sorted.Add(config.LighthouseIsland);
            }

            for (int i = 0; i < config.Archetypes.Count; i++)
            {
                if (config.Archetypes[i] != null)
                {
                    sorted.Add(config.Archetypes[i]);
                }
            }

            sorted.Sort(CompareById);
            hash.Add(sorted.Count);
            for (int i = 0; i < sorted.Count; i++)
            {
                AppendArchetype(hash, sorted[i]);
            }
        }

        private static void AppendArchetype(StableHash hash, IslandArchetype archetype)
        {
            hash.Add(archetype.Id);
            hash.Add((int)archetype.Zones);
            hash.Add(archetype.Weight);
            hash.Add(archetype.MinPerMap);
            hash.Add(archetype.MaxPerMap);
            hash.Add(archetype.Radius);
            hash.Add(archetype.SeedVariance);

            hash.Add(archetype.Variants.Count);
            for (int i = 0; i < archetype.Variants.Count; i++)
            {
                hash.Add(archetype.Variants[i] != null ? archetype.Variants[i].Weight : 0f);
            }

            hash.Add(archetype.Resources.Count);
            for (int i = 0; i < archetype.Resources.Count; i++)
            {
                IslandResourceEntry entry = archetype.Resources[i];
                hash.Add(entry != null && entry.Resource != null ? entry.Resource.Id : 0);
                hash.Add(entry != null ? entry.Weight : 0f);
                AppendDeposit(hash, entry != null ? entry.Deposit : default);
            }
        }

        private static void AppendZones(StableHash hash, IReadOnlyList<ZoneConfig> zones)
        {
            hash.Add(zones.Count);
            for (int i = 0; i < zones.Count; i++)
            {
                ZoneConfig zone = zones[i];
                if (zone == null)
                {
                    hash.Add(0);
                    continue;
                }

                hash.Add((int)zone.Zone);
                hash.Add(zone.InnerRadius);
                hash.Add(zone.OuterRadius);
                hash.Add(zone.MinIslands);
                hash.Add(zone.MaxIslands);
                hash.Add(zone.MinIslandDistance);
                hash.Add(zone.MinReefs);
                hash.Add(zone.MaxReefs);
                hash.Add(zone.MinReefDistance);
            }
        }

        private static void AppendDeposit(StableHash hash, DepositSpec deposit)
        {
            hash.Add(deposit.InitialAmount);
            hash.Add(deposit.MaxAmount);
            hash.Add(deposit.DailyRegenRate);
        }

        private static int CompareById(IslandArchetype a, IslandArchetype b)
        {
            return a.Id.CompareTo(b.Id);
        }
    }
}
