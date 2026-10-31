using Lighthouse.World.Islands.Rules;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class IslandAppearanceStep : IMapGenerationStep
    {
        private const float FullTurnDegrees = 360f;

        public int Order => 400;

        public bool IsValidation => false;

        public void Execute(MapContext context)
        {
            SeededRandom random = context.Fork(MapGenerationStepId.IslandAppearance);
            Apply(context, context.Lighthouse, random);
            for (int i = 0; i < context.Slots.Count; i++)
            {
                Apply(context, context.Slots[i], random);
            }
        }

        private static void Apply(MapContext context, IslandSlot slot, SeededRandom random)
        {
            float variantRoll = random.NextFloat();
            float yaw = random.NextFloat(0f, FullTurnDegrees);
            if (slot == null || !context.Config.TryGetArchetype(slot.ArchetypeId, out IslandArchetype archetype))
            {
                return;
            }

            slot.VariantIndex = archetype.SelectVariantIndex(variantRoll);
            IslandVariant variant = slot.VariantIndex < archetype.Variants.Count ? archetype.Variants[slot.VariantIndex] : null;
            // Consume both rolls before applying the Terrain rotation contract.
            slot.YawDegrees = IslandRotationRules.SelectYaw(variant != null ? variant.Prefab : null, yaw);
        }
    }
}
