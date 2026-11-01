using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public readonly struct WaterSplashRequest
    {
        public readonly WaterSplashKind Kind;
        public readonly Vector3 Position;
        public readonly Vector3 Normal;
        public readonly float Strength;
        public readonly byte Priority;

        public WaterSplashRequest(WaterSplashKind kind, Vector3 position, Vector3 normal, float strength, byte priority)
        {
            Kind = kind;
            Position = position;
            Normal = normal;
            Strength = strength;
            Priority = priority;
        }
    }
}
