using System;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Clock.View
{
    [DisallowMultipleComponent]
    public sealed class DayNightSunView : MonoBehaviour
    {
        private const float HalfRotation = 180f;

        [SerializeField] private GameClock _gameClock;
        [SerializeField] private Light _sun;
        [SerializeField] private DayNightSunConfig _config;

        private IClock _clock;
        private Quaternion _initialRotation;
        private float _initialIntensity;
        private bool _hasInitialState;

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
                0f, 1f, Mathf.Clamp01(elevation / _config.HorizonBlend));

            _sun.transform.rotation = Quaternion.Euler(angle, _config.Azimuth, 0f);
            _sun.intensity = Mathf.Lerp(
                _config.NightIntensity, _config.DayIntensity, daylight);
        }

        private void OnDisable()
        {
            if (_sun != null && _hasInitialState)
            {
                _sun.transform.rotation = _initialRotation;
                _sun.intensity = _initialIntensity;
            }
        }
    }
}
