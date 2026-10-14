using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    public readonly struct ReefPlacement
    {
        public readonly Vector2 Position;
        public readonly float Radius;
        public readonly SeaZone Zone;

        public ReefPlacement(Vector2 position, float radius, SeaZone zone)
        {
            Position = position;
            Radius = radius;
            Zone = zone;
        }

        public void AppendTo(StableHash hash)
        {
            hash.Add(Position.x);
            hash.Add(Position.y);
            hash.Add(Radius);
            hash.Add((int)Zone);
        }
    }
}
