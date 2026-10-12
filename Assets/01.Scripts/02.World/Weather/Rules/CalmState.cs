namespace Lighthouse.World.Weather.Rules
{
    public sealed class CalmState : WeatherState
    {
        public CalmState(WeatherSnapshot target) : base(WeatherType.Calm, target)
        {
        }
    }
}
