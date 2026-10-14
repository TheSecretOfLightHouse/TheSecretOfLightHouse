using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.Ocean.Rules;

namespace Lighthouse.World.MapGeneration.Rules
{
    public static class ArchetypeContentValidator
    {
        public static void Validate(MapGenerationConfig config, List<string> errors)
        {
            HashSet<int> ids = new HashSet<int>();
            if (config.LighthouseIsland == null)
            {
                errors.Add("LighthouseIsland 없음");
            }
            else
            {
                ValidateOne(config.LighthouseIsland, "LighthouseIsland", config.Resources, ids, true, errors);
            }

            if (config.Archetypes.Count == 0)
            {
                errors.Add("Archetypes 비어 있음");
            }

            for (int i = 0; i < config.Archetypes.Count; i++)
            {
                if (config.Archetypes[i] == null)
                {
                    errors.Add($"Archetypes[{i}] 비어 있음");
                    continue;
                }

                ValidateOne(config.Archetypes[i], $"Archetypes[{i}]", config.Resources, ids, false, errors);
            }
        }

        private static void ValidateOne(IslandArchetype archetype, string label, ResourceCatalog catalog, HashSet<int> ids, bool isLighthouse, List<string> errors)
        {
            string tag = $"{label}({archetype.name})";
            if (archetype.Id <= 0)
            {
                errors.Add($"{tag}: Id는 1 이상이어야 함");
            }
            else if (!ids.Add(archetype.Id))
            {
                errors.Add($"{tag}: Id {archetype.Id} 중복");
            }

            if (archetype.Radius <= 0f)
            {
                errors.Add($"{tag}: Radius는 0보다 커야 함");
            }

            if (!isLighthouse)
            {
                ValidateQuota(archetype, tag, errors);
            }

            ValidateVariants(archetype, tag, errors);
            ValidateResources(archetype, tag, catalog, errors);
        }

        private static void ValidateQuota(IslandArchetype archetype, string tag, List<string> errors)
        {
            if (archetype.Zones == SeaZone.None)
            {
                errors.Add($"{tag}: 등장 구역 없음");
            }

            if (archetype.HasMaxLimit && archetype.MaxPerMap < archetype.MinPerMap)
            {
                errors.Add($"{tag}: MaxPerMap({archetype.MaxPerMap}) < MinPerMap({archetype.MinPerMap})");
            }
        }

        private static void ValidateVariants(IslandArchetype archetype, string tag, List<string> errors)
        {
            if (archetype.Variants.Count == 0)
            {
                errors.Add($"{tag}: Variants 비어 있음");
            }

            for (int i = 0; i < archetype.Variants.Count; i++)
            {
                IslandVariant variant = archetype.Variants[i];
                if (variant == null || variant.Prefab == null || variant.Weight <= 0f)
                {
                    errors.Add($"{tag}: Variants[{i}] 프리팹 없음 또는 가중치 0 이하");
                }
            }
        }

        private static void ValidateResources(IslandArchetype archetype, string tag, ResourceCatalog catalog, List<string> errors)
        {
            for (int i = 0; i < archetype.Resources.Count; i++)
            {
                IslandResourceEntry entry = archetype.Resources[i];
                bool isFound = entry != null
                    && entry.Resource != null
                    && catalog != null
                    && catalog.TryGetById(entry.Resource.Id, out ResourceDefinition registered)
                    && registered == entry.Resource;

                if (!isFound)
                {
                    errors.Add($"{tag}: Resources[{i}]가 비었거나 ResourceCatalog에 없음");
                }
                else if (entry.Weight <= 0f || !entry.Deposit.IsValid)
                {
                    errors.Add($"{tag}: Resources[{i}] 가중치 또는 매장량이 올바르지 않음");
                }
            }
        }
    }
}
