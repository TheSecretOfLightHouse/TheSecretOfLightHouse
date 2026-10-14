using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class ReefPlacementStep : IMapGenerationStep
    {
        public int Order => 500;

        public bool IsValidation => false;

        public void Execute(MapContext context)
        {
            SeededRandom random = context.Fork(MapGenerationStepId.Reefs);
            List<Vector2> accepted = new List<Vector2>();
            List<Vector2> candidates = new List<Vector2>();

            for (int i = 0; i < context.Zones.Count; i++)
            {
                if (context.Zones[i].Config.MaxReefs > 0)
                {
                    PlaceInZone(context, random, context.Zones[i], accepted, candidates);
                }
            }
        }

        private static void PlaceInZone(MapContext context, SeededRandom random, ZoneRing ring, List<Vector2> accepted, List<Vector2> candidates)
        {
            ZoneConfig config = ring.Config;
            RingDomain domain = new RingDomain(Vector2.zero, ring.InnerRadius, ring.OuterRadius);
            if (!PoissonDiskSampler.TrySample(random, domain, config.MinReefDistance, int.MaxValue, candidates, accepted))
            {
                context.AddIssue($"{ring.Zone}: 암초 후보 샘플링 인자가 올바르지 않음");
                return;
            }

            RemoveBlocked(context, candidates, config.ReefRadius);
            random.Shuffle(candidates);

            int target = random.NextInt(config.MinReefs, config.MaxReefs + 1);
            int count = Mathf.Min(target, candidates.Count);
            if (count < config.MinReefs)
            {
                context.AddIssue($"{ring.Zone}: 암초 후보 {candidates.Count}개 < 최소 {config.MinReefs}개");
            }

            for (int i = 0; i < count; i++)
            {
                accepted.Add(candidates[i]);
                context.Reefs.Add(new ReefPlacement(candidates[i], config.ReefRadius, ring.Zone));
            }
        }

        private static void RemoveBlocked(MapContext context, List<Vector2> candidates, float reefRadius)
        {
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                if (IsBlocked(context, candidates[i], reefRadius))
                {
                    candidates.RemoveAt(i);
                }
            }
        }

        private static bool IsBlocked(MapContext context, Vector2 point, float reefRadius)
        {
            if (Overlaps(context, context.Lighthouse, point, reefRadius))
            {
                return true;
            }

            for (int i = 0; i < context.Slots.Count; i++)
            {
                if (Overlaps(context, context.Slots[i], point, reefRadius))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Overlaps(MapContext context, IslandSlot slot, Vector2 point, float reefRadius)
        {
            if (!context.TryGetRadius(slot, out float radius))
            {
                return false;
            }

            float reach = radius + reefRadius;
            return (point - slot.Position).sqrMagnitude < reach * reach;
        }
    }
}
