using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.Rules;
using Lighthouse.World.Weather.Server;
using Lighthouse.World.WorldState.Net;
using Lighthouse.World.WorldState.Server;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Weather.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeatherStateNet))]
    public class WeatherScheduleDriver : NetworkBehaviour, IWeatherTransitionSink
    {
        [SerializeField] private WeatherScheduleProfile _profile;
        [SerializeField] private DayCycleConfig _dayConfig;
        [SerializeField] private ulong _debugSeed;

        private WeatherStateNet _weatherNet;
        private WorldStateNet _worldState;
        private WeatherScheduleRunner _runner;

        public bool IsLocked => _weatherNet != null && _weatherNet.IsLocked ||
            WorldServices.TryGet(out WeatherSystem system) && system.Target == WeatherType.Maelstrom;

        private void Awake()
        {
            if (!TryGetComponent(out _weatherNet) || !TryGetComponent(out _worldState))
            {
                Debug.LogError($"[{nameof(WeatherScheduleDriver)}] WeatherStateNet and WorldStateNet are required.");
                return;
            }

            if (_profile == null || _dayConfig == null)
            {
                Debug.LogError($"[{nameof(WeatherScheduleDriver)}] Profile or DayCycleConfig is not assigned.");
                _weatherNet = null;
            }
        }

        public override void OnStartServer()
        {
            if (_weatherNet == null || !WorldServices.TryGet(out IGameClock clock))
            {
                return;
            }

            _runner = new WeatherScheduleRunner(clock, _profile, _dayConfig.DaySeconds, this, NextSeed);
            WorldServices.Register<IForecastService>(_runner);

            if (_worldState.IsMatchStarted)
            {
                _runner.Process(clock.DayIndex);
            }
        }

        public override void OnStopServer()
        {
            if (_runner == null)
            {
                return;
            }

            WorldServices.Unregister<IForecastService>(_runner);
            _runner.Dispose();
            _runner = null;
        }

        public bool BeginTransition(WeatherType to, float duration)
        {
            return _weatherNet != null && _weatherNet.BeginTransition(to, duration);
        }

        public bool TryGetActual(int day, out WeatherType type)
        {
            type = WeatherType.Calm;
            return _runner != null && _runner.TryGetActual(day, out type);
        }

        private ulong NextSeed()
        {
            return _debugSeed != 0UL ? _debugSeed : ForecastSeedSource.Next();
        }
    }
}
