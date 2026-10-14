using System.Collections.Generic;
using Lighthouse.World.Islands.Rules;

namespace Lighthouse.World.MapGeneration.Rules
{
    public static class MapLayoutAssembler
    {
        public static MapLayout Create(MapContext context)
        {
            List<IslandPlacement> islands = new List<IslandPlacement>();
            AddIsland(context, context.Lighthouse, islands);
            for (int i = 0; i < context.Slots.Count; i++)
            {
                AddIsland(context, context.Slots[i], islands);
            }

            return new MapLayout(context.Seed, islands, context.Reefs);
        }

        private static void AddIsland(MapContext context, IslandSlot slot, List<IslandPlacement> islands)
        {
            if (slot == null || !context.Config.TryGetArchetype(slot.ArchetypeId, out IslandArchetype archetype))
            {
                return;
            }

            islands.Add(new IslandPlacement(
                (short)islands.Count,
                archetype.Id,
                (byte)slot.VariantIndex,
                slot.Zone,
                slot.Position,
                archetype.Radius,
                slot.YawDegrees));
        }
    }
}
