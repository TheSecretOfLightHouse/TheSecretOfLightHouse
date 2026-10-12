namespace Lighthouse.World.Weather.Rules
{
    public sealed class MaelstromState : WeatherState
    {
        public MaelstromState(WeatherSnapshot target) : base(WeatherType.Maelstrom, target)
        {
        }

        public override bool CanTransitionTo(WeatherType next)
        {
            return next == WeatherType.Maelstrom;
        }
    }
}
