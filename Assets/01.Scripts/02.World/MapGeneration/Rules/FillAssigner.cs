using System.Collections.Generic;
using Lighthouse.World.Islands.Rules;

namespace Lighthouse.World.MapGeneration.Rules
{
    public static class FillAssigner
    {
        public static void Assign(MapContext context, SeededRandom random, List<IslandArchetype> archetypes, int[] counts)
        {
            List<int> order = new List<int>();
            for (int i = 0; i < context.Slots.Count; i++)
            {
                if (context.Slots[i].ArchetypeId == 0)
                {
                    order.Add(i);
                }
            }

            random.Shuffle(order);
            List<float> weights = new List<float>();
            for (int o = 0; o < order.Count; o++)
            {
                IslandSlot slot = context.Slots[order[o]];
                BuildWeights(context, slot, archetypes, counts, weights);

                int picked = WeightedChoice.Pick(random, weights);
                if (picked < 0)
                {
                    context.AddIssue($"{slot.Zone} 섬 슬롯에 배정 가능한 아키타입이 없음");
                    continue;
                }

                slot.ArchetypeId = archetypes[picked].Id;
                counts[picked]++;
            }
        }

        private static void BuildWeights(MapContext context, IslandSlot slot, List<IslandArchetype> archetypes, int[] counts, List<float> weights)
        {
            weights.Clear();
            for (int a = 0; a < archetypes.Count; a++)
            {
                IslandArchetype archetype = archetypes[a];
                bool isFull = archetype.HasMaxLimit && counts[a] >= archetype.MaxPerMap;
                bool isBlocked = isFull || !SameTypeSpacing.IsFarEnough(context, slot, archetype);
                weights.Add(isBlocked ? 0f : archetype.GetWeight(slot.Zone));
            }
        }
    }
}
