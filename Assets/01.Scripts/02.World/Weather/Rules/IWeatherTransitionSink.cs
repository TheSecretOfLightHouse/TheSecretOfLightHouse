namespace Lighthouse.World.Weather.Rules
{
    public interface IWeatherTransitionSink
    {
        bool IsLocked { get; }

        bool BeginTransition(WeatherType to, float duration);
    }
}
