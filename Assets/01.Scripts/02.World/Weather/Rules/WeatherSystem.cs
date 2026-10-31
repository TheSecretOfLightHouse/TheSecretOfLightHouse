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
        private WeatherTransitionInfo _activeInfo;
        private WeatherTransitionInfo _pendingInfo;
        private bool _hasPending;
        private uint _generation;

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
        internal WeatherTransitionInfo CurrentTransition => _hasPending ? _pendingInfo : _activeInfo;

        public static bool TryCreate(WeatherState[] states, WeatherType initial, out WeatherSystem system)
        {
            system = null;

            if (states == null || (int)initial < 0 || (int)initial >= states.Length || states[(int)initial] == null)
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

            WeatherTransitionInfo previous = _hasPending ? _pendingInfo : _activeInfo;
            var info = new WeatherTransitionInfo
            {
                From = from,
                To = to,
                StartTime = startTime,
                Duration = Mathf.Max(0f, duration),
                IsSet = true,
                HasOrigin = true,
                Generation = _generation + 1,
                Origin = _hasApplied ? EvaluateAt(startTime) : WeatherTransitionOrigin.Capture(in _states[(int)from].Target),
                HasPrevious = previous.IsSet,
                PreviousTo = previous.To,
                PreviousStartTime = previous.StartTime,
                PreviousDuration = previous.Duration,
                PreviousOrigin = previous.Origin
            };
            return Apply(in info);
        }

        public WeatherTransitionOrigin EvaluateAt(double now)
        {
            WeatherTransitionInfo info = _hasPending && now >= _pendingInfo.StartTime
                ? _pendingInfo : _activeInfo;
            if (!info.IsSet)
            {
                return WeatherTransitionOrigin.Capture(in _initial.Target);
            }

            return Evaluate(in info, now);
        }

        public WeatherTransitionOrigin Evaluate(in WeatherTransitionInfo info, double now)
        {
            var transition = new WeatherTransition(_states[(int)info.From], _states[(int)info.To], info.StartTime, info.Duration);
            WeatherTransitionOrigin target = WeatherTransitionOrigin.Capture(in _states[(int)info.To].Target);
            return WeatherTransitionOrigin.Lerp(in info.Origin, in target, transition.Evaluate(now));
        }

        public bool Apply(in WeatherTransitionInfo info)
        {
            if (!info.IsSet || !info.HasOrigin || info.Generation <= _generation ||
                double.IsNaN(info.StartTime) || double.IsInfinity(info.StartTime) ||
                float.IsNaN(info.Duration) || float.IsInfinity(info.Duration) || info.Duration < 0f ||
                !CanApply(info.From, info.To))
            {
                return false;
            }

            if (info.HasPrevious)
            {
                _activeInfo = new WeatherTransitionInfo
                {
                    From = info.PreviousTo,
                    To = info.PreviousTo,
                    StartTime = info.PreviousStartTime,
                    Duration = info.PreviousDuration,
                    Origin = info.PreviousOrigin,
                    IsSet = true,
                    HasOrigin = true
                };
            }
            else if (_hasPending && info.StartTime > _pendingInfo.StartTime)
            {
                _activeInfo = _pendingInfo;
            }

            _pendingInfo = info;
            _hasPending = true;
            _target = _states[(int)info.To];
            _generation = info.Generation;
            _lastStartTime = info.StartTime;
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
            _activeInfo = default;
            _pendingInfo = default;
            _hasPending = false;
            _generation = 0;
            WeatherSnapshot.TryLerp(in _initial.Target, in _initial.Target, 0f, ref _snapshot);
            _listeners.Notify(in _snapshot);
        }

        public void Tick(double now)
        {
            if (!_isTransitioning)
            {
                return;
            }

            if (_hasPending && now >= _pendingInfo.StartTime)
            {
                _activeInfo = _pendingInfo;
                _hasPending = false;
                _transition = new WeatherTransition(
                    _states[(int)_activeInfo.From], _states[(int)_activeInfo.To],
                    _activeInfo.StartTime, _activeInfo.Duration);
            }

            WeatherTransitionOrigin value = EvaluateAt(now);
            value.Apply(ref _snapshot);

            if (!_hasPending && _transition.IsCompleteAt(now))
            {
                _isTransitioning = false;
            }

            _listeners.Notify(in _snapshot);
        }

        private bool TryGetState(WeatherType type, out WeatherState state)
        {
            int index = (int)type;
            state = index >= 0 && index < _states.Length ? _states[index] : null;
            return state != null;
        }
    }
}
