using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.Rules;
using UnityEngine;

namespace Lighthouse.World.Weather.Sandbox
{
    public class WeatherSandboxDriver : MonoBehaviour
    {
        private const double StartDelay = 0.2;

        [SerializeField] private WeatherType _next = WeatherType.Storm;
        [SerializeField, Min(0f)] private float _duration = 30f;

        [ContextMenu("Weather/Apply Next")]
        private void ApplyNext()
        {
            if (!WorldServices.TryGet(out WeatherSystem system) || !WorldServices.TryGet(out IClock clock))
            {
                Debug.LogError($"[{nameof(WeatherSandboxDriver)}] WeatherSystem and IClock are required. Enter Play mode with WeatherHost and GameClock.");
                return;
            }

            WeatherType from = system.Target;
            bool isApplied = system.Apply(from, _next, clock.Now + StartDelay, _duration);

            Debug.Log($"[{nameof(WeatherSandboxDriver)}] Apply {from} -> {_next} over {_duration}s: {(isApplied ? "applied" : "rejected")}");
        }

        [ContextMenu("Weather/Log State")]
        private void LogState()
        {
            if (!WorldServices.TryGet(out WeatherSystem system))
            {
                Debug.LogError($"[{nameof(WeatherSandboxDriver)}] WeatherSystem is not registered.");
                return;
            }

            ref readonly WeatherSnapshot snapshot = ref system.Snapshot;

            Debug.Log($"[{nameof(WeatherSandboxDriver)}] target={system.Target} transitioning={system.IsTransitioning} intensity={snapshot.StormIntensity:0.00} wind={snapshot.Wind} fog={snapshot.Fog:0.00} amp0={snapshot.Waves[0].Amplitude:0.00}");
        }
    }
}
