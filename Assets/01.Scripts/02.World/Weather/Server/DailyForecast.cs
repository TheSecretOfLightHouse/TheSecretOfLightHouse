using Lighthouse.World.Weather.Rules;

namespace Lighthouse.World.Weather.Server
{
    public sealed class DailyForecast : IForecast
    {
        private readonly int _day;
        private readonly WeatherType _type;

        public DailyForecast(int day, WeatherType type)
        {
            _day = day;
            _type = type;
        }

        public int Day => _day;

        public WeatherType GetWeather()
        {
            return _type;
        }
    }
}
