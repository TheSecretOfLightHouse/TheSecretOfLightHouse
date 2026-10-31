using System;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.Rules;
using Lighthouse.World.WorldState.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Weather.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WorldStateNet))]
    public class WeatherStateNet : NetworkBehaviour
    {
        [SerializeField, Min(0f)] private float _startLeadSeconds = 0.2f;

        [SyncVar(hook = nameof(OnTransitionChanged))]
        private WeatherTransitionInfo _transition;

        private WorldStateNet _worldState;
        private WeatherApplyBinder _binder;

        public event Action<WeatherTransitionInfo, bool> TransitionApplied;

        public bool IsLocked => _transition.IsSet && _transition.To == WeatherType.Maelstrom;

        private void Awake()
        {
            if (!TryGetComponent(out _worldState))
            {
                Debug.LogError($"[{nameof(WeatherStateNet)}] WorldStateNet is missing.");
                enabled = false;
                return;
            }

            _binder = new WeatherApplyBinder(IsClockReady);
            _binder.TransitionApplied += HandleTransitionApplied;
        }

        private void Update()
        {
            _binder.Tick();
        }

        public override void OnStartClient()
        {
            _binder.Submit(_transition);
        }

        public override void OnStopServer()
        {
            _transition = default;
            _binder.Reset();
        }

        public override void OnStopClient()
        {
            if (isServer)
            {
                return;
            }

            _binder.Reset();
        }

        [Server]
        public bool BeginTransition(WeatherType to, float duration)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration))
            {
                return false;
            }

            if (_binder == null || !WorldServices.TryGet(out WeatherSystem system))
            {
                Debug.LogError($"[{nameof(WeatherStateNet)}] WeatherSystem is not registered. Add a WeatherHost to the scene.");
                return false;
            }

            WeatherType from = _transition.IsSet ? _transition.To : system.Target;

            if (IsLocked)
            {
                return to == WeatherType.Maelstrom;
            }

            if (!system.CanApply(from, to))
            {
                Debug.LogWarning($"[{nameof(WeatherStateNet)}] Transition {from} -> {to} is not allowed.");
                return false;
            }

            double startTime = NetworkTime.time + Mathf.Max(0f, _startLeadSeconds);
            bool isReplacingScheduled = _transition.IsSet && _transition.StartTime > NetworkTime.time;
            if (isReplacingScheduled)
            {
                startTime = _transition.StartTime;
            }
            var info = new WeatherTransitionInfo
            {
                From = from,
                To = to,
                StartTime = startTime,
                Duration = Mathf.Max(0f, duration),
                IsSet = true,
                HasOrigin = true,
                Generation = _transition.Generation + 1,
                Origin = isReplacingScheduled ? _transition.Origin : _transition.IsSet
                    ? system.Evaluate(in _transition, startTime)
                    : system.EvaluateAt(startTime),
                HasPrevious = isReplacingScheduled ? _transition.HasPrevious : _transition.IsSet,
                PreviousTo = isReplacingScheduled ? _transition.PreviousTo : _transition.To,
                PreviousStartTime = isReplacingScheduled ? _transition.PreviousStartTime : _transition.StartTime,
                PreviousDuration = isReplacingScheduled ? _transition.PreviousDuration : _transition.Duration,
                PreviousOrigin = isReplacingScheduled ? _transition.PreviousOrigin : _transition.Origin
            };

            _transition = info;

            if (!NetworkServer.activeHost)
            {
                _binder.Submit(info);
            }

            return true;
        }

        private bool IsClockReady()
        {
            return _worldState.IsClockReady;
        }

        private void OnTransitionChanged(WeatherTransitionInfo oldValue, WeatherTransitionInfo newValue)
        {
            if (_binder != null)
            {
                _binder.Submit(newValue);
            }
        }

        private void HandleTransitionApplied(WeatherTransitionInfo info, bool isApplied)
        {
            TransitionApplied?.Invoke(info, isApplied);
        }
    }
}
