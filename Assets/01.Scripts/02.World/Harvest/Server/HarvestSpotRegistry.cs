using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;

namespace Lighthouse.World.Harvest.Server
{
    public sealed class HarvestSpotRegistry : IHarvestSpotRegistry
    {
        private readonly Dictionary<short, IslandEntry> _islands = new Dictionary<short, IslandEntry>();
        private readonly List<IslandEntry> _entries = new List<IslandEntry>();

        public bool RegisterIsland(short islandIndex, IReadOnlyList<HarvestSpot> spots, IReadOnlyList<SpotAssignment> assignments)
        {
            if (_islands.ContainsKey(islandIndex) || !HarvestSpotBinder.TryBind(islandIndex, spots, assignments))
            {
                return false;
            }

            HarvestSpot[] copy = new HarvestSpot[spots.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = spots[i];
            }

            IslandEntry entry = new IslandEntry(new ResourceDeposit(assignments), copy);
            _islands.Add(islandIndex, entry);
            _entries.Add(entry);
            return true;
        }

        public bool UnregisterIsland(short islandIndex)
        {
            if (!_islands.TryGetValue(islandIndex, out IslandEntry entry))
            {
                return false;
            }

            _islands.Remove(islandIndex);
            _entries.Remove(entry);
            return true;
        }

        public bool TryGetSpot(int spotId, out SpotInfo info)
        {
            info = default;
            if (!TryFind(spotId, out IslandEntry entry, out int localIndex)
                || !entry.Deposit.TryGetResourceId(localIndex, out int resourceId)
                || !entry.Deposit.TryGetRemaining(localIndex, out int remaining))
            {
                return false;
            }

            info = new SpotInfo(spotId, entry.Spots[localIndex].Position, resourceId, remaining);
            return true;
        }

        public bool TryConsume(int spotId, int requested, out ConsumeResult result)
        {
            result = default;
            return TryFind(spotId, out IslandEntry entry, out int localIndex)
                && entry.Deposit.TryConsume(localIndex, requested, out result);
        }

        public int Regenerate(IRegenPolicy policy, int days)
        {
            int changed = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                changed += _entries[i].Deposit.Regenerate(policy, days);
            }

            return changed;
        }

        private bool TryFind(int spotId, out IslandEntry entry, out int localIndex)
        {
            localIndex = HarvestSpotIds.GetLocalIndex(spotId);
            if (!_islands.TryGetValue(HarvestSpotIds.GetIslandIndex(spotId), out entry)
                || localIndex >= entry.Spots.Length
                || entry.Spots[localIndex] == null)
            {
                return false;
            }

            return true;
        }

        private sealed class IslandEntry
        {
            public readonly ResourceDeposit Deposit;
            public readonly HarvestSpot[] Spots;

            public IslandEntry(ResourceDeposit deposit, HarvestSpot[] spots)
            {
                Deposit = deposit;
                Spots = spots;
            }
        }
    }
}
