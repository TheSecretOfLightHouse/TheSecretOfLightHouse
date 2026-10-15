using System;
using Lighthouse.World.Weather.Rules;

namespace Lighthouse.World.Weather.Server
{
    public class WeatherScheduler : IForecastService
    {
        private readonly IForecast[] _original;
        private readonly IForecast[] _actual;
        private int _currentDay;

        public WeatherScheduler(IForecast[] original)
        {
            _original = original ?? Array.Empty<IForecast>();
            _actual = new IForecast[_original.Length];
            Array.Copy(_original, _actual, _original.Length);
        }

        public int DayCount => _original.Length;
        public int CurrentDay => _currentDay;

        public void NotifyDayStarted(int day)
        {
            if (day > _currentDay)
            {
                _currentDay = day;
            }
        }

        public bool TryGetOriginal(int day, out WeatherType type)
        {
            return TryRead(_original, day, out type);
        }

        public bool TryGetActual(int day, out WeatherType type)
        {
            return TryRead(_actual, day, out type);
        }

        public bool Tamper(int day, WeatherType type)
        {
            if (!IsInRange(day) || day <= _currentDay || !IsTamperable(type))
            {
                return false;
            }

            _actual[day - 1] = new TamperedForecast(_original[day - 1], type);
            return true;
        }

        private bool IsInRange(int day)
        {
            return day >= 1 && day <= _original.Length;
        }

        private bool TryRead(IForecast[] source, int day, out WeatherType type)
        {
            type = WeatherType.Calm;

            if (!IsInRange(day))
            {
                return false;
            }

            type = source[day - 1].GetWeather();
            return true;
        }

        private static bool IsTamperable(WeatherType type)
        {
            return type != WeatherType.Maelstrom && (byte)type < (byte)WeatherType.Maelstrom;
        }
    }
}
