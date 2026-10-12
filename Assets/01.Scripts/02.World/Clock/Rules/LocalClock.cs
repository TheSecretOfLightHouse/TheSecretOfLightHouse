using UnityEngine;

namespace Lighthouse.World.Clock.Rules
{
    public sealed class LocalClock : IClock
    {
        public double Now => Time.timeAsDouble;
    }
}
