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
            if (results == null)
            {
                return false;
            }

            results.Clear();
            if (catalog == null || archetype == null || categories == null || random == null)
            {
                return false;
            }

            List<Candidate> candidates = CollectCandidates(catalog, archetype);
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
                results.Add(picked >= 0 ? CreateAssignment(candidates[picked], archetype.SeedVariance, roll) : default);
            }

            return true;
        }

        private static List<Candidate> CollectCandidates(ResourceCatalog catalog, IslandArchetype archetype)
        {
            List<Candidate> candidates = new List<Candidate>();
            IReadOnlyList<IslandResourceEntry> entries = archetype.Resources;

            for (int i = 0; i < entries.Count; i++)
            {
                IslandResourceEntry entry = entries[i];
                if (entry == null || entry.Resource == null || entry.Weight <= 0f || !entry.Deposit.IsValid
                    || entry.Resource.SpotCategory == HarvestSpotCategory.None || ContainsId(candidates, entry.Resource.Id))
                {
                    continue;
                }

                candidates.Add(new Candidate(entry.Resource.SpotCategory, entry.Resource.Id, entry.Weight, entry.Deposit));
            }

            List<ResourceDefinition> sorted = new List<ResourceDefinition>();
            catalog.CollectSortedById(sorted);

            for (int i = 0; i < sorted.Count; i++)
            {
                ResourceDefinition resource = sorted[i];
                if (!resource.IsUbiquitous || !resource.DefaultDeposit.IsValid
                    || resource.SpotCategory == HarvestSpotCategory.None || ContainsId(candidates, resource.Id))
                {
                    continue;
                }

                candidates.Add(new Candidate(resource.SpotCategory, resource.Id, 1f, resource.DefaultDeposit));
            }

            return candidates;
        }

        private static SpotAssignment CreateAssignment(Candidate candidate, float seedVariance, float roll)
        {
            float factor = 1f + (roll * 2f - 1f) * seedVariance;
            int initial = Mathf.Clamp(Mathf.RoundToInt(candidate.Spec.InitialAmount * factor), 0, candidate.Spec.MaxAmount);

            return new SpotAssignment(candidate.ResourceId, initial, candidate.Spec.MaxAmount, candidate.Spec.DailyRegenRate);
        }

        private static bool ContainsId(List<Candidate> candidates, int resourceId)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].ResourceId == resourceId)
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct Candidate
        {
            public readonly HarvestSpotCategory Category;
            public readonly int ResourceId;
            public readonly float Weight;
            public readonly DepositSpec Spec;

            public Candidate(HarvestSpotCategory category, int resourceId, float weight, DepositSpec spec)
            {
                Category = category;
                ResourceId = resourceId;
                Weight = weight;
                Spec = spec;
            }
        }
    }
}
