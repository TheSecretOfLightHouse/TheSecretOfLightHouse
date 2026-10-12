namespace Lighthouse.World.Weather.Rules
{
    public sealed class RainState : WeatherState
    {
        public RainState(WeatherSnapshot target) : base(WeatherType.Rain, target)
        {
        }
    }
}
