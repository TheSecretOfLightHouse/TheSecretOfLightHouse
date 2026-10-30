using System.Collections.Generic;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.MapGeneration.Rules;
using UnityEngine;

namespace Lighthouse.World.Harvest.Rules
{
    public static class HarvestSpotAssigner
    {
        public static bool Assign(
            ResourceCatalog catalog,
            IslandArchetype archetype,
            IReadOnlyList<HarvestSpotCategory> categories,
            SeededRandom random,
            List<SpotAssignment> results)
        {
            return Assign(catalog, archetype, categories, null, random, results);
        }

        public static bool Assign(
            ResourceCatalog catalog,
            IslandArchetype archetype,
            IReadOnlyList<HarvestSpotCategory> categories,
            IReadOnlyList<ResourceDefinition> fixedResources,
            SeededRandom random,
            List<SpotAssignment> results)
        {
            if (results == null)
            {
                return false;
            }

            results.Clear();
            if (catalog == null || archetype == null || categories == null || random == null)
            {
                return false;
            }

            if (fixedResources != null && fixedResources.Count != categories.Count)
            {
                return false;
            }

            List<HarvestCandidate> candidates = HarvestCandidateCollector.Collect(catalog, archetype);
            List<float> weights = new List<float>(candidates.Count);

            for (int i = 0; i < categories.Count; i++)
            {
                weights.Clear();
                for (int j = 0; j < candidates.Count; j++)
                {
                    weights.Add(candidates[j].Category == categories[i] ? candidates[j].Weight : 0f);
                }

                int picked = WeightedChoice.Pick(random, weights);
                float roll = random.NextFloat();

                ResourceDefinition fixedResource = fixedResources != null ? fixedResources[i] : null;
                if (fixedResource != null)
                {
                    results.Add(CreateFixedAssignment(fixedResource, candidates, archetype.SeedVariance, roll));
                    continue;
                }

                results.Add(picked >= 0 ? CreateAssignment(candidates[picked], archetype.SeedVariance, roll) : default);
            }

            return true;
        }

        private static SpotAssignment CreateFixedAssignment(
            ResourceDefinition resource,
            List<HarvestCandidate> candidates,
            float seedVariance,
            float roll)
        {
            int index = HarvestCandidateCollector.IndexOfResource(candidates, resource.Id);
            if (index >= 0)
            {
                return CreateAssignment(candidates[index], seedVariance, roll);
            }

            if (!resource.DefaultDeposit.IsValid)
            {
                return default;
            }

            HarvestCandidate fallback = new HarvestCandidate(
                resource.SpotCategory, resource.Id, 1f, resource.DefaultDeposit, resource.PickupPrefab);
            return CreateAssignment(fallback, seedVariance, roll);
        }

        private static SpotAssignment CreateAssignment(HarvestCandidate candidate, float seedVariance, float roll)
        {
            float factor = 1f + (roll * 2f - 1f) * seedVariance;
            int initial = Mathf.Clamp(Mathf.RoundToInt(candidate.Spec.InitialAmount * factor), 0, candidate.Spec.MaxAmount);

            return new SpotAssignment(
                candidate.ResourceId,
                initial,
                candidate.Spec.MaxAmount,
                candidate.Spec.DailyRegenRate,
                candidate.PickupPrefab);
        }
    }
}
