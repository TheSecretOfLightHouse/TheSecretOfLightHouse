using Lighthouse.World.Weather.Rules;

namespace Lighthouse.World.Weather.Server
{
    public sealed class TamperedForecast : IForecast
    {
        private readonly IForecast _inner;
        private readonly WeatherType _overrideType;

        public TamperedForecast(IForecast inner, WeatherType overrideType)
        {
            _inner = inner;
            _overrideType = overrideType;
        }

        public int Day => _inner.Day;

        public WeatherType GetWeather()
        {
            return _overrideType;
        }
    }
}
