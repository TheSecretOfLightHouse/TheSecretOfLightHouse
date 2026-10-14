using UnityEngine;

namespace Lighthouse.World.Weather.Rules
{
    public class WeatherSystem : IWeatherService
    {
        private readonly WeatherState[] _states;
        private readonly WeatherListenerHub _listeners = new WeatherListenerHub();
        private WeatherSnapshot _snapshot;
        private WeatherTransition _transition;
        private WeatherState _target;
        private readonly WeatherState _initial;
        private double _lastStartTime;
        private bool _hasApplied;
        private bool _isTransitioning;

        private WeatherSystem(WeatherState[] states, WeatherState initial)
        {
            _states = states;
            _initial = initial;
            _target = initial;
            _snapshot = WeatherSnapshot.CreateBuffer(initial.Target.Waves.Length);
            WeatherSnapshot.TryLerp(in initial.Target, in initial.Target, 0f, ref _snapshot);
        }

        public WeatherType Target => _target.Type;

        public bool IsTransitioning => _isTransitioning;

        public ref readonly WeatherSnapshot Snapshot => ref _snapshot;

        public static bool TryCreate(WeatherState[] states, WeatherType initial, out WeatherSystem system)
        {
            system = null;

            if (states == null || (int)initial >= states.Length || states[(int)initial] == null)
            {
                return false;
            }

            int waveCount = states[(int)initial].Target.Waves.Length;

            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] != null && ((int)states[i].Type != i || states[i].Target.Waves.Length != waveCount))
                {
                    return false;
                }
            }

            system = new WeatherSystem(states, states[(int)initial]);
            return true;
        }

        public bool AddListener(IWeatherListener listener)
        {
            return _listeners.Add(listener);
        }

        public bool RemoveListener(IWeatherListener listener)
        {
            return _listeners.Remove(listener);
        }

        public bool CanApply(WeatherType from, WeatherType to)
        {
            return TryGetState(from, out WeatherState fromState)
                && TryGetState(to, out _)
                && fromState.CanTransitionTo(to)
                && _target.CanTransitionTo(to);
        }

        public bool Apply(WeatherType from, WeatherType to, double startTime, float duration)
        {
            if (_hasApplied && startTime == _lastStartTime)
            {
                return false;
            }

            if (!CanApply(from, to))
            {
                return false;
            }

            WeatherState fromState = _states[(int)from];
            WeatherState toState = _states[(int)to];

            _transition = new WeatherTransition(fromState, toState, startTime, Mathf.Max(0f, duration));
            _target = toState;
            _lastStartTime = startTime;
            _hasApplied = true;
            _isTransitioning = true;
            return true;
        }

        public void Reset()
        {
            _target = _initial;
            _transition = default;
            _lastStartTime = 0.0;
            _hasApplied = false;
            _isTransitioning = false;
            WeatherSnapshot.TryLerp(in _initial.Target, in _initial.Target, 0f, ref _snapshot);
            _listeners.Notify(in _snapshot);
        }

        public void Tick(double now)
        {
            if (!_isTransitioning)
            {
                return;
            }

            float t = _transition.Evaluate(now);

            if (!WeatherSnapshot.TryLerp(in _transition.From.Target, in _transition.To.Target, t, ref _snapshot))
            {
                _isTransitioning = false;
                return;
            }

            if (_transition.IsCompleteAt(now))
            {
                _isTransitioning = false;
            }

            _listeners.Notify(in _snapshot);
        }

        private bool TryGetState(WeatherType type, out WeatherState state)
        {
            int index = (int)type;
            state = index < _states.Length ? _states[index] : null;
            return state != null;
        }
    }
}
