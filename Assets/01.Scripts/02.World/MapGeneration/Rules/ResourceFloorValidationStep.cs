using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Rules;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class ResourceFloorValidationStep : IMapGenerationStep
    {
        public int Order => 800;

        public bool IsValidation => true;

        public void Execute(MapContext context)
        {
            for (int i = 0; i < context.Config.ResourceFloors.Count; i++)
            {
                ResourceFloor floor = context.Config.ResourceFloors[i];
                if (floor == null || floor.Resource == null)
                {
                    continue;
                }

                int count = CountProvidingIslands(context, floor.Resource);
                if (count < floor.MinIslandCount)
                {
                    context.AddIssue($"{floor.Resource.name}: 제공하는 섬 {count}개 < 하한 {floor.MinIslandCount}개");
                }
            }
        }

        private static int CountProvidingIslands(MapContext context, ResourceDefinition resource)
        {
            int count = 0;
            for (int i = 0; i < context.Slots.Count; i++)
            {
                if (context.Config.TryGetArchetype(context.Slots[i].ArchetypeId, out IslandArchetype archetype)
                    && (resource.IsUbiquitous || Provides(archetype, resource.Id)))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool Provides(IslandArchetype archetype, int resourceId)
        {
            for (int i = 0; i < archetype.Resources.Count; i++)
            {
                IslandResourceEntry entry = archetype.Resources[i];
                if (entry != null && entry.Resource != null && entry.Resource.Id == resourceId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
