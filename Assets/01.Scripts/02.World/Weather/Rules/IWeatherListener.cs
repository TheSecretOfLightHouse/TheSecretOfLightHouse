namespace Lighthouse.World.Weather.Rules
{
    public interface IWeatherListener
    {
        void HandleWeatherChanged(in WeatherSnapshot snapshot);
    }
}
