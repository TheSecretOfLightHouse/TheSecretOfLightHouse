using UnityEngine;

namespace Lighthouse.Map.Net.Contracts
{
    public readonly struct SpawnerPoint
    {
        public readonly Vector3 Position;
        public readonly SeaZone Zone;
        public readonly int ProfileId;

        public SpawnerPoint(Vector3 position, SeaZone zone, int profileId)
        {
            Position = position;
            Zone = zone;
            ProfileId = profileId;
        }
    }
}
