using System.Collections.Generic;

namespace Lighthouse.World.Harvest.Rules
{
    public static class DepositDistributor
    {
        public static void Apply(IReadOnlyDictionary<int, int> totals, IReadOnlyList<List<SpotAssignment>> islands)
        {
            if (totals == null || islands == null)
            {
                return;
            }

            foreach (KeyValuePair<int, int> pair in totals)
            {
                if (pair.Value > 0)
                {
                    Distribute(pair.Key, pair.Value, islands);
                }
            }
        }

        private static void Distribute(int resourceId, int total, IReadOnlyList<List<SpotAssignment>> islands)
        {
            int count = CountSpots(resourceId, islands);
            if (count == 0)
            {
                return;
            }

            int share = total / count;
            int remainder = total % count;
            int ordinal = 0;

            for (int i = 0; i < islands.Count; i++)
            {
                List<SpotAssignment> assignments = islands[i];
                for (int j = 0; j < assignments.Count; j++)
                {
                    SpotAssignment current = assignments[j];
                    if (current.ResourceId != resourceId)
                    {
                        continue;
                    }

                    int amount = share + (ordinal < remainder ? 1 : 0);
                    ordinal++;
                    assignments[j] = new SpotAssignment(current.ResourceId, amount, amount, current.DailyRegenRate, current.PickupPrefab);
                }
            }
        }

        private static int CountSpots(int resourceId, IReadOnlyList<List<SpotAssignment>> islands)
        {
            int count = 0;
            for (int i = 0; i < islands.Count; i++)
            {
                List<SpotAssignment> assignments = islands[i];
                for (int j = 0; j < assignments.Count; j++)
                {
                    if (assignments[j].ResourceId == resourceId)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
