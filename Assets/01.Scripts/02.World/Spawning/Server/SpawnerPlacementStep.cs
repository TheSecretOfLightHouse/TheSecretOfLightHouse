using System;
using System.Collections.Generic;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Spawning.Rules;
using UnityEngine;

namespace Lighthouse.World.Spawning.Server
{
    public static class SpawnerPlacementStep
    {
        private const int MaxAllocationCount = 4096;

        public static SpawnerRegistry Build(MapLayout layout,
            MapGenerationConfig mapConfig, SpawnerPlacementConfig config, ulong seed)
        {
            if (layout == null || layout.Islands.Count == 0
                || mapConfig == null || config == null)
            {
                throw new ArgumentException("Map and spawning settings are required.");
            }

            if (!IsFinite(config.SpreadFactor) || config.SpreadFactor <= 0f
                || !IsFinite(config.MinSpacingFloor) || config.MinSpacingFloor < 1f
                || !IsFinite(config.QueryCellSize) || config.QueryCellSize < 1f
                || !IsFinite(config.ObstacleClearance) || config.ObstacleClearance < 0f
                || !IsFinite(config.SafeRadius) || config.SafeRadius < 0f
                || !IsFinite(mapConfig.RadiusScale) || mapConfig.RadiusScale <= 0f)
            {
                throw new ArgumentException("Invalid spawning dimensions.");
            }

            var points = new List<SpawnerPoint>();
            var positions = new List<Vector2>();
            var candidates = new List<Vector2>();
            var profiles = new Dictionary<int, SpawnerProfile>();
            var random = new SeededRandom(seed);

            for (int i = 0; i < config.Allocations.Count; i++)
            {
                SpawnerAllocation allocation = config.Allocations[i];
                if (allocation == null || !allocation.IsValid
                    || allocation.MaxCount > MaxAllocationCount)
                {
                    throw new ArgumentException($"Invalid allocation at index {i}.");
                }

                SpawnerProfile profile = allocation.Profile;
                if (profiles.TryGetValue(profile.Id, out SpawnerProfile previous)
                    && previous != profile)
                {
                    throw new ArgumentException($"Duplicate profile ID {profile.Id}.");
                }

                profiles[profile.Id] = profile;
                ZoneConfig zone = FindZone(mapConfig, allocation.Zone);
                var ring = new RingDomain(Vector2.zero,
                    zone.InnerRadius * mapConfig.RadiusScale,
                    zone.OuterRadius * mapConfig.RadiusScale);
                ValidateRing(ring);

                int target = random.NextInt(allocation.MinCount, allocation.MaxCount + 1);
                if (target == 0)
                {
                    continue;
                }

                var domain = new SpawnerSampleDomain(ring, layout,
                    config.ObstacleClearance, config.SafeRadius);

                float spacing = SpawnerSpacingEstimator.Estimate(random, domain, target,
                    config.SpreadFactor, config.MinSpacingFloor);

                SpawnerRandomPlacer.TryPlace(random, domain, spacing,
                    config.MinSpacingFloor, target, candidates, positions);

                if (candidates.Count < allocation.MinCount)
                {
                    throw new InvalidOperationException(
                        $"Allocation {i} requires {allocation.MinCount}, placed {candidates.Count}.");
                }

                for (int j = 0; j < candidates.Count; j++)
                {
                    Vector2 position = candidates[j];
                    positions.Add(position);
                    points.Add(new SpawnerPoint(
                        new Vector3(position.x, 0f, position.y),
                        allocation.Zone, profile.Id));
                }
            }

            return new SpawnerRegistry(points, config.QueryCellSize);
        }

        private static ZoneConfig FindZone(MapGenerationConfig config, SeaZone zone)
        {
            ZoneConfig found = null;
            for (int i = 0; i < config.Zones.Count; i++)
            {
                ZoneConfig candidate = config.Zones[i];
                if (candidate == null || candidate.Zone != zone)
                {
                    continue;
                }

                if (found != null)
                {
                    throw new ArgumentException($"Duplicate zone {zone}.");
                }

                found = candidate;
            }

            if (found == null)
            {
                throw new ArgumentException($"Missing zone {zone}.");
            }

            return found;
        }

        private static void ValidateRing(RingDomain ring)
        {
            if (!IsFinite(ring.InnerRadius) || !IsFinite(ring.OuterRadius)
                || ring.InnerRadius < 0f || ring.OuterRadius <= ring.InnerRadius)
            {
                throw new ArgumentException("Invalid spawning ring.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
