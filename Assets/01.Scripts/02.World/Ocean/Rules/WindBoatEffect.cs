using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public struct WindBoatEffect
    {
        public Vector3 Drift;
        public float SpeedMultiplier;
        public float AccelerationMultiplier;

        public static WindBoatEffect None => new WindBoatEffect
        {
            Drift = Vector3.zero,
            SpeedMultiplier = 1f,
            AccelerationMultiplier = 1f
        };
    }
}
