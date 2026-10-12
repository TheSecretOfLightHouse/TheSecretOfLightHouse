namespace Lighthouse.World.Weather.Rules
{
    public sealed class StormState : WeatherState
    {
        public StormState(WeatherSnapshot target) : base(WeatherType.Storm, target)
        {
        }
    }
}
