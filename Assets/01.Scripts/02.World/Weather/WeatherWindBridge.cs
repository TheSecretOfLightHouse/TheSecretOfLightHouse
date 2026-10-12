using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.Rules;
using UnityEngine;

namespace Lighthouse.World.Weather
{
    public class WeatherWindBridge : MonoBehaviour
    {
        [SerializeField] private float _forceCoefficient = 1f;

        private IWeatherService _service;
        private ForceFieldRegistry _registry;
        private WeatherWindSource _source;
        private WindField _field;

        private void OnEnable()
        {
            if (!WorldServices.TryGet(out _service) || !WorldServices.TryGet(out _registry))
            {
                Debug.LogError($"[{nameof(WeatherWindBridge)}] IWeatherService and ForceFieldRegistry are required.");
                enabled = false;
                return;
            }

            _source = new WeatherWindSource();
            _source.HandleWeatherChanged(in _service.Snapshot);
            _service.AddListener(_source);

            _field = new WindField(_source, _forceCoefficient);
            _registry.Register(_field, false);
        }

        private void OnDisable()
        {
            if (_source == null)
            {
                return;
            }

            _service.RemoveListener(_source);
            _registry.Unregister(_field);
            _source = null;
            _field = null;
        }
    }
}
