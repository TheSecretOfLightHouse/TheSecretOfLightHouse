using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.MapGeneration.Rules;

namespace Lighthouse.World.Harvest.Sandbox
{
    public static class WorldDepositPlannerChecks
    {
        private const int CheckSeed = 1000;
        private const int BaseTotal = 1000;

        public static bool RunAll(MapGenerationConfig config)
        {
            MapBuildResult map = MapBuilder.CreateDefault(config).Build(CheckSeed, true);
            List<IslandSpawnPlan> plans = new List<IslandSpawnPlan>();
            List<string> errors = new List<string>();
            if (!map.IsValid || !IslandSpawnPlanner.TryBuild(config, map.Layout, plans, errors))
            {
                HarvestDepositChecks.Report("Planner setup", false, $"valid={map.IsValid}, errors={errors.Count}");
                return false;
            }

            Dictionary<int, int> totals = BuildTotals(config.Resources);
            List<List<SpotAssignment>> first = new List<List<SpotAssignment>>();
            List<List<SpotAssignment>> second = new List<List<SpotAssignment>>();
            SeededRandom random = new SeededRandom(map.Seed);
            bool isBuilt = WorldDepositPlanner.TryBuild(config, plans, random, totals, first)
                && WorldDepositPlanner.TryBuild(config, plans, random, totals, second);

            bool isSumOk = isBuilt;
            bool isSpreadOk = isBuilt;
            bool isRepeatOk = isBuilt;
            int checkedResources = 0;
            foreach (KeyValuePair<int, int> pair in totals)
            {
                if (!isBuilt || !TryMeasure(first, pair.Key, out int count, out int sum, out int min, out int max))
                {
                    continue;
                }

                checkedResources++;
                isSumOk &= sum == pair.Value;
                isSpreadOk &= max - min <= 1;
                isRepeatOk &= IsSame(first, second);
            }

            HarvestDepositChecks.Report("Planner total preserved", isSumOk && checkedResources > 0, $"resources={checkedResources}, islands={plans.Count}");
            HarvestDepositChecks.Report("Planner even spread", isSpreadOk, "max-min <= 1 per resource");
            HarvestDepositChecks.Report("Planner determinism", isRepeatOk, "same seed twice");
            return isSumOk && checkedResources > 0 && isSpreadOk && isRepeatOk;
        }

        private static Dictionary<int, int> BuildTotals(ResourceCatalog catalog)
        {
            Dictionary<int, int> totals = new Dictionary<int, int>();
            for (int i = 0; i < catalog.Count; i++)
            {
                if (catalog[i] != null)
                {
                    totals[catalog[i].Id] = BaseTotal + catalog[i].Id;
                }
            }

            return totals;
        }

        private static bool TryMeasure(List<List<SpotAssignment>> islands, int resourceId, out int count, out int sum, out int min, out int max)
        {
            count = 0;
            sum = 0;
            min = int.MaxValue;
            max = 0;
            for (int i = 0; i < islands.Count; i++)
            {
                for (int j = 0; j < islands[i].Count; j++)
                {
                    SpotAssignment assignment = islands[i][j];
                    if (assignment.ResourceId != resourceId)
                    {
                        continue;
                    }

                    count++;
                    sum += assignment.InitialAmount;
                    min = System.Math.Min(min, assignment.InitialAmount);
                    max = System.Math.Max(max, assignment.InitialAmount);
                }
            }

            return count > 0;
        }

        private static bool IsSame(List<List<SpotAssignment>> a, List<List<SpotAssignment>> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].Count != b[i].Count)
                {
                    return false;
                }

                for (int j = 0; j < a[i].Count; j++)
                {
                    if (a[i][j].ResourceId != b[i][j].ResourceId || a[i][j].InitialAmount != b[i][j].InitialAmount)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
