using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Environment.View
{
    public sealed class EnvironmentRegionSampler
    {
        private const int RegionCount = 3;

        private readonly SkyEnvironmentState[] _days;
        private readonly SkyEnvironmentState[] _nights;
        private readonly float[] _boundaries;
        private readonly float _halfWidth;

        private EnvironmentRegionSampler(
            MapGenerationConfig map,
            SeaEnvironmentProfile[] profiles,
            float halfWidth)
        {
            _days = new SkyEnvironmentState[RegionCount];
            _nights = new SkyEnvironmentState[RegionCount];
            _boundaries = new float[RegionCount - 1];

            float previous = 0f;
            float minimumWidth = float.PositiveInfinity;

            for (int i = 0; i < RegionCount; i++)
            {
                _days[i] = profiles[i].Day;
                _nights[i] = profiles[i].Night;

                float radius = map.Zones[i].OuterRadius * map.RadiusScale;
                minimumWidth = Mathf.Min(minimumWidth, radius - previous);
                previous = radius;

                if (i < _boundaries.Length)
                {
                    _boundaries[i] = radius;
                }
            }

            _halfWidth = Mathf.Min(halfWidth, minimumWidth * 0.49f);
        }

        public static bool TryCreate(
            MapGenerationConfig map,
            SeaEnvironmentProfile[] profiles,
            float halfWidth,
            out EnvironmentRegionSampler sampler)
        {
            sampler = null;

            if (map == null || profiles == null ||
                profiles.Length != RegionCount || map.Zones.Count != RegionCount ||
                !(map.RadiusScale > 0f) || float.IsInfinity(map.RadiusScale) ||
                !(halfWidth > 0f) || float.IsInfinity(halfWidth))
            {
                return false;
            }

            float previous = 0f;

            for (int i = 0; i < RegionCount; i++)
            {
                SeaZone expected = (SeaZone)(1 << i);
                ZoneConfig zone = map.Zones[i];

                if (zone == null || zone.Zone != expected ||
                    profiles[i] == null || profiles[i].Zone != expected)
                {
                    return false;
                }

                float radius = zone.OuterRadius * map.RadiusScale;

                if (float.IsNaN(radius) || float.IsInfinity(radius) ||
                    radius <= previous)
                {
                    return false;
                }

                previous = radius;
            }

            sampler = new EnvironmentRegionSampler(map, profiles, halfWidth);
            return true;
        }

        public void Evaluate(
            Vector3 position,
            out SkyEnvironmentState fromDay,
            out SkyEnvironmentState fromNight,
            out SkyEnvironmentState toDay,
            out SkyEnvironmentState toNight,
            out float blend)
        {
            float radius = new Vector2(position.x, position.z).magnitude;
            int from = RegionCount - 1;
            int to = from;
            blend = 0f;

            for (int i = 0; i < _boundaries.Length; i++)
            {
                if (radius <= _boundaries[i] + _halfWidth)
                {
                    from = i;
                    to = i + 1;
                    blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
                        _boundaries[i] - _halfWidth,
                        _boundaries[i] + _halfWidth,
                        radius));
                    break;
                }
            }

            fromDay = _days[from];
            fromNight = _nights[from];
            toDay = _days[to];
            toNight = _nights[to];
        }
    }
}
