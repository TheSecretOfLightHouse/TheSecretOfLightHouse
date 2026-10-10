using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public readonly struct HazardHit
    {
        public readonly HazardType Type;
        public readonly Vector3 Position;
        public readonly float Strength;
        public readonly uint ShipId;

        public HazardHit(HazardType type, Vector3 position, float strength, uint shipId)
        {
            Type = type;
            Position = position;
            Strength = strength;
            ShipId = shipId;
        }
    }
}
