namespace Lighthouse.World.Weather.Rules
{
    public sealed class OvercastState : WeatherState
    {
        public OvercastState(WeatherSnapshot target) : base(WeatherType.Overcast, target)
        {
        }
    }
}
