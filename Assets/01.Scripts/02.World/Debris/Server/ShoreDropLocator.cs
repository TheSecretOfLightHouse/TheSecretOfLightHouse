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

        public ShoreDropResult GetDropPoint(in ShoreDropRequest request)
        {
            if (!ShoreIslandSelector.TrySelect(
                _islands, request.Origin, request.Kind, _regions.UnlockedZones, null, out IslandPlacement island))
            {
                return ShoreDropResult.Fail(ShoreFailure.NoCandidateIsland);
            }

            Vector2 center = island.Position;
            Vector2 origin = new Vector2(request.Origin.x, request.Origin.z);
            Vector2 outward = ShoreSlotMath.GetOutward(center, origin);
            float waterHeight = _world.SampleWaterHeight(new Vector3(center.x, 0f, center.y));
            float minHeight = waterHeight + _config.MinHeightAboveWater;

            if (!TryFindShore(island, center, outward, minHeight, out float baseDistance))
            {
                return ShoreDropResult.Fail(ShoreFailure.ShoreNotFound);
            }

            Vector2 slotPoint = ShoreSlotMath.GetPoint(
                center, outward, baseDistance, 0f, request.Slot, _config.SlotSpacing);
            Vector2 slotDirection = slotPoint - center;

            if (!TryFindShore(island, center, slotDirection, minHeight, out float slotDistance))
            {
                return ShoreDropResult.Fail(ShoreFailure.ShoreNotFound);
            }

            Vector2 point = center + slotDirection.normalized * Mathf.Max(0f, slotDistance - _config.ShoreInset);

            if (!ShoreSpotValidator.TryValidate(
                _ground, island.Index, point, minHeight, _config.MaxSlopeDegrees, out float height))
            {
                return ShoreDropResult.Fail(ShoreFailure.UnsafeSpot);
            }

            return ShoreDropResult.Success(new Vector3(point.x, height, point.y), island.Index);
        }

        private bool TryFindShore(
            IslandPlacement island,
            Vector2 center,
            Vector2 direction,
            float minHeight,
            out float distance)
        {
            return ShoreLineSearch.TryFindShore(
                _ground,
                island.Index,
                center,
                direction,
                island.Radius,
                _config.SearchStep,
                minHeight,
                _config.MissCountToExit,
                out distance);
        }
    }
}
