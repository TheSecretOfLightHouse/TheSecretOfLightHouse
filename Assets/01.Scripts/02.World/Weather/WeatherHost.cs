using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.Rules;
using UnityEngine;

namespace Lighthouse.World.Weather
{
    [DefaultExecutionOrder(-900)]
    public class WeatherHost : MonoBehaviour
    {
        [SerializeField] private WeatherCatalog _catalog;
        [SerializeField] private WeatherType _initialWeather = WeatherType.Calm;

        private WeatherSystem _system;
        private IClock _clock;

        private void Awake()
        {
            if (!WeatherStateSet.TryBuild(_catalog, out WeatherState[] states)
                || !WeatherSystem.TryCreate(states, _initialWeather, out _system))
            {
                Debug.LogError($"[{nameof(WeatherHost)}] WeatherCatalog is missing or inconsistent. Run Weather/Run All Tests.");
                enabled = false;
                return;
            }

            WorldServices.Register(_system);
            WorldServices.Register<IWeatherService>(_system);
        }

        private void Start()
        {
            if (!WorldServices.TryGet<IClock>(out _clock))
            {
                Debug.LogError($"[{nameof(WeatherHost)}] IClock is not registered. Add a GameClock to the scene.");
                enabled = false;
            }
        }

        private void Update()
        {
            _system.Tick(_clock.Now);
        }

        private void OnDestroy()
        {
            if (_system == null)
            {
                return;
            }

            WorldServices.Unregister(_system);
            WorldServices.Unregister<IWeatherService>(_system);
        }
    }
}
