using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class IslandSlotStep : IMapGenerationStep
    {
        public int Order => 200;

        public bool IsValidation => false;

        public void Execute(MapContext context)
        {
            SeededRandom random = context.Fork(MapGenerationStepId.IslandSlots);
            List<Vector2> existing = new List<Vector2>();
            List<Vector2> candidates = new List<Vector2>();
            if (context.Lighthouse != null)
            {
                existing.Add(context.Lighthouse.Position);
            }

            context.Slots.Clear();
            for (int i = 0; i < context.Zones.Count; i++)
            {
                PlaceInZone(context, random, context.Zones[i], existing, candidates);
            }
        }

        private static void PlaceInZone(MapContext context, SeededRandom random, ZoneRing ring, List<Vector2> existing, List<Vector2> candidates)
        {
            ZoneConfig config = ring.Config;
            RingDomain domain = new RingDomain(Vector2.zero, ring.InnerRadius, ring.OuterRadius);
            if (!PoissonDiskSampler.TrySample(random, domain, config.MinIslandDistance, int.MaxValue, candidates, existing))
            {
                context.AddIssue($"{ring.Zone}: 섬 후보 샘플링 인자가 올바르지 않음");
                return;
            }

            random.Shuffle(candidates);
            int target = random.NextInt(config.MinIslands, config.MaxIslands + 1);
            int count = Mathf.Min(target, candidates.Count);
            if (count < config.MinIslands)
            {
                context.AddIssue($"{ring.Zone}: 섬 후보 {candidates.Count}개 < 최소 {config.MinIslands}개");
            }

            for (int i = 0; i < count; i++)
            {
                context.Slots.Add(new IslandSlot(candidates[i], ring.Zone));
                existing.Add(candidates[i]);
            }
        }
    }
}
