using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.MapGeneration.Rules;

namespace Lighthouse.World.Harvest.Sandbox
{
    public static class HarvestAssignerChecks
    {
        private const int CheckSeed = 777;
        private const int RepeatPerCategory = 3;

        public static bool RunAll(ResourceCatalog catalog, IslandArchetype archetype)
        {
            List<HarvestSpotCategory> categories = BuildCategories();
            List<SpotAssignment> first = new List<SpotAssignment>();
            List<SpotAssignment> second = new List<SpotAssignment>();

            bool isOk = Assign(catalog, archetype, categories, first) && Assign(catalog, archetype, categories, second);
            isOk &= first.Count == categories.Count && second.Count == categories.Count;

            bool isSame = isOk;
            bool isRuleOk = isOk;
            bool isUbiquitousOk = isOk;
            int assigned = 0;

            for (int i = 0; isOk && i < first.Count; i++)
            {
                isSame &= IsEqual(first[i], second[i]);
                assigned += first[i].IsAssigned ? 1 : 0;
                isRuleOk &= IsRuleValid(catalog, first[i], categories[i]);
                isUbiquitousOk &= first[i].IsAssigned || !HasUbiquitous(catalog, categories[i]);
            }

            HarvestDepositChecks.Report("Assigner determinism", isSame, $"same stream twice, spots={first.Count}");
            HarvestDepositChecks.Report("Assigner category rule", isRuleOk, $"assigned={assigned}, category match and amount within max");
            HarvestDepositChecks.Report("Assigner ubiquitous", isUbiquitousOk, "every spot with a ubiquitous resource of its category is assigned");
            return isSame && isRuleOk && isUbiquitousOk;
        }

        private static bool Assign(ResourceCatalog catalog, IslandArchetype archetype, List<HarvestSpotCategory> categories, List<SpotAssignment> results)
        {
            SeededRandom random = new SeededRandom(CheckSeed).Fork((int)MapGenerationStepId.HarvestAssignment).Fork(1);
            return HarvestSpotAssigner.Assign(catalog, archetype, categories, random, results);
        }

        private static List<HarvestSpotCategory> BuildCategories()
        {
            List<HarvestSpotCategory> categories = new List<HarvestSpotCategory>();
            for (int repeat = 0; repeat < RepeatPerCategory; repeat++)
            {
                for (int category = (int)HarvestSpotCategory.Rock; category <= (int)HarvestSpotCategory.Crystal; category++)
                {
                    categories.Add((HarvestSpotCategory)category);
                }
            }

            return categories;
        }

        private static bool IsEqual(SpotAssignment a, SpotAssignment b)
        {
            return a.ResourceId == b.ResourceId && a.InitialAmount == b.InitialAmount
                && a.MaxAmount == b.MaxAmount && a.DailyRegenRate == b.DailyRegenRate;
        }

        private static bool IsRuleValid(ResourceCatalog catalog, SpotAssignment assignment, HarvestSpotCategory category)
        {
            if (!assignment.IsAssigned)
            {
                return true;
            }

            return catalog.TryGetById(assignment.ResourceId, out ResourceDefinition resource)
                && resource.SpotCategory == category
                && assignment.InitialAmount >= 0
                && assignment.InitialAmount <= assignment.MaxAmount;
        }

        private static bool HasUbiquitous(ResourceCatalog catalog, HarvestSpotCategory category)
        {
            for (int i = 0; i < catalog.Count; i++)
            {
                ResourceDefinition resource = catalog[i];
                if (resource != null && resource.IsUbiquitous && resource.SpotCategory == category && resource.DefaultDeposit.IsValid)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
