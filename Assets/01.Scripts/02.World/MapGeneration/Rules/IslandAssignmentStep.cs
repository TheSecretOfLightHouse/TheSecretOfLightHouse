using System.Collections.Generic;
using Lighthouse.World.Islands.Rules;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class IslandAssignmentStep : IMapGenerationStep
    {
        public int Order => 300;

        public bool IsValidation => false;

        public void Execute(MapContext context)
        {
            SeededRandom random = context.Fork(MapGenerationStepId.IslandAssignment);
            List<IslandArchetype> archetypes = CollectSortedArchetypes(context.Config);
            int[] counts = new int[archetypes.Count];

            QuotaAssigner.Assign(context, random, archetypes, counts);
            FillAssigner.Assign(context, random, archetypes, counts);
        }

        private static List<IslandArchetype> CollectSortedArchetypes(MapGenerationConfig config)
        {
            List<IslandArchetype> archetypes = new List<IslandArchetype>();
            for (int i = 0; i < config.Archetypes.Count; i++)
            {
                if (config.Archetypes[i] != null)
                {
                    archetypes.Add(config.Archetypes[i]);
                }
            }

            archetypes.Sort(CompareById);
            return archetypes;
        }

        private static int CompareById(IslandArchetype a, IslandArchetype b)
        {
            return a.Id.CompareTo(b.Id);
        }
    }
}
