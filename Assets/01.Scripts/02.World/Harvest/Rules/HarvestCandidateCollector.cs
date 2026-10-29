using System.Collections.Generic;
using Lighthouse.World.Islands.Rules;
using UnityEngine;

namespace Lighthouse.World.Harvest.Rules
{
    public static class HarvestCandidateCollector
    {
        public static List<HarvestCandidate> Collect(ResourceCatalog catalog, IslandArchetype archetype)
        {
            List<HarvestCandidate> candidates = new List<HarvestCandidate>();
            IReadOnlyList<IslandResourceEntry> entries = archetype.Resources;

            for (int i = 0; i < entries.Count; i++)
            {
                IslandResourceEntry entry = entries[i];
                if (entry == null || entry.Resource == null || entry.Weight <= 0f || !entry.Deposit.IsValid
                    || entry.Resource.SpotCategory == HarvestSpotCategory.None || IndexOfResource(candidates, entry.Resource.Id) >= 0)
                {
                    continue;
                }

                GameObject pickup = entry.PickupPrefab != null ? entry.PickupPrefab : entry.Resource.PickupPrefab;
                candidates.Add(new HarvestCandidate(entry.Resource.SpotCategory, entry.Resource.Id, entry.Weight, entry.Deposit, pickup));
            }

            List<ResourceDefinition> sorted = new List<ResourceDefinition>();
            catalog.CollectSortedById(sorted);

            for (int i = 0; i < sorted.Count; i++)
            {
                ResourceDefinition resource = sorted[i];
                if (!resource.IsUbiquitous || !resource.DefaultDeposit.IsValid
                    || resource.SpotCategory == HarvestSpotCategory.None || IndexOfResource(candidates, resource.Id) >= 0)
                {
                    continue;
                }

                candidates.Add(new HarvestCandidate(resource.SpotCategory, resource.Id, 1f, resource.DefaultDeposit, resource.PickupPrefab));
            }

            return candidates;
        }

        public static int IndexOfResource(IReadOnlyList<HarvestCandidate> candidates, int resourceId)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].ResourceId == resourceId)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
