using System;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Environment.View;
using UnityEngine;

namespace Lighthouse.World.Clock.View
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1200)]
    public sealed class DayNightSunView : MonoBehaviour
    {
        private const float HalfRotation = 180f;
        private static readonly int SkyNightBlendId = Shader.PropertyToID("_SkyNightBlend");

        [SerializeField] private GameClock _gameClock;
        [SerializeField] private Light _sun;
        [SerializeField] private DayNightSunConfig _config;
        [SerializeField] private EnvironmentSkyView _environment;
        [SerializeField, Min(0f)] private float _maxFlashIntensity = 2f;

        private IClock _clock;
        private Quaternion _initialRotation;
        private float _initialIntensity;
        private bool _hasInitialState;
        private float _initialSkyNightBlend;
        private float _flashIntensity;

        private void OnEnable()
        {
            _initialSkyNightBlend = Shader.GetGlobalFloat(SkyNightBlendId);
        }

        private void Awake()
        {
            if (_sun != null)
            {
                _initialRotation = _sun.transform.rotation;
                _initialIntensity = _sun.intensity;
                _hasInitialState = true;
            }
        }

        private void Start()
        {
            if (_gameClock == null || _gameClock.Config == null ||
                _sun == null || _config == null ||
                _sun.type != LightType.Directional ||
                !WorldServices.TryGet<IClock>(out _clock))
            {
                Debug.LogError(
                    $"[{nameof(DayNightSunView)}] Clock, directional light and config are required.");
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (_gameClock == null || !_gameClock.IsRunning ||
                _sun == null || _config == null || _clock == null)
            {
                return;
            }

            DayCycleConfig cycleConfig = _gameClock.Config;
            if (cycleConfig == null || cycleConfig.DaySeconds <= 0f ||
                cycleConfig.NightSeconds <= 0f)
            {
                return;
            }

            double elapsed = Math.Max(0.0, _clock.Now - _gameClock.GameStartTime);
            double inCycle = elapsed % cycleConfig.CycleSeconds;
            float angle;

            if (inCycle < cycleConfig.DaySeconds)
            {
                angle = (float)(inCycle / cycleConfig.DaySeconds) * HalfRotation;
            }
            else
            {
                double nightElapsed = inCycle - cycleConfig.DaySeconds;
                angle = HalfRotation +
                    (float)(nightElapsed / cycleConfig.NightSeconds) * HalfRotation;
            }

            float elevation = Mathf.Sin(angle * Mathf.Deg2Rad);
            float daylight = Mathf.SmoothStep(
                0f, 1f, Mathf.InverseLerp(-_config.HorizonBlend, _config.HorizonBlend, elevation));
            // Keep twilight on both sides of the horizon instead of finishing at sunset.
            float twilightElevation = Mathf.Sin(_config.SkyTwilightElevation * Mathf.Deg2Rad);
            float skyDaylight = Mathf.SmoothStep(
                0f, 1f, Mathf.InverseLerp(-twilightElevation, twilightElevation, elevation));

            _sun.transform.rotation = Quaternion.Euler(angle, _config.Azimuth, 0f);
            float weatherScale = _environment != null && _environment.HasState
                ? _environment.WeatherLightScale : 1f;
            _sun.intensity = Mathf.Lerp(
                _config.NightIntensity, _config.DayIntensity, daylight) * weatherScale +
                Mathf.Clamp(_flashIntensity, 0f, _maxFlashIntensity);
            Shader.SetGlobalFloat(SkyNightBlendId, 1f - skyDaylight);
        }

        private void OnDisable()
        {
            _flashIntensity = 0f;
            Shader.SetGlobalFloat(SkyNightBlendId, _initialSkyNightBlend);
            if (_sun != null && _hasInitialState)
            {
                _sun.transform.rotation = _initialRotation;
                _sun.intensity = _initialIntensity;
            }
        }

        public void SetWeatherFlash(float intensity)
        {
            _flashIntensity = float.IsNaN(intensity) || float.IsInfinity(intensity)
                ? 0f : Mathf.Clamp(intensity, 0f, _maxFlashIntensity);
        }
    }
}
