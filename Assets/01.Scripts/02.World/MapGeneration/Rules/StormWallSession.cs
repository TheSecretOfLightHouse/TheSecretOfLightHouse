using System;
using System.Collections.Generic;
using Lighthouse.World.Ocean.Rules;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class StormWallSession : IDisposable
    {
        private readonly ForceFieldRegistry _registry;
        private readonly StormWallField[] _fields;
        private readonly WaveModifierRegistry _modifiers;
        private readonly StormWallWaveSource _waveSource;

        public IReadOnlyList<StormWallField> Fields => _fields;

        public StormWallSession(MapGenerationConfig mapConfig,
            StormWallConfig wallConfig, float safeRadius,
            IRegionGate regions, ForceFieldRegistry registry,
            WaveModifierRegistry modifiers)
        {
            if (mapConfig == null || wallConfig == null || !wallConfig.IsValid
                || regions == null || registry == null || modifiers == null)
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

            if (mapConfig.SailingChannelWidth < wallConfig.Width)
            {
                throw new ArgumentException("Sailing channel is narrower than the storm wall.");
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

            _modifiers = modifiers;
            _waveSource = new StormWallWaveSource(_fields, wallConfig);

            try
            {
                for (int i = 0; i < _fields.Length; i++)
                {
                    if (!_registry.Register(_fields[i], true))
                    {
                        throw new InvalidOperationException("Storm wall registration failed.");
                    }
                }

                if (!_modifiers.Register(_waveSource))
                {
                    throw new InvalidOperationException("Storm wave registration failed or capacity is full.");
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
            _modifiers.Unregister(_waveSource);

            for (int i = 0; i < _fields.Length; i++)
            {
                _registry.Unregister(_fields[i]);
            }
        }
    }
}
