using System;
using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;

namespace Lighthouse.World.Harvest.Server
{
    public sealed class HarvestSpotRegistry : IHarvestSpotRegistry, IHarvestIslandRegistrar, IHarvestChangeSource
    {
        private readonly Dictionary<short, IslandEntry> _islands = new Dictionary<short, IslandEntry>();
        private readonly List<IslandEntry> _entries = new List<IslandEntry>();

        public event Action<short, int, int> RemainingChanged;

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

            IslandEntry entry = new IslandEntry(islandIndex, new ResourceDeposit(assignments), copy, RaiseRemainingChanged);
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

            entry.Detach();
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

        public bool TryCopyNodes(short islandIndex, List<NodeState> destination)
        {
            if (destination == null || !_islands.TryGetValue(islandIndex, out IslandEntry entry))
            {
                return false;
            }

            destination.Clear();
            for (int i = 0; i < entry.Deposit.Count; i++)
            {
                entry.Deposit.TryGetResourceId(i, out int resourceId);
                entry.Deposit.TryGetRemaining(i, out int remaining);
                destination.Add(new NodeState(resourceId, remaining));
            }

            return true;
        }

        private void RaiseRemainingChanged(short islandIndex, int localIndex, int remaining)
        {
            RemainingChanged?.Invoke(islandIndex, localIndex, remaining);
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

            private readonly short _islandIndex;
            private readonly Action<short, int, int> _forward;

            public IslandEntry(short islandIndex, ResourceDeposit deposit, HarvestSpot[] spots, Action<short, int, int> forward)
            {
                _islandIndex = islandIndex;
                _forward = forward;
                Deposit = deposit;
                Spots = spots;
                Deposit.RemainingChanged += HandleRemainingChanged;
            }

            public void Detach()
            {
                Deposit.RemainingChanged -= HandleRemainingChanged;
            }

            private void HandleRemainingChanged(int localIndex, int remaining)
            {
                _forward(_islandIndex, localIndex, remaining);
            }
        }
    }
}
