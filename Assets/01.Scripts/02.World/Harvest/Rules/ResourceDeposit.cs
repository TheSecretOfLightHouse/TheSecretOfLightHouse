using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Harvest.Rules
{
    public sealed class ResourceDeposit
    {
        private readonly int[] _resourceIds;
        private readonly int[] _maxAmounts;
        private readonly float[] _regenRates;
        private readonly int[] _remaining;
        private readonly int[] _reserved;

        private readonly Dictionary<HarvestReservation, int> _pending =
            new Dictionary<HarvestReservation, int>();
        private readonly HarvestNotificationQueue<(int LocalIndex, int Remaining)> _notifications;

        public int Count => _remaining.Length;

        public event Action<int, int> RemainingChanged;

        public ResourceDeposit(IReadOnlyList<SpotAssignment> assignments)
        {
            int count = assignments != null ? assignments.Count : 0;
            _resourceIds = new int[count];
            _maxAmounts = new int[count];
            _regenRates = new float[count];
            _remaining = new int[count];
            _reserved = new int[count];
            _notifications =
                new HarvestNotificationQueue<(int LocalIndex, int Remaining)>(DispatchRemainingChanged);

            for (int i = 0; i < count; i++)
            {
                SpotAssignment assignment = assignments[i];
                _resourceIds[i] = assignment.ResourceId;
                _maxAmounts[i] = Mathf.Max(0, assignment.MaxAmount);
                _regenRates[i] = assignment.DailyRegenRate;
                _remaining[i] = Mathf.Clamp(
                    assignment.InitialAmount, 0, _maxAmounts[i]);
            }
        }

        public bool TryGetResourceId(int localIndex, out int resourceId)
        {
            resourceId = 0;
            if (!IsAssigned(localIndex))
            {
                return false;
            }

            resourceId = _resourceIds[localIndex];
            return true;
        }

        public bool TryGetRemaining(int localIndex, out int remaining)
        {
            remaining = 0;
            if (!IsAssigned(localIndex))
            {
                return false;
            }

            remaining = _remaining[localIndex];
            return true;
        }

        internal bool TryReserve(
            int localIndex,
            int spotId,
            Vector3 position,
            int requested,
            out HarvestReservation reservation)
        {
            reservation = null;
            if (!IsAssigned(localIndex) || requested <= 0)
            {
                return false;
            }

            int available = _remaining[localIndex] - _reserved[localIndex];
            int granted = Mathf.Min(available, requested);
            if (granted <= 0)
            {
                return false;
            }

            HarvestReservation created = new HarvestReservation(
                spotId, position, _resourceIds[localIndex], granted);

            _pending.Add(created, localIndex);
            _reserved[localIndex] += granted;
            reservation = created;
            return true;
        }

        internal bool TryCommit(HarvestReservation reservation)
        {
            if (!TryTakeReservation(reservation, out int localIndex))
            {
                return false;
            }

            _reserved[localIndex] -= reservation.Granted;
            _remaining[localIndex] -= reservation.Granted;

            _notifications.Publish((localIndex, _remaining[localIndex]));

            return true;
        }

        internal bool TryCancel(HarvestReservation reservation)
        {
            if (!TryTakeReservation(reservation, out int localIndex))
            {
                return false;
            }

            _reserved[localIndex] -= reservation.Granted;
            return true;
        }

        internal void InvalidateReservations()
        {
            _pending.Clear();
            Array.Clear(_reserved, 0, _reserved.Length);
        }

        public int Regenerate(IRegenPolicy policy, int days)
        {
            if (policy == null || days <= 0)
            {
                return 0;
            }

            int changed = 0;
            for (int i = 0; i < _remaining.Length; i++)
            {
                if (!IsAssigned(i))
                {
                    continue;
                }

                // Reserved units still belong to the deposit until commit.
                int room = _maxAmounts[i] - _remaining[i];
                int amount = policy.GetRegenAmount(
                    _maxAmounts[i], _regenRates[i], _remaining[i], days);

                int added = Mathf.Clamp(amount, 0, room);
                if (added == 0)
                {
                    continue;
                }

                _remaining[i] += added;
                changed++;

                _notifications.Publish((i, _remaining[i]));
            }

            return changed;
        }

        private bool TryTakeReservation(
            HarvestReservation reservation,
            out int localIndex)
        {
            localIndex = 0;
            if (reservation == null
                || !_pending.TryGetValue(reservation, out localIndex))
            {
                return false;
            }

            // Remove before callbacks can re-enter the settlement path.
            _pending.Remove(reservation);
            return true;
        }

        private bool IsAssigned(int localIndex)
        {
            return localIndex >= 0
                && localIndex < _resourceIds.Length
                && _resourceIds[localIndex] != 0;
        }

        private void DispatchRemainingChanged((int LocalIndex, int Remaining) notification)
        {
            HarvestChangeNotification.Publish(
                RemainingChanged,
                notification.LocalIndex,
                notification.Remaining);
        }
    }
}
