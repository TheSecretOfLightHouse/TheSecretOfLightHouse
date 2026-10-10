using System;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Query.Rules.Mocks
{
    public class FlatWorldQuery : IWorldQuery
    {
        private const float FlatWaterHeight = 0f;
        private const float NeutralThreatMultiplier = 1f;

        public float NightThreatMultiplier => NeutralThreatMultiplier;

        public float SampleWaterHeight(Vector3 worldPos)
        {
            return FlatWaterHeight;
        }

        public void SampleWaterHeights(ReadOnlySpan<Vector3> points, Span<float> heights)
        {
            int count = Math.Min(points.Length, heights.Length);
            heights.Slice(0, count).Fill(FlatWaterHeight);
        }

        public SeaZone GetZoneAt(Vector3 worldPos)
        {
            return SeaZone.Inner;
        }

        public bool IsSafeZone(Vector3 worldPos)
        {
            return true;
        }

        public bool TryFindNearestIsland(Vector3 from, out IslandHandle island)
        {
            island = default;
            return false;
        }
    }
}
