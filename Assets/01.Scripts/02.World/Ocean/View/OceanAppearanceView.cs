using System;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Clock.View;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.View
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1001)]
    public sealed class OceanAppearanceView : MonoBehaviour
    {
        private const int ZoneCount = 3;

        [SerializeField] private GameClock _gameClock;
        [SerializeField] private DayNightSunConfig _sunConfig;
        [SerializeField] private WeatherCatalog _weatherCatalog;
        [SerializeField] private OceanOpticsProfile _profile;
        [SerializeField] private MapGenerationConfig _mapConfig;
        [SerializeField] private Material _sourceMaterial;
        [SerializeField] private Renderer[] _renderers;

        private IClock _clock;
        private IWeatherService _weather;
        private OceanOpticsMaterial _material;
        private float _calmIntensity;
        private float _stormIntensity;
        private float _abyssIntensity;

        private void OnEnable()
        {
            _clock = null;
            _weather = null;

            if (!TryPrepare())
            {
                Debug.LogError(
                    $"[{nameof(OceanAppearanceView)}] Check clock, sun config, optics profile, weather catalog and single-material ocean renderers.");
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (_material == null || _gameClock == null ||
                _profile == null || _sunConfig == null)
            {
                return;
            }

            if (_clock == null || _weather == null)
            {
                if (!WorldServices.TryGet<IClock>(out _clock) ||
                    !WorldServices.TryGet<IWeatherService>(out _weather))
                {
                    return;
                }
            }

            if (!_gameClock.IsRunning)
            {
                return;
            }

            DayCycleConfig cycle = _gameClock.Config;

            if (cycle == null)
            {
                return;
            }

            float daylight = OceanOpticsModel.GetDaylight(
                _clock.Now - _gameClock.GameStartTime,
                cycle.DaySeconds,
                cycle.NightSeconds,
                _sunConfig.HorizonBlend);

            float intensity = _weather.Snapshot.StormIntensity;

            float stormWeight = Mathf.InverseLerp(
                _calmIntensity, _stormIntensity, intensity);

            float abyssWeight = 0f;

            if (_weather.Target == WeatherType.Maelstrom)
            {
                abyssWeight = Mathf.InverseLerp(
                    _stormIntensity, _abyssIntensity, intensity);
            }

            OceanOpticsState state = OceanOpticsModel.Evaluate(
                _profile, daylight, stormWeight, abyssWeight);

            _material.Apply(in state);
        }

        private void OnDisable()
        {
            if (_material != null)
            {
                _material.Dispose();
                _material = null;
            }
        }

        private bool TryPrepare()
        {
            if (_gameClock == null || _gameClock.Config == null ||
                _sunConfig == null || _profile == null ||
                _weatherCatalog == null)
            {
                return false;
            }

            if (!_weatherCatalog.TryGetProfile(
                    WeatherType.Calm, out WeatherProfile calm) ||
                !_weatherCatalog.TryGetProfile(
                    WeatherType.Storm, out WeatherProfile storm) ||
                !_weatherCatalog.TryGetProfile(
                    WeatherType.Maelstrom, out WeatherProfile abyss))
            {
                return false;
            }

            _calmIntensity = calm.StormIntensity;
            _stormIntensity = storm.StormIntensity;
            _abyssIntensity = abyss.StormIntensity;

            if (!(_calmIntensity < _stormIntensity &&
                _stormIntensity < _abyssIntensity))
            {
                return false;
            }

            if (!OceanOpticsMaterial.TryCreate(
                    _renderers, _sourceMaterial, out _material))
            {
                return false;
            }

            ApplyZones();
            return true;
        }

        private void ApplyZones()
        {
            if (_mapConfig == null || _mapConfig.Zones.Count != ZoneCount ||
                !(_mapConfig.RadiusScale > 0f))
            {
                return;
            }

            Span<float> radii = stackalloc float[ZoneCount];
            float previous = 0f;

            for (int i = 0; i < ZoneCount; i++)
            {
                ZoneConfig zone = _mapConfig.Zones[i];

                if (zone == null)
                {
                    return;
                }

                radii[i] = zone.OuterRadius * _mapConfig.RadiusScale;

                if (!(radii[i] > previous) || float.IsInfinity(radii[i]))
                {
                    return;
                }

                previous = radii[i];
            }

            _material.ApplyZones(_profile, radii[0], radii[1], radii[2]);
        }
    }
}
