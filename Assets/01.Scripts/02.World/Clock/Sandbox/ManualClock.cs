using Lighthouse.World.Clock.Rules;

namespace Lighthouse.World.Clock.Sandbox
{
    public sealed class ManualClock : IClock
    {
        public double Now { get; set; }
    }
}
