using System;
using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;

namespace Lighthouse.World.Harvest.Server
{
    public sealed class HarvestSpotRegistry :
        IHarvestSpotRegistry,
        IHarvestIslandRegistrar,
        IHarvestChangeSource
    {
        private readonly Dictionary<short, IslandEntry> _islands =
            new Dictionary<short, IslandEntry>();

        private readonly List<IslandEntry> _entries =
            new List<IslandEntry>();

        private readonly HarvestNotificationQueue<(IslandEntry Entry, int LocalIndex, int Remaining)> _notifications;

        public event Action<short, int, int> RemainingChanged;

        public HarvestSpotRegistry()
        {
            _notifications = new HarvestNotificationQueue<
                (IslandEntry Entry, int LocalIndex, int Remaining)>(
                DispatchRemainingChanged);
        }

        public bool RegisterIsland(
            short islandIndex,
            IReadOnlyList<HarvestSpot> spots,
            IReadOnlyList<SpotAssignment> assignments)
        {
            if (_islands.ContainsKey(islandIndex)
                || !HarvestSpotBinder.TryBind(islandIndex, spots, assignments))
            {
                return false;
            }

            HarvestSpot[] copy = new HarvestSpot[spots.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = spots[i];
            }

            IslandEntry entry = new IslandEntry(
                islandIndex,
                new ResourceDeposit(assignments),
                copy,
                RaiseRemainingChanged);

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

            entry.Release();
            _islands.Remove(islandIndex);
            _entries.Remove(entry);
            return true;
        }

        public bool TryGetSpot(int spotId, out SpotInfo info)
        {
            info = default;
            if (!TryFind(spotId, out IslandEntry entry, out int localIndex)
                || !entry.Deposit.TryGetResourceId(
                    localIndex, out int resourceId)
                || !entry.Deposit.TryGetRemaining(
                    localIndex, out int remaining))
            {
                return false;
            }

            info = new SpotInfo(
                spotId,
                entry.Spots[localIndex].Position,
                resourceId,
                remaining);
            return true;
        }

        public bool TryReserve(
            int spotId,
            int requested,
            out HarvestReservation reservation)
        {
            reservation = null;
            return TryFind(spotId, out IslandEntry entry, out int localIndex)
                && entry.Deposit.TryReserve(
                    localIndex,
                    spotId,
                    entry.Spots[localIndex].Position,
                    requested,
                    out reservation);
        }

        public bool TryCommit(HarvestReservation reservation)
        {
            return TryFindReservationIsland(reservation, out IslandEntry entry)
                && entry.Deposit.TryCommit(reservation);
        }

        public bool TryCancel(HarvestReservation reservation)
        {
            return TryFindReservationIsland(reservation, out IslandEntry entry)
                && entry.Deposit.TryCancel(reservation);
        }

        public int Regenerate(IRegenPolicy policy, int days)
        {
            int changed = 0;

            // Notifications may unregister an island while iterating.
            IslandEntry[] snapshot = _entries.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                IslandEntry entry = snapshot[i];
                if (_islands.TryGetValue(entry.Index, out IslandEntry current)
                    && ReferenceEquals(entry, current))
                {
                    changed += entry.Deposit.Regenerate(policy, days);
                }
            }

            return changed;
        }

        public bool TryCopyNodes(
            short islandIndex,
            List<NodeState> destination)
        {
            if (destination == null
                || !_islands.TryGetValue(islandIndex, out IslandEntry entry))
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

        private void RaiseRemainingChanged(IslandEntry entry, int localIndex, int remaining)
        {
            _notifications.Publish((entry, localIndex, remaining));
        }

        private void DispatchRemainingChanged((IslandEntry Entry, int LocalIndex, int Remaining) notification)
        {
            IslandEntry entry = notification.Entry;

            if (!_islands.TryGetValue(entry.Index, out IslandEntry current)
                || !ReferenceEquals(entry, current)
                || !entry.CanPublish())
            {
                return;
            }

            HarvestChangeNotification.Publish(
                RemainingChanged,
                entry.Index,
                notification.LocalIndex,
                notification.Remaining,
                entry.CanPublish);
        }

        private bool TryFind(
            int spotId,
            out IslandEntry entry,
            out int localIndex)
        {
            localIndex = HarvestSpotIds.GetLocalIndex(spotId);
            return _islands.TryGetValue(
                    HarvestSpotIds.GetIslandIndex(spotId), out entry)
                && localIndex < entry.Spots.Length
                && entry.Spots[localIndex] != null;
        }

        private bool TryFindReservationIsland(
            HarvestReservation reservation,
            out IslandEntry entry)
        {
            entry = null;
            return reservation != null
                && _islands.TryGetValue(
                    HarvestSpotIds.GetIslandIndex(reservation.SpotId),
                    out entry);
        }

        private sealed class IslandEntry
        {
            private readonly Action<IslandEntry, int, int> _forward;

            private bool _isReleased;

            public short Index { get; }
            public ResourceDeposit Deposit { get; }
            public HarvestSpot[] Spots { get; }
            public Func<bool> CanPublish { get; }

            public IslandEntry(
                short index,
                ResourceDeposit deposit,
                HarvestSpot[] spots,
                Action<IslandEntry, int, int> forward)
            {
                Index = index;
                Deposit = deposit;
                Spots = spots;
                _forward = forward;
                CanPublish = IsActive;

                Deposit.RemainingChanged += HandleRemainingChanged;
            }

            public void Release()
            {
                _isReleased = true;
                Deposit.RemainingChanged -= HandleRemainingChanged;
                Deposit.InvalidateReservations();
            }

            private bool IsActive()
            {
                return !_isReleased;
            }

            private void HandleRemainingChanged(
                int localIndex,
                int remaining)
            {
                _forward(this, localIndex, remaining);
            }
        }
    }
}
