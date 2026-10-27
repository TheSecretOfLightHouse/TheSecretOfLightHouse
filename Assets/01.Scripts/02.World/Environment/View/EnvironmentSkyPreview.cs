using Lighthouse.World.Weather.Rules;
using UnityEngine;

namespace Lighthouse.World.Environment.View
{
    [DisallowMultipleComponent]
    public sealed class EnvironmentSkyPreview : MonoBehaviour
    {
        [SerializeField] private Material _sourceMaterial;
        [SerializeField] private SeaEnvironmentProfile _fromSea;
        [SerializeField] private SeaEnvironmentProfile _toSea;
        [SerializeField] private WeatherEnvironmentProfile _fromWeather;
        [SerializeField] private WeatherEnvironmentProfile _toWeather;
        [SerializeField] private WeatherEnvironmentProfile _abyssProfile;

        [SerializeField, Range(0f, 1f)] private float _regionBlend;
        [SerializeField, Range(0f, 1f)] private float _weatherBlend;
        [SerializeField, Range(0f, 1f)] private float _daylight;

        private EnvironmentSkyMaterial _material;

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!HasInputs() ||
                !EnvironmentSkyMaterial.TryCreate(_sourceMaterial, out _material))
            {
                Debug.LogError(
                    $"[{nameof(EnvironmentSkyPreview)}] Assign all profiles, a supported EnvironmentSky material, and use only one preview.");
                enabled = false;
                return;
            }

            Apply();
        }

        private void Update()
        {
            if (_material == null)
            {
                return;
            }

            if (!HasInputs())
            {
                Debug.LogError(
                    $"[{nameof(EnvironmentSkyPreview)}] A required profile was removed.");
                enabled = false;
                return;
            }

            Apply();
        }

        private void OnDisable()
        {
            if (_material != null)
            {
                _material.Dispose();
                _material = null;
            }
        }

        private bool HasInputs()
        {
            return _fromSea != null &&
                _toSea != null &&
                _fromWeather != null &&
                _toWeather != null &&
                _abyssProfile != null &&
                _abyssProfile.Type == WeatherType.Maelstrom;
        }

        private void Apply()
        {
            WeatherEnvironmentState fromWeather = _fromWeather.Appearance;
            WeatherEnvironmentState toWeather = _toWeather.Appearance;

            WeatherEnvironmentState weather = WeatherEnvironmentState.Lerp(
                in fromWeather, in toWeather, _weatherBlend);

            SkyEnvironmentState state = EnvironmentBlendModel.Evaluate(
                _fromSea.Day,
                _fromSea.Night,
                _toSea.Day,
                _toSea.Night,
                _regionBlend,
                _daylight,
                in weather,
                _abyssProfile.AbyssSky);

            float visibleDaylight = _daylight * (1f - weather.AbyssWeight);
            _material.Apply(in state, visibleDaylight);
        }
    }
}
