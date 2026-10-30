using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    public readonly struct MapIslandMark
    {
        public readonly short Index;
        public readonly int ArchetypeId;
        public readonly string DisplayName;
        public readonly bool IsLighthouse;
        public readonly SeaZone Zone;
        public readonly Vector2 Position;
        public readonly float Radius;
        public readonly float YawDegrees;

        public MapIslandMark(short index, int archetypeId, string displayName, bool isLighthouse,
            SeaZone zone, Vector2 position, float radius, float yawDegrees)
        {
            Index = index;
            ArchetypeId = archetypeId;
            DisplayName = displayName ?? string.Empty;
            IsLighthouse = isLighthouse;
            Zone = zone;
            Position = position;
            Radius = radius;
            YawDegrees = yawDegrees;
        }
    }
}
