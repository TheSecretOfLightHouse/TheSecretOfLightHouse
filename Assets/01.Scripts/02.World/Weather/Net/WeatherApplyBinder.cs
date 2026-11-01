using System;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.Rules;
using UnityEngine;

namespace Lighthouse.World.Weather.Net
{
    public sealed class WeatherApplyBinder
    {
        private readonly Func<bool> _isReady;
        private WeatherTransitionInfo _pending;
        private WeatherTransitionInfo _applied;
        private bool _hasPending;
        private bool _isMissingLogged;

        public WeatherApplyBinder(Func<bool> isReady)
        {
            _isReady = isReady;
        }

        public event Action<WeatherTransitionInfo, bool> TransitionApplied;

        public void Submit(WeatherTransitionInfo info)
        {
            if (!info.IsSet || info.Generation != 0 &&
                (info.Generation <= _applied.Generation || _hasPending && info.Generation <= _pending.Generation) ||
                info.Generation == 0 && info.Equals(_applied))
            {
                return;
            }

            _pending = info;
            _hasPending = true;
            Tick();
        }

        public void Tick()
        {
            if (!_hasPending || !_isReady())
            {
                return;
            }

            if (!WorldServices.TryGet(out WeatherSystem system))
            {
                if (!_isMissingLogged)
                {
                    _isMissingLogged = true;
                    Debug.LogError($"[{nameof(WeatherApplyBinder)}] WeatherSystem is not registered. Add a WeatherHost to the scene.");
                }

                return;
            }

            _hasPending = false;
            _applied = _pending;

            bool isApplied = _applied.HasOrigin
                ? system.Apply(in _applied)
                : system.Apply(_applied.From, _applied.To, _applied.StartTime, _applied.Duration);
            TransitionApplied?.Invoke(_applied, isApplied);
        }

        public void Reset()
        {
            _pending = default;
            _applied = default;
            _hasPending = false;
            _isMissingLogged = false;

            if (WorldServices.TryGet(out WeatherSystem system))
            {
                system.Reset();
            }
        }
    }
}
