using Lighthouse.World.Weather.Rules;

namespace Lighthouse.World.Weather.Server
{
    public interface IForecast
    {
        int Day { get; }

        WeatherType GetWeather();
    }
}
