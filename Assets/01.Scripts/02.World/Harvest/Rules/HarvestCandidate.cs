using UnityEngine;

namespace Lighthouse.World.Harvest.Rules
{
    public readonly struct HarvestCandidate
    {
        public readonly HarvestSpotCategory Category;
        public readonly int ResourceId;
        public readonly float Weight;
        public readonly DepositSpec Spec;
        public readonly GameObject PickupPrefab;

        public HarvestCandidate(
            HarvestSpotCategory category,
            int resourceId,
            float weight,
            DepositSpec spec,
            GameObject pickupPrefab)
        {
            Category = category;
            ResourceId = resourceId;
            Weight = weight;
            Spec = spec;
            PickupPrefab = pickupPrefab;
        }
    }
}
