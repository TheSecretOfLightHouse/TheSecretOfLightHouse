namespace Lighthouse.World.Weather.Rules
{
    // The only way out is the calm after a citizens' victory. The server keeps the schedule locked afterwards, so
    // nothing else can pull the weather back out of this state.
    public sealed class MaelstromState : WeatherState
    {
        public MaelstromState(WeatherSnapshot target) : base(WeatherType.Maelstrom, target)
        {
        }

        public override bool CanTransitionTo(WeatherType next)
        {
            return next == WeatherType.Maelstrom || next == WeatherType.Calm;
        }
    }
}
