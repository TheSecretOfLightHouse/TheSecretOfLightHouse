using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public struct BreakerEvent
    {
        public int SourceId;
        public Vector2 ImpactPoint;
        public Vector2 Direction;
        public float Width;
        public float Height;
        public float Age;
        public float Strength;
    }
}
