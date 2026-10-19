using UnityEngine;

namespace Lighthouse.World.Islands.Rules
{
    public readonly struct IslandSpawnPlan
    {
        public readonly short Index;
        public readonly IslandArchetype Archetype;
        public readonly byte VariantIndex;
        public readonly GameObject Prefab;
        public readonly Vector3 Position;
        public readonly float YawDegrees;

        public IslandSpawnPlan(short index, IslandArchetype archetype, byte variantIndex, GameObject prefab, Vector3 position, float yawDegrees)
        {
            Index = index;
            Archetype = archetype;
            VariantIndex = variantIndex;
            Prefab = prefab;
            Position = position;
            YawDegrees = yawDegrees;
        }
    }
}
