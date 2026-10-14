using Lighthouse.World.Islands.Rules;

namespace Lighthouse.World.MapGeneration.Rules
{
    public static class SameTypeSpacing
    {
        public static bool IsFarEnough(MapContext context, IslandSlot slot, IslandArchetype archetype)
        {
            float minDistance = archetype.MinSameTypeDistance;
            if (minDistance <= 0f)
            {
                return true;
            }

            float minSqr = minDistance * minDistance;
            for (int i = 0; i < context.Slots.Count; i++)
            {
                IslandSlot other = context.Slots[i];
                if (other != slot && other.ArchetypeId == archetype.Id && (other.Position - slot.Position).sqrMagnitude < minSqr)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
