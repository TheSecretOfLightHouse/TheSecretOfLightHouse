using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Harvest.Rules.Mocks
{
    public class FakeHarvestRegistry : IHarvestSpotRegistry
    {
        private readonly int _resourceId;

        private readonly Dictionary<int, ResourceDeposit> _deposits =
            new Dictionary<int, ResourceDeposit>();

        public FakeHarvestRegistry(int resourceId)
        {
            _resourceId = resourceId;
        }

        public bool TryGetSpot(int spotId, out SpotInfo info)
        {
            info = default;
            if (!TryGetDeposit(spotId, out ResourceDeposit deposit)
                || !deposit.TryGetRemaining(0, out int remaining))
            {
                return false;
            }

            info = new SpotInfo(
                spotId, Vector3.zero, _resourceId, remaining);
            return true;
        }

        public bool TryReserve(
            int spotId,
            int requested,
            out HarvestReservation reservation)
        {
            reservation = null;
            return requested > 0
                && TryGetDeposit(spotId, out ResourceDeposit deposit)
                && deposit.TryReserve(
                    0, spotId, Vector3.zero, requested, out reservation);
        }

        public bool TryCommit(HarvestReservation reservation)
        {
            return reservation != null
                && _deposits.TryGetValue(
                    reservation.SpotId, out ResourceDeposit deposit)
                && deposit.TryCommit(reservation);
        }

        public bool TryCancel(HarvestReservation reservation)
        {
            return reservation != null
                && _deposits.TryGetValue(
                    reservation.SpotId, out ResourceDeposit deposit)
                && deposit.TryCancel(reservation);
        }

        private bool TryGetDeposit(
            int spotId,
            out ResourceDeposit deposit)
        {
            deposit = null;
            if (spotId <= 0 || _resourceId <= 0)
            {
                return false;
            }

            if (!_deposits.TryGetValue(spotId, out deposit))
            {
                SpotAssignment[] assignments =
                {
                    new SpotAssignment(
                        _resourceId, int.MaxValue, int.MaxValue, 0f)
                };

                deposit = new ResourceDeposit(assignments);
                _deposits.Add(spotId, deposit);
            }

            return true;
        }
    }
}
