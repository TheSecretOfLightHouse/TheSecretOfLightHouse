using Lighthouse.World.Islands.Rules;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    public readonly struct IslandPlacement
    {
        public readonly short Index;
        public readonly int ArchetypeId;
        public readonly byte VariantIndex;
        public readonly SeaZone Zone;
        public readonly Vector2 Position;
        public readonly float Radius;
        public readonly float YawDegrees;

        public IslandPlacement(short index, int archetypeId, byte variantIndex, SeaZone zone, Vector2 position, float radius, float yawDegrees)
        {
            Index = index;
            ArchetypeId = archetypeId;
            VariantIndex = variantIndex;
            Zone = zone;
            Position = position;
            Radius = radius;
            YawDegrees = yawDegrees;
        }

        public IslandHandle ToHandle()
        {
            return new IslandHandle(Index, new Vector3(Position.x, 0f, Position.y), Radius);
        }

        public void AppendTo(StableHash hash)
        {
            hash.Add(Index);
            hash.Add(ArchetypeId);
            hash.Add(VariantIndex);
            hash.Add((int)Zone);
            hash.Add(Position.x);
            hash.Add(Position.y);
            hash.Add(Radius);
            hash.Add(YawDegrees);
        }
    }
}
