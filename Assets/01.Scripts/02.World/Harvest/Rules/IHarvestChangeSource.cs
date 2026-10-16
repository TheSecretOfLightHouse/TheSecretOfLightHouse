using System;
using System.Collections.Generic;

namespace Lighthouse.World.Harvest.Rules
{
    public interface IHarvestChangeSource
    {
        event Action<short, int, int> RemainingChanged;

        bool TryCopyNodes(short islandIndex, List<NodeState> destination);
    }
}
