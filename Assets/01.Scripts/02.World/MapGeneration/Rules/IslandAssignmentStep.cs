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

            AssignMinimums(context, random, archetypes, counts);
            FillRemaining(context, random, archetypes, counts);
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

        private static void AssignMinimums(MapContext context, SeededRandom random, List<IslandArchetype> archetypes, int[] counts)
        {
            List<int> order = new List<int>();
            for (int i = 0; i < archetypes.Count; i++)
            {
                order.Add(i);
            }

            order.Sort((a, b) => CompareByConstraint(context, archetypes[a], archetypes[b]));

            for (int o = 0; o < order.Count; o++)
            {
                int index = order[o];
                IslandArchetype archetype = archetypes[index];
                for (int n = 0; n < archetype.MinPerMap; n++)
                {
                    if (!TryAssignOne(context, random, archetype))
                    {
                        context.AddIssue($"{archetype.name}: 최소 수량 {archetype.MinPerMap}개 중 {n}개만 배정 가능");
                        break;
                    }

                    counts[index]++;
                }
            }
        }

        private static bool TryAssignOne(MapContext context, SeededRandom random, IslandArchetype archetype)
        {
            List<int> candidates = new List<int>();
            List<float> weights = new List<float>();
            for (int i = 0; i < context.Slots.Count; i++)
            {
                IslandSlot slot = context.Slots[i];
                if (slot.ArchetypeId == 0 && archetype.AllowsZone(slot.Zone))
                {
                    candidates.Add(i);
                    weights.Add(archetype.GetWeight(slot.Zone));
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            int picked = WeightedChoice.Pick(random, weights);
            if (picked < 0)
            {
                picked = random.NextInt(0, candidates.Count);
            }

            context.Slots[candidates[picked]].ArchetypeId = archetype.Id;
            return true;
        }

        private static void FillRemaining(MapContext context, SeededRandom random, List<IslandArchetype> archetypes, int[] counts)
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
                weights.Clear();
                for (int a = 0; a < archetypes.Count; a++)
                {
                    bool isFull = archetypes[a].HasMaxLimit && counts[a] >= archetypes[a].MaxPerMap;
                    weights.Add(isFull ? 0f : archetypes[a].GetWeight(slot.Zone));
                }

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

        private static int CompareByConstraint(MapContext context, IslandArchetype a, IslandArchetype b)
        {
            int zoneCompare = CountAllowedZones(context, a).CompareTo(CountAllowedZones(context, b));
            return zoneCompare != 0 ? zoneCompare : a.Id.CompareTo(b.Id);
        }

        private static int CountAllowedZones(MapContext context, IslandArchetype archetype)
        {
            int count = 0;
            for (int i = 0; i < context.Zones.Count; i++)
            {
                if (archetype.AllowsZone(context.Zones[i].Zone))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CompareById(IslandArchetype a, IslandArchetype b)
        {
            return a.Id.CompareTo(b.Id);
        }
    }
}
