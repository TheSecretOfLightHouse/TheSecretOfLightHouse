using System;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Query.Rules
{
    public sealed class WorldQueryFacade : IWorldQuery
    {
        private const float NeutralThreatMultiplier = 1f;

        private readonly MapGeometry _map;
        private readonly WaveInputs _waves;
        private readonly IClock _clock;
        private WaterSamplerHost _sampleHost;

        public float NightThreatMultiplier => NeutralThreatMultiplier;

        public WorldQueryFacade(MapGeometry map, WaveInputs waves, IClock clock)
        {
            if (map == null || waves == null || clock == null)
            {
                throw new ArgumentException("World query dependencies are required.");
            }

            _map = map;
            _waves = waves;
            _clock = clock;
        }

        public float SampleWaterHeight(Vector3 worldPos)
        {
            if (F3WaveField.IsActive)
            {
                if (TryFastHeight(worldPos, (float)_clock.Now, out float height))
                {
                    return height;
                }
                return F3WaveField.HeightAt(_waves.Waves, _waves.Modifiers,
                    worldPos.x, worldPos.z, (float)_clock.Now);
            }
            return WaveModel.HeightAt(
                _waves.Waves, _waves.Modifiers, CoastWaveRegistry.Bounds, CoastWaveRegistry.Edges, CoastWaveRegistry.Profiles,
                worldPos.x, worldPos.z, (float)_clock.Now);
        }

        public void SampleWaterHeights(ReadOnlySpan<Vector3> points, Span<float> heights)
        {
            if (heights.Length < points.Length)
            {
                throw new ArgumentException("The output span is too short.", nameof(heights));
            }

            float time = (float)_clock.Now;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 position = points[i];
                if (F3WaveField.IsActive)
                {
                    if (TryFastHeight(position, time, out float height))
                    {
                        heights[i] = height;
                        continue;
                    }
                    heights[i] = F3WaveField.HeightAt(_waves.Waves, _waves.Modifiers,
                        position.x, position.z, time);
                    continue;
                }
                heights[i] = WaveModel.HeightAt(
                    _waves.Waves, _waves.Modifiers, CoastWaveRegistry.Bounds, CoastWaveRegistry.Edges, CoastWaveRegistry.Profiles,
                    position.x, position.z, time);
            }
        }

        public SeaZone GetZoneAt(Vector3 worldPos)
        {
            return _map.GetZoneAt(worldPos);
        }

        private bool TryFastHeight(Vector3 position, float time, out float height)
        {
            if (_sampleHost == null)
            {
                WorldServices.TryGet(out _sampleHost);
            }
            if (_sampleHost != null && ReferenceEquals(_sampleHost.Inputs, _waves))
            {
                return _sampleHost.TrySampleF3Height(position, time, out height);
            }
            height = 0f;
            return false;
        }

        public bool IsSafeZone(Vector3 worldPos)
        {
            return _map.IsSafeZone(worldPos);
        }

        public bool TryFindNearestIsland(Vector3 from, out IslandHandle island)
        {
            return _map.TryFindNearestIsland(from, out island);
        }
    }
}
