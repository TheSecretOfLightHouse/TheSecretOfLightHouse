using System.Collections.Generic;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.MapGeneration.Rules;

namespace Lighthouse.World.Harvest.Rules
{
    public static class WorldDepositPlanner
    {
        public static bool TryBuild(
            MapGenerationConfig config,
            IReadOnlyList<IslandSpawnPlan> plans,
            SeededRandom mapRandom,
            IReadOnlyDictionary<int, int> totals,
            List<List<SpotAssignment>> results)
        {
            if (results == null)
            {
                return false;
            }

            results.Clear();
            if (config == null || plans == null || mapRandom == null)
            {
                return false;
            }

            List<HarvestSpot> spots = new List<HarvestSpot>();
            for (int i = 0; i < plans.Count; i++)
            {
                IslandSpawnPlan plan = plans[i];
                List<SpotAssignment> assignments = new List<SpotAssignment>();
                results.Add(assignments);

                if (plan.Index == 0)
                {
                    continue;
                }

                if (plan.Prefab == null)
                {
                    return false;
                }

                plan.Prefab.GetComponentsInChildren(true, spots);
                if (spots.Count > 0 && !HarvestIslandSetup.TryAssign(config.Resources, plan.Archetype, plan.Index, mapRandom, spots, assignments))
                {
                    return false;
                }
            }

            DepositDistributor.Apply(totals, results);
            return true;
        }
    }
}
