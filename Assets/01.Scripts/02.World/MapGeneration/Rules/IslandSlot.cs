using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class IslandSlot
    {
        public IslandSlot(Vector2 position, SeaZone zone, int archetypeId = 0)
        {
            Position = position;
            Zone = zone;
            ArchetypeId = archetypeId;
        }

        public Vector2 Position { get; }

        public SeaZone Zone { get; }

        public int ArchetypeId { get; set; }

        public int VariantIndex { get; set; }

        public float YawDegrees { get; set; }
    }
}
