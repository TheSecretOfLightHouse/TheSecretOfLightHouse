using UnityEngine;

namespace Lighthouse.Map.Net.Contracts
{
    public readonly struct SpotInfo
    {
        public readonly int SpotId;
        public readonly Vector3 Position;
        public readonly int ResourceId;
        public readonly int Remaining;

        public SpotInfo(int spotId, Vector3 position, int resourceId, int remaining)
        {
            SpotId = spotId;
            Position = position;
            ResourceId = resourceId;
            Remaining = remaining;
        }
    }
}
