using System;
using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Weather.Net
{
    [DisallowMultipleComponent]
    public sealed class WeatherLightningNet : NetworkBehaviour
    {
        [SerializeField, Min(1f)] private float _eventRadius = 180f;
        private double _nextAt;
        private uint _sequence;
        private bool _isStopped;

        public event Action<WeatherLightningEvent> LightningOccurred;
        public event Action Stopped;

        private void Update()
        {
            if (!isServer || _isStopped || !WorldServices.TryGet<IWeatherService>(out var weather))
            {
                return;
            }
            float rate = weather.Snapshot.Lightning;
            if (rate <= 0f)
            {
                _nextAt = 0d;
                return;
            }
            double now = NetworkTime.time;
            double interval = 60d / Math.Max(0.1f, rate);
            if (_nextAt == 0d)
            {
                _nextAt = now + interval;
            }
            if (now < _nextAt)
            {
                return;
            }

            _nextAt = now + interval;
            uint id = ++_sequence;
            int seed = WorldServices.TryGet<MapStateNet>(out var map) ? map.Info.Seed : 1;
            float angle = Mathf.Repeat(id * 137.508f + seed, 360f) * Mathf.Deg2Rad;
            Vector3 position = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _eventRadius;
            if (WorldServices.TryGet<IWorldQuery>(out var query))
            {
                position.y = query.SampleWaterHeight(position);
            }
            RpcLightning(new WeatherLightningEvent
            {
                Id = id,
                Time = now,
                Position = position,
                Seed = seed,
                IsAbyss = weather.Target == WeatherType.Maelstrom
            });
        }

        public override void OnStopServer()
        {
            _nextAt = 0d;
            _sequence = 0;
            _isStopped = true;
        }

        public override void OnStartServer()
        {
            _isStopped = false;
        }

        [Server]
        public void StopEvents()
        {
            _isStopped = true;
            _nextAt = 0d;
            RpcStopped();
        }

        public override void OnStopClient()
        {
            Stopped?.Invoke();
        }

        [ClientRpc]
        private void RpcLightning(WeatherLightningEvent lightning)
        {
            LightningOccurred?.Invoke(lightning);
        }

        [ClientRpc]
        private void RpcStopped()
        {
            Stopped?.Invoke();
        }
    }
}
