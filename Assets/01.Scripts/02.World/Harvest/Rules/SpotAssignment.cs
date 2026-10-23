using UnityEngine;

namespace Lighthouse.World.Harvest.Rules
{
    public readonly struct SpotAssignment
    {
        public readonly int ResourceId;
        public readonly GameObject PickupPrefab;
        public readonly int InitialAmount;
        public readonly int MaxAmount;
        public readonly float DailyRegenRate;

        public SpotAssignment(
            int resourceId,
            int initialAmount,
            int maxAmount,
            float dailyRegenRate,
            GameObject pickupPrefab = null)
        {
            ResourceId = resourceId;
            PickupPrefab = pickupPrefab;
            InitialAmount = initialAmount;
            MaxAmount = maxAmount;
            DailyRegenRate = dailyRegenRate;
        }

        public bool IsAssigned => ResourceId != 0;
    }
}
