using Lighthouse.World.Weather.Rules;
using UnityEngine;

namespace Lighthouse.World.Environment.View
{
    public sealed class WeatherEnvironmentSampler
    {
        private const int ProfileCount = 5;

        private readonly float[] _intensities;
        private readonly WeatherEnvironmentState[] _states;

        public SkyEnvironmentState AbyssSky { get; }

        private WeatherEnvironmentSampler(
            WeatherCatalog catalog,
            WeatherEnvironmentProfile[] profiles)
        {
            _intensities = new float[ProfileCount];
            _states = new WeatherEnvironmentState[ProfileCount];

            for (int i = 0; i < ProfileCount; i++)
            {
                catalog.TryGetProfile((WeatherType)i, out WeatherProfile profile);
                _intensities[i] = profile.StormIntensity;
                _states[i] = profiles[i].Appearance;
            }

            AbyssSky = profiles[(int)WeatherType.Maelstrom].AbyssSky;
        }

        public static bool TryCreate(
            WeatherCatalog catalog,
            WeatherEnvironmentProfile[] profiles,
            out WeatherEnvironmentSampler sampler)
        {
            sampler = null;

            if (catalog == null || profiles == null ||
                profiles.Length != ProfileCount)
            {
                return false;
            }

            float previous = -1f;

            for (int i = 0; i < ProfileCount; i++)
            {
                if (profiles[i] == null || (int)profiles[i].Type != i ||
                    !catalog.TryGetProfile((WeatherType)i, out WeatherProfile profile))
                {
                    return false;
                }

                float intensity = profile.StormIntensity;

                if (float.IsNaN(intensity) || float.IsInfinity(intensity) ||
                    intensity < 0f || intensity <= previous)
                {
                    return false;
                }

                previous = intensity;
            }

            sampler = new WeatherEnvironmentSampler(catalog, profiles);
            return true;
        }

        public bool TryEvaluate(
            float intensity,
            out WeatherEnvironmentState state)
        {
            state = default;

            if (float.IsNaN(intensity) || float.IsInfinity(intensity))
            {
                return false;
            }

            for (int i = 1; i < ProfileCount; i++)
            {
                if (intensity <= _intensities[i])
                {
                    float blend = Mathf.InverseLerp(
                        _intensities[i - 1], _intensities[i], intensity);

                    state = WeatherEnvironmentState.Lerp(
                        in _states[i - 1], in _states[i], blend);
                    return true;
                }
            }

            state = _states[ProfileCount - 1];
            return true;
        }
    }
}
