namespace Lighthouse.World.Weather.Rules
{
    public interface IForecastService
    {
        bool TryGetOriginal(int day, out WeatherType type);
        bool Tamper(int day, WeatherType type);
    }
}
