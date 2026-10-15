using System.Collections.Generic;

namespace Lighthouse.World.Harvest.Rules
{
    public static class HarvestSpotBinder
    {
        public static bool TryBind(short islandIndex, IReadOnlyList<HarvestSpot> spots, IReadOnlyList<SpotAssignment> assignments)
        {
            if (spots == null || assignments == null || spots.Count != assignments.Count || spots.Count > ushort.MaxValue + 1)
            {
                return false;
            }

            for (int i = 0; i < spots.Count; i++)
            {
                if (spots[i] == null)
                {
                    return false;
                }
            }

            for (int i = 0; i < spots.Count; i++)
            {
                spots[i].Bind(HarvestSpotIds.Compose(islandIndex, (ushort)i), assignments[i].ResourceId);
            }

            return true;
        }
    }
}
