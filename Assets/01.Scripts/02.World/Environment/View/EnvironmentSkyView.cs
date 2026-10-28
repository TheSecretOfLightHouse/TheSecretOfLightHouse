using System;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Clock.View;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.Rules;
using UnityEngine;

namespace Lighthouse.World.Environment.View
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1100)]
    public sealed class EnvironmentSkyView : MonoBehaviour
    {
        [SerializeField] private Material _sourceMaterial;
        [SerializeField] private Transform _observer;
        [SerializeField] private GameClock _gameClock;
        [SerializeField] private DayNightSunConfig _sunConfig;
        [SerializeField] private MapGenerationConfig _mapConfig;
        [SerializeField] private WeatherCatalog _weatherCatalog;
        [SerializeField] private SeaEnvironmentProfile[] _seaProfiles;
        [SerializeField] private WeatherEnvironmentProfile[] _weatherProfiles;
        [SerializeField, Min(0.1f)] private float _boundaryHalfWidth = 20f;

        private const float AmbientStep = 0.03f;
        private const float AmbientMinInterval = 0.5f;

        private float _ambientDaylight = -1f;
        private float _ambientLightScale = -1f;
        private float _ambientNextTime;

        private EnvironmentRegionSampler _regions;
        private WeatherEnvironmentSampler _weatherSampler;
        private EnvironmentSkyMaterial _material;

        public SkyEnvironmentState CurrentState { get; private set; }
        public bool HasState { get; private set; }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            _ambientDaylight = -1f;
            _ambientLightScale = -1f;
            _ambientNextTime = 0f;

            if (_gameClock == null || _gameClock.Config == null ||
                _sunConfig == null ||
                !EnvironmentRegionSampler.TryCreate(
                    _mapConfig, _seaProfiles, _boundaryHalfWidth, out _regions) ||
                !WeatherEnvironmentSampler.TryCreate(
                    _weatherCatalog, _weatherProfiles, out _weatherSampler))
            {
                Debug.LogError(
                    $"[{nameof(EnvironmentSkyView)}] Check clock, sun config, map, catalog and ordered environment profiles.");
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (_regions == null || _weatherSampler == null ||
                _observer == null || _gameClock == null ||
                !_gameClock.IsRunning || _gameClock.Config == null ||
                _sunConfig == null)
            {
                return;
            }

            if (!WorldServices.TryGet<IClock>(out IClock clock) ||
                !WorldServices.TryGet<IWeatherService>(out IWeatherService weather))
            {
                return;
            }

            if (!_weatherSampler.TryEvaluate(
                weather.Snapshot.StormIntensity,
                out WeatherEnvironmentState appearance))
            {
                Debug.LogError(
                    $"[{nameof(EnvironmentSkyView)}] Weather intensity must be finite.");
                enabled = false;
                return;
            }

            if (_material == null &&
                !EnvironmentSkyMaterial.TryCreate(_sourceMaterial, out _material))
            {
                Debug.LogError(
                    $"[{nameof(EnvironmentSkyView)}] Check the EnvironmentSky material and disable other sky previews.");
                enabled = false;
                return;
            }

            double elapsed = Math.Max(0d, clock.Now - _gameClock.GameStartTime);
            float daylight = EnvironmentClockModel.GetDaylight(
                elapsed, _gameClock.Config, _sunConfig);

            _regions.Evaluate(
                _observer.position,
                out SkyEnvironmentState fromDay,
                out SkyEnvironmentState fromNight,
                out SkyEnvironmentState toDay,
                out SkyEnvironmentState toNight,
                out float regionBlend);

            SkyEnvironmentState state = EnvironmentBlendModel.Evaluate(
                in fromDay, in fromNight, in toDay, in toNight,
                regionBlend, daylight, in appearance, _weatherSampler.AbyssSky);

            CurrentState = state;
            HasState = true;

            float skyDaylight = daylight * (1f - appearance.AbyssWeight);
            _material.Apply(in state, skyDaylight, (float)elapsed);
            RefreshAmbient(skyDaylight, state.LightScale);
        }

        private void RefreshAmbient(float daylight, float lightScale)
        {
            float change = Mathf.Abs(daylight - _ambientDaylight) +
                Mathf.Abs(lightScale - _ambientLightScale);

            if (change < AmbientStep || Time.unscaledTime < _ambientNextTime)
            {
                return;
            }

            DynamicGI.UpdateEnvironment();
            _ambientDaylight = daylight;
            _ambientLightScale = lightScale;
            _ambientNextTime = Time.unscaledTime + AmbientMinInterval;
        }

        private void OnDisable()
        {
            HasState = false;
            CurrentState = default;

            if (_material != null)
            {
                _material.Dispose();
                _material = null;
            }

            _regions = null;
            _weatherSampler = null;
        }

        public void SetObserver(Transform observer)
        {
            _observer = observer;
        }
    }
}
