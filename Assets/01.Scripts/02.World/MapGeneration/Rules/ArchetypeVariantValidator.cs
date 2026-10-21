using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Rules;

namespace Lighthouse.World.MapGeneration.Rules
{
    public static class ArchetypeVariantValidator
    {
        public static void Validate(IslandArchetype archetype, string tag, List<string> errors)
        {
            IReadOnlyList<IslandVariant> variants = archetype.Variants;
            if (variants.Count == 0)
            {
                errors.Add($"{tag}: Variants 비어 있음");
            }

            if (variants.Count > byte.MaxValue)
            {
                errors.Add($"{tag}: Variants는 {byte.MaxValue}개 이하여야 함");
            }

            List<HarvestSpot> spots = new List<HarvestSpot>();
            for (int i = 0; i < variants.Count; i++)
            {
                IslandVariant variant = variants[i];
                if (variant == null || variant.Prefab == null || variant.Weight <= 0f)
                {
                    errors.Add($"{tag}: Variants[{i}] 프리팹 없음 또는 가중치 0 이하");
                    continue;
                }

                variant.Prefab.GetComponentsInChildren(true, spots);
                ValidateSpecialSpots(archetype, tag, i, spots, errors);
            }
        }

        private static void ValidateSpecialSpots(IslandArchetype archetype, string tag, int variantIndex, List<HarvestSpot> spots, List<string> errors)
        {
            IReadOnlyList<IslandResourceEntry> entries = archetype.Resources;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] == null || entries[i].Resource == null)
                {
                    continue;
                }

                HarvestSpotCategory category = entries[i].Resource.SpotCategory;
                if (category != HarvestSpotCategory.None && !ContainsCategory(spots, category))
                {
                    errors.Add($"{tag}: Variants[{variantIndex}]에 {category} HarvestSpot 없음");
                }
            }
        }

        private static bool ContainsCategory(List<HarvestSpot> spots, HarvestSpotCategory category)
        {
            for (int i = 0; i < spots.Count; i++)
            {
                if (spots[i].Category == category)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
