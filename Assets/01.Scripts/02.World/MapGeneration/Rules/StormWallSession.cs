using System;
using System.Collections.Generic;
using Lighthouse.World.Ocean.Rules;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class StormWallSession : IDisposable
    {
        private readonly ForceFieldRegistry _registry;
        private readonly StormWallField[] _fields;

        public IReadOnlyList<StormWallField> Fields => _fields;

        public StormWallSession(MapGenerationConfig mapConfig,
            StormWallConfig wallConfig, float safeRadius,
            IRegionGate regions, ForceFieldRegistry registry)
        {
            if (mapConfig == null || wallConfig == null || !wallConfig.IsValid
                || regions == null || registry == null)
            {
                throw new ArgumentException("Storm wall setup is incomplete.");
            }

            if (mapConfig.Zones.Count != 3
                || mapConfig.Zones[0] == null
                || mapConfig.Zones[1] == null
                || mapConfig.Zones[2] == null
                || mapConfig.Zones[0].Zone != SeaZone.Inner
                || mapConfig.Zones[1].Zone != SeaZone.Middle
                || mapConfig.Zones[2].Zone != SeaZone.Outer)
            {
                throw new ArgumentException("Zones must be Inner, Middle and Outer.");
            }

            float middleRadius = mapConfig.Zones[0].OuterRadius * mapConfig.RadiusScale;
            float outerRadius = mapConfig.Zones[1].OuterRadius * mapConfig.RadiusScale;
            float halfWidth = wallConfig.Width * 0.5f;

            if (float.IsNaN(safeRadius) || float.IsInfinity(safeRadius)
                || safeRadius < 0f || middleRadius - halfWidth <= safeRadius)
            {
                throw new ArgumentException("Storm wall overlaps the safe zone.");
            }

            if (outerRadius - middleRadius <= wallConfig.Width)
            {
                throw new ArgumentException("Storm wall bands overlap.");
            }

            _registry = registry;
            _fields = new[]
            {
                new StormWallField(middleRadius, SeaZone.Middle, regions, wallConfig),
                new StormWallField(outerRadius, SeaZone.Outer, regions, wallConfig)
            };

            try
            {
                for (int i = 0; i < _fields.Length; i++)
                {
                    if (!_registry.Register(_fields[i], true))
                    {
                        throw new InvalidOperationException("Storm wall registration failed.");
                    }
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _fields.Length; i++)
            {
                _registry.Unregister(_fields[i]);
            }
        }
    }
}
