using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.Net;
using Lighthouse.World.Weather.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Weather.Sandbox
{
    public class WeatherForecastProbe : MonoBehaviour
    {
        [SerializeField] private WeatherScheduleDriver _driver;
        [SerializeField] private WeatherType _tamperType = WeatherType.Storm;

        [ContextMenu("Forecast/Log (Server)")]
        private void LogForecast()
        {
            if (!TryGetServerContext(out IForecastService service, out IGameClock clock))
            {
                return;
            }

            Debug.Log($"[{nameof(WeatherForecastProbe)}] currentDay={clock.DayIndex} totalDays={clock.TotalDays}");

            for (int day = 1; day <= clock.TotalDays; day++)
            {
                string original = service.TryGetOriginal(day, out WeatherType originalType) ? originalType.ToString() : "-";
                string actual = _driver.TryGetActual(day, out WeatherType actualType) ? actualType.ToString() : "-";
                Debug.Log($"[{nameof(WeatherForecastProbe)}] day {day}: original={original} actual={actual}");
            }
        }

        [ContextMenu("Forecast/Tamper Next Day (Server)")]
        private void TamperNextDay()
        {
            if (!TryGetServerContext(out IForecastService service, out IGameClock clock))
            {
                return;
            }

            int day = clock.DayIndex + 1;
            bool isTampered = service.Tamper(day, _tamperType);
            Debug.Log($"[{nameof(WeatherForecastProbe)}] Tamper day {day} -> {_tamperType}: {(isTampered ? "accepted" : "rejected")}");
        }

        private bool TryGetServerContext(out IForecastService service, out IGameClock clock)
        {
            service = null;
            clock = null;

            if (_driver == null || !NetworkServer.active
                || !WorldServices.TryGet(out service) || !WorldServices.TryGet(out clock))
            {
                Debug.LogError($"[{nameof(WeatherForecastProbe)}] Run on the server or host after the match has started, with the driver assigned.");
                return false;
            }

            return true;
        }
    }
}
