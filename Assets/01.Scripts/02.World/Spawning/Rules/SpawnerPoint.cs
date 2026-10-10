using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Spawning.Rules
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
