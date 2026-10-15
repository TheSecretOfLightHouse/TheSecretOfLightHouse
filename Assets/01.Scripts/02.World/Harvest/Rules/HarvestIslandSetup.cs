using System.Collections.Generic;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.MapGeneration.Rules;

namespace Lighthouse.World.Harvest.Rules
{
    public static class HarvestIslandSetup
    {
        public static bool TryRegister(
            IHarvestIslandRegistrar registrar,
            ResourceCatalog catalog,
            IslandArchetype archetype,
            short islandIndex,
            SeededRandom mapRandom,
            IReadOnlyList<HarvestSpot> spots)
        {
            if (mapRandom == null || spots == null)
            {
                return false;
            }

            List<HarvestSpotCategory> categories = new List<HarvestSpotCategory>(spots.Count);
            for (int i = 0; i < spots.Count; i++)
            {
                if (spots[i] == null)
                {
                    return false;
                }

                categories.Add(spots[i].Category);
            }

            SeededRandom random = mapRandom.Fork((int)MapGenerationStepId.HarvestAssignment).Fork(islandIndex);
            List<SpotAssignment> assignments = new List<SpotAssignment>(spots.Count);
            if (!HarvestSpotAssigner.Assign(catalog, archetype, categories, random, assignments))
            {
                return false;
            }

            return registrar != null
                ? registrar.RegisterIsland(islandIndex, spots, assignments)
                : HarvestSpotBinder.TryBind(islandIndex, spots, assignments);
        }
    }
}
