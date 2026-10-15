using System.Collections.Generic;

namespace Lighthouse.World.Harvest.Rules
{
    public interface IHarvestIslandRegistrar
    {
        bool RegisterIsland(short islandIndex, IReadOnlyList<HarvestSpot> spots, IReadOnlyList<SpotAssignment> assignments);

        bool UnregisterIsland(short islandIndex);
    }
}
