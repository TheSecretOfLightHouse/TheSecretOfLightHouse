using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.MapGeneration.Rules;

namespace Lighthouse.World.Harvest.Sandbox
{
    public static class HarvestFixedAssignChecks
    {
        private const int CheckSeed = 777;
        private const int FixedSpotIndex = 0;

        public static bool RunAll(ResourceCatalog catalog, IslandArchetype archetype)
        {
            List<HarvestSpotCategory> categories = BuildCategories();
            if (!TryPickFixedResource(catalog, categories[FixedSpotIndex], out ResourceDefinition fixedResource))
            {
                HarvestDepositChecks.Report("Assigner fixed resource", false, "no resource with a different category and a valid deposit");
                return false;
            }

            List<ResourceDefinition> fixedResources = new List<ResourceDefinition>(categories.Count);
            for (int i = 0; i < categories.Count; i++)
            {
                fixedResources.Add(i == FixedSpotIndex ? fixedResource : null);
            }

            List<SpotAssignment> baseline = new List<SpotAssignment>();
            List<SpotAssignment> applied = new List<SpotAssignment>();
            List<SpotAssignment> repeated = new List<SpotAssignment>();
            bool isOk = Assign(catalog, archetype, categories, null, baseline)
                && Assign(catalog, archetype, categories, fixedResources, applied)
                && Assign(catalog, archetype, categories, fixedResources, repeated);

            bool isFixedOk = isOk && applied[FixedSpotIndex].ResourceId == fixedResource.Id
                && applied[FixedSpotIndex].InitialAmount <= applied[FixedSpotIndex].MaxAmount;
            bool isStableOk = isOk;
            bool isRepeatOk = isOk;
            bool isPickupOk = isOk;

            for (int i = 0; isOk && i < categories.Count; i++)
            {
                isStableOk &= i == FixedSpotIndex || IsEqual(baseline[i], applied[i]);
                isRepeatOk &= IsEqual(applied[i], repeated[i]);
                isPickupOk &= HasExpectedPickup(catalog, archetype, applied[i]);
            }

            HarvestDepositChecks.Report("Assigner fixed resource", isFixedOk, $"spot {FixedSpotIndex} -> resource {fixedResource.Id}");
            HarvestDepositChecks.Report("Assigner fixed leaves others", isStableOk, "non-fixed spots equal the baseline stream");
            HarvestDepositChecks.Report("Assigner fixed determinism", isRepeatOk, "same stream twice");
            HarvestDepositChecks.Report("Assigner pickup source", isPickupOk, "entry override, else resource default");
            return isFixedOk && isStableOk && isRepeatOk && isPickupOk;
        }

        private static bool Assign(
            ResourceCatalog catalog,
            IslandArchetype archetype,
            List<HarvestSpotCategory> categories,
            List<ResourceDefinition> fixedResources,
            List<SpotAssignment> results)
        {
            SeededRandom random = new SeededRandom(CheckSeed).Fork((int)MapGenerationStepId.HarvestAssignment).Fork(1);
            return HarvestSpotAssigner.Assign(catalog, archetype, categories, fixedResources, random, results);
        }

        private static List<HarvestSpotCategory> BuildCategories()
        {
            List<HarvestSpotCategory> categories = new List<HarvestSpotCategory>();
            for (int repeat = 0; repeat < 2; repeat++)
            {
                for (int category = (int)HarvestSpotCategory.Rock; category <= (int)HarvestSpotCategory.Crystal; category++)
                {
                    categories.Add((HarvestSpotCategory)category);
                }
            }

            return categories;
        }

        private static bool TryPickFixedResource(ResourceCatalog catalog, HarvestSpotCategory spotCategory, out ResourceDefinition resource)
        {
            for (int i = 0; i < catalog.Count; i++)
            {
                ResourceDefinition candidate = catalog[i];
                if (candidate != null && candidate.SpotCategory != spotCategory && candidate.DefaultDeposit.IsValid)
                {
                    resource = candidate;
                    return true;
                }
            }

            resource = null;
            return false;
        }

        private static bool IsEqual(SpotAssignment a, SpotAssignment b)
        {
            return a.ResourceId == b.ResourceId && a.InitialAmount == b.InitialAmount
                && a.MaxAmount == b.MaxAmount && a.DailyRegenRate == b.DailyRegenRate
                && a.PickupPrefab == b.PickupPrefab;
        }

        private static bool HasExpectedPickup(ResourceCatalog catalog, IslandArchetype archetype, SpotAssignment assignment)
        {
            if (!assignment.IsAssigned)
            {
                return true;
            }

            IReadOnlyList<IslandResourceEntry> entries = archetype.Resources;
            for (int i = 0; i < entries.Count; i++)
            {
                IslandResourceEntry entry = entries[i];
                if (entry != null && entry.Resource != null && entry.Resource.Id == assignment.ResourceId && entry.Weight > 0f && entry.Deposit.IsValid)
                {
                    return assignment.PickupPrefab == (entry.PickupPrefab != null ? entry.PickupPrefab : entry.Resource.PickupPrefab);
                }
            }

            return catalog.TryGetById(assignment.ResourceId, out ResourceDefinition resource)
                && assignment.PickupPrefab == resource.PickupPrefab;
        }
    }
}
