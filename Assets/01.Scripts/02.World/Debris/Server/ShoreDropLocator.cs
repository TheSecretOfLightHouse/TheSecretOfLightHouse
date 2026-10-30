using System;
using System.Collections.Generic;
using Lighthouse.World.Debris.Rules;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Debris.Server
{
    public sealed class ShoreDropLocator : IShoreDropLocator
    {
        private const int MaxTrackedSlots = 16;
        private const float MinSeparationScale = 0.75f;
        private const int MaxIslandAttempts = 4;

        private readonly IReadOnlyList<IslandPlacement> _islands;
        private readonly IGroundHeightSource _ground;
        private readonly IWorldQuery _world;
        private readonly IRegionGate _regions;
        private readonly DebrisConfig _config;

        public ShoreDropLocator(
            IReadOnlyList<IslandPlacement> islands,
            IGroundHeightSource ground,
            IWorldQuery world,
            IRegionGate regions,
            DebrisConfig config)
        {
            _islands = islands ?? throw new ArgumentNullException(nameof(islands));
            _ground = ground ?? throw new ArgumentNullException(nameof(ground));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _regions = regions ?? throw new ArgumentNullException(nameof(regions));
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
        }

        // A failed island is excluded and the next candidate is tried, so a single cluttered island cannot leave the
        // wreck without a drop point. The first island's failure is reported when every attempt fails.
        public ShoreDropResult GetDropPoint(in ShoreDropRequest request)
        {
            Span<short> tried = stackalloc short[MaxIslandAttempts];
            int count = 0;
            ShoreDropResult firstFailure = ShoreDropResult.Fail(ShoreFailure.NoCandidateIsland);

            while (count < MaxIslandAttempts)
            {
                if (!ShoreIslandSelector.TrySelect(
                    _islands, request.Origin, request.Kind, _regions.UnlockedZones, null, tried.Slice(0, count), out IslandPlacement island))
                {
                    break;
                }

                ShoreDropResult result = TryLocateOnIsland(island, request);
                if (result.IsSuccess)
                {
                    return result;
                }

                if (count == 0)
                {
                    firstFailure = result;
                }

                tried[count++] = island.Index;
            }

            return firstFailure;
        }

        private ShoreDropResult TryLocateOnIsland(IslandPlacement island, in ShoreDropRequest request)
        {
            Vector2 center = island.Position;
            Vector2 origin = new Vector2(request.Origin.x, request.Origin.z);
            Vector2 outward = ShoreSlotMath.GetOutward(center, origin);
            float waterHeight = _world.SampleWaterHeight(new Vector3(center.x, 0f, center.y));
            float minHeight = waterHeight + _config.MinHeightAboveWater;

            if (!TryFindShore(island, center, outward, minHeight, out outward, out float baseDistance))
            {
                return ShoreDropResult.Fail(ShoreFailure.ShoreNotFound);
            }

            Span<Vector2> taken = stackalloc Vector2[MaxTrackedSlots];
            int count = 0;
            int tracked = Mathf.Min(request.Slot, MaxTrackedSlots);
            for (int slot = 0; slot < tracked; slot++)
            {
                if (TryResolveSlot(island, center, minHeight, outward, baseDistance, slot, taken.Slice(0, count), out Vector2 earlier, out _)
                    == ShoreFailure.None)
                {
                    taken[count++] = earlier;
                }
            }

            ShoreFailure failure = TryResolveSlot(
                island, center, minHeight, outward, baseDistance, request.Slot, taken.Slice(0, count), out Vector2 spot, out float height);
            if (failure != ShoreFailure.None)
            {
                return ShoreDropResult.Fail(failure);
            }

            return ShoreDropResult.Success(new Vector3(spot.x, height, spot.y), island.Index);
        }

        private ShoreFailure TryResolveSlot(
            IslandPlacement island,
            Vector2 center,
            float minHeight,
            Vector2 outward,
            float baseDistance,
            int slot,
            ReadOnlySpan<Vector2> taken,
            out Vector2 spot,
            out float height)
        {
            spot = center;
            height = 0f;

            Vector2 slotPoint = ShoreSlotMath.GetPoint(center, outward, baseDistance, 0f, slot, _config.SlotSpacing);
            if (!TryFindShore(island, center, slotPoint - center, minHeight, out Vector2 slotDirection, out float slotDistance))
            {
                return ShoreFailure.ShoreNotFound;
            }

            Vector2 point = center + slotDirection * Mathf.Max(0f, slotDistance - _config.ShoreInset);
            return ShoreSpotSearch.TryFind(
                _ground,
                island.Index,
                point,
                -slotDirection,
                minHeight,
                _config.MaxSlopeDegrees,
                _config.SpotSearchRadius,
                _config.SpotSearchStep,
                taken,
                _config.SlotSpacing * MinSeparationScale,
                out spot,
                out height)
                ? ShoreFailure.None
                : ShoreFailure.UnsafeSpot;
        }

        private bool TryFindShore(
            IslandPlacement island,
            Vector2 center,
            Vector2 direction,
            float minHeight,
            out Vector2 foundDirection,
            out float distance)
        {
            return ShoreLineSearch.TryFindNearestShore(
                _ground,
                island.Index,
                center,
                direction,
                island.Radius,
                _config.SearchStep,
                minHeight,
                _config.MissCountToExit,
                out foundDirection,
                out distance);
        }
    }
}
