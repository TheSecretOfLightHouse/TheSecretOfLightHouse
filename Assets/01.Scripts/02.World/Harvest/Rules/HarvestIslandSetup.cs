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
            List<SpotAssignment> assignments = new List<SpotAssignment>(spots != null ? spots.Count : 0);
            return TryAssign(catalog, archetype, islandIndex, mapRandom, spots, assignments)
                && TryRegister(registrar, islandIndex, spots, assignments);
        }

        public static bool TryRegister(
            IHarvestIslandRegistrar registrar,
            short islandIndex,
            IReadOnlyList<HarvestSpot> spots,
            IReadOnlyList<SpotAssignment> assignments)
        {
            return registrar != null
                ? registrar.RegisterIsland(islandIndex, spots, assignments)
                : HarvestSpotBinder.TryBind(islandIndex, spots, assignments);
        }

        public static bool TryAssign(
            ResourceCatalog catalog,
            IslandArchetype archetype,
            short islandIndex,
            SeededRandom mapRandom,
            IReadOnlyList<HarvestSpot> spots,
            List<SpotAssignment> assignments)
        {
            if (mapRandom == null || spots == null || assignments == null)
            {
                return false;
            }

            List<HarvestSpotCategory> categories = new List<HarvestSpotCategory>(spots.Count);
            List<ResourceDefinition> fixedResources = new List<ResourceDefinition>(spots.Count);
            for (int i = 0; i < spots.Count; i++)
            {
                if (spots[i] == null)
                {
                    return false;
                }

                categories.Add(spots[i].Category);
                fixedResources.Add(spots[i].FixedResource);
            }

            SeededRandom random = mapRandom.Fork((int)MapGenerationStepId.HarvestAssignment).Fork(islandIndex);
            return HarvestSpotAssigner.Assign(catalog, archetype, categories, fixedResources, random, assignments);
        }
    }
}
