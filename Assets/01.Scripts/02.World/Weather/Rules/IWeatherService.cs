namespace Lighthouse.World.Weather.Rules
{
    public interface IWeatherService
    {
        WeatherType Target { get; }
        bool IsTransitioning { get; }
        ref readonly WeatherSnapshot Snapshot { get; }

        bool AddListener(IWeatherListener listener);
        bool RemoveListener(IWeatherListener listener);
    }
}
