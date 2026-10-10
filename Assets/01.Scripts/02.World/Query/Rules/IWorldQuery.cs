using System;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Query.Rules
{
    public interface IWorldQuery
    {
        float NightThreatMultiplier { get; }

        float SampleWaterHeight(Vector3 worldPos);
        void SampleWaterHeights(ReadOnlySpan<Vector3> points, Span<float> heights);
        SeaZone GetZoneAt(Vector3 worldPos);
        bool IsSafeZone(Vector3 worldPos);
        bool TryFindNearestIsland(Vector3 from, out IslandHandle island);
    }
}
