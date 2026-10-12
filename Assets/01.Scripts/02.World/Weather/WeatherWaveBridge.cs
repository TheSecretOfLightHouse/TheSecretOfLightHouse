using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.Rules;
using UnityEngine;

namespace Lighthouse.World.Weather
{
    public class WeatherWaveBridge : MonoBehaviour, IWeatherListener
    {
        private IWeatherService _service;
        private WaterSamplerHost _host;
        private bool _isInvalidLogged;

        private void OnEnable()
        {
            if (!WorldServices.TryGet(out _service) || !WorldServices.TryGet(out _host))
            {
                Debug.LogError($"[{nameof(WeatherWaveBridge)}] IWeatherService and WaterSamplerHost are required.");
                enabled = false;
                return;
            }

            _service.AddListener(this);
            HandleWeatherChanged(in _service.Snapshot);
        }

        private void OnDisable()
        {
            if (_service != null)
            {
                _service.RemoveListener(this);
            }
        }

        public void HandleWeatherChanged(in WeatherSnapshot snapshot)
        {
            if (_host == null)
            {
                return;
            }

            if (_host.Inputs.TrySetWaves(snapshot.WaveSpan))
            {
                _isInvalidLogged = false;
                return;
            }

            if (!_isInvalidLogged)
            {
                Debug.LogError($"[{nameof(WeatherWaveBridge)}] Snapshot waves were rejected by WaveModel.IsValid.");
                _isInvalidLogged = true;
            }
        }
    }
}
