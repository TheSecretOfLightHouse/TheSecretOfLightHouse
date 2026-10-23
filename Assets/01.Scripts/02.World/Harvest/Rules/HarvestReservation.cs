using UnityEngine;

namespace Lighthouse.World.Harvest.Rules
{
    public sealed class HarvestReservation
    {
        public int SpotId { get; }
        public Vector3 Position { get; }
        public int ResourceId { get; }
        public GameObject PickupPrefab { get; }
        public int Granted { get; }

        internal HarvestReservation(
            int spotId,
            Vector3 position,
            int resourceId,
            int granted,
            GameObject pickupPrefab = null)
        {
            SpotId = spotId;
            Position = position;
            ResourceId = resourceId;
            PickupPrefab = pickupPrefab;
            Granted = granted;
        }
    }
}
