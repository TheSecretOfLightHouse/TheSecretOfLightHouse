using System;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Query.Rules
{
    public sealed class MapGeometry
    {
        private readonly MapLayout _layout;
        private readonly SeaZone[] _zones;
        private readonly double[] _outerRadiusSquared;
        private readonly SafeZone _safeZone;

        public MapGeometry(MapLayout layout, MapGenerationConfig config, float safeRadius)
        {
            if (layout == null || layout.Islands.Count == 0 || config == null)
            {
                throw new ArgumentException("A completed map is required.");
            }

            _layout = layout;
            _safeZone = new SafeZone(layout.Islands[0].Position, safeRadius);
            _zones = new SeaZone[config.Zones.Count];
            _outerRadiusSquared = new double[config.Zones.Count];

            double previous = 0d;
            for (int i = 0; i < config.Zones.Count; i++)
            {
                ZoneConfig zone = config.Zones[i];
                if (zone == null)
                {
                    throw new ArgumentException("A map zone is missing.");
                }

                double radius = (double)zone.OuterRadius * config.RadiusScale;
                if (double.IsNaN(radius) || double.IsInfinity(radius) || radius <= previous)
                {
                    throw new ArgumentException("Zone outer radii must increase.");
                }

                _zones[i] = zone.Zone;
                _outerRadiusSquared[i] = radius * radius;
                previous = radius;
            }
        }

        public SeaZone GetZoneAt(Vector3 position)
        {
            if (!IsFinite(position))
            {
                return SeaZone.None;
            }

            double distanceSquared = (double)position.x * position.x
                + (double)position.z * position.z;

            for (int i = 0; i < _zones.Length; i++)
            {
                if (distanceSquared <= _outerRadiusSquared[i])
                {
                    return _zones[i];
                }
            }

            return SeaZone.None;
        }

        public bool IsSafeZone(Vector3 position)
        {
            return _safeZone.Contains(position);
        }

        public bool TryFindNearestIsland(Vector3 from, out IslandHandle island)
        {
            island = default;
            if (!IsFinite(from))
            {
                return false;
            }

            double nearest = double.PositiveInfinity;
            bool hasIsland = false;

            for (int i = 0; i < _layout.Islands.Count; i++)
            {
                IslandPlacement candidate = _layout.Islands[i];
                double x = (double)from.x - candidate.Position.x;
                double z = (double)from.z - candidate.Position.y;
                double distanceSquared = x * x + z * z;

                if (distanceSquared < nearest)
                {
                    nearest = distanceSquared;
                    island = candidate.ToHandle();
                    hasIsland = true;
                }
            }

            return hasIsland;
        }

        private static bool IsFinite(Vector3 position)
        {
            return !float.IsNaN(position.x) && !float.IsInfinity(position.x)
                && !float.IsNaN(position.y) && !float.IsInfinity(position.y)
                && !float.IsNaN(position.z) && !float.IsInfinity(position.z);
        }
    }
}
