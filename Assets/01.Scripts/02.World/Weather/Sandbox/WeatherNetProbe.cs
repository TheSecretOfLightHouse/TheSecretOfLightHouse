using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.Net;
using Lighthouse.World.Weather.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Weather.Sandbox
{
    public class WeatherNetProbe : MonoBehaviour
    {
        [SerializeField] private WeatherStateNet _weatherNet;
        [SerializeField] private WeatherType _next = WeatherType.Storm;
        [SerializeField, Min(0f)] private float _duration = 10f;
        [SerializeField] private float _logInterval = 2f;

        private float _nextLogTime;

        private void OnEnable()
        {
            if (_weatherNet == null)
            {
                Debug.LogError($"[{nameof(WeatherNetProbe)}] WeatherStateNet is not assigned.");
                return;
            }

            _weatherNet.TransitionApplied += HandleTransitionApplied;
        }

        private void OnDisable()
        {
            if (_weatherNet != null)
            {
                _weatherNet.TransitionApplied -= HandleTransitionApplied;
            }
        }

        private void Update()
        {
            if (_logInterval <= 0f || Time.unscaledTime < _nextLogTime)
            {
                return;
            }

            _nextLogTime = Time.unscaledTime + _logInterval;
            LogState();
        }

        [ContextMenu("Weather/Begin Transition (Server)")]
        private void BeginTransition()
        {
            if (_weatherNet == null || !NetworkServer.active)
            {
                Debug.LogError($"[{nameof(WeatherNetProbe)}] Run on the server or host with WeatherStateNet assigned.");
                return;
            }

            bool isBegun = _weatherNet.BeginTransition(_next, _duration);
            Debug.Log($"[{nameof(WeatherNetProbe)}] {Role()} Begin {_next} over {_duration}s netTime={NetworkTime.time:F2}: {(isBegun ? "written" : "rejected")}");
        }

        [ContextMenu("Weather/Log State")]
        private void LogState()
        {
            if (!WorldServices.TryGet(out WeatherSystem system))
            {
                return;
            }

            ref readonly WeatherSnapshot snapshot = ref system.Snapshot;

            Debug.Log($"[{nameof(WeatherNetProbe)}] {Role()} target={system.Target} transitioning={system.IsTransitioning} amp0={snapshot.Waves[0].Amplitude:F3} intensity={snapshot.StormIntensity:F3} netTime={NetworkTime.time:F2}");
        }

        private void HandleTransitionApplied(WeatherTransitionInfo info, bool isApplied)
        {
            Debug.Log($"[{nameof(WeatherNetProbe)}] {Role()} Apply {info.From} -> {info.To} start={info.StartTime:F2} duration={info.Duration:F1} netTime={NetworkTime.time:F2}: {(isApplied ? "applied" : "rejected")}");
        }

        private static string Role()
        {
            if (NetworkServer.active && NetworkClient.active)
            {
                return "Host";
            }

            if (NetworkServer.active)
            {
                return "Server";
            }

            return NetworkClient.active ? "Client" : "Offline";
        }
    }
}
