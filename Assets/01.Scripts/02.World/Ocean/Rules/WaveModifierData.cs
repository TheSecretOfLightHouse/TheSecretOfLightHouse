using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public readonly struct WaveModifierData
    {
        public readonly WaveModifierKind Kind;
        public readonly Vector2 Center;
        public readonly float Radius;
        public readonly float Strength;
        public readonly float StartTime;

        public WaveModifierData(WaveModifierKind kind, Vector2 center, float radius, float strength, float startTime)
        {
            Kind = kind;
            Center = center;
            Radius = radius;
            Strength = strength;
            StartTime = startTime;
        }
    }
}
