using Lighthouse.World.Clock.Rules;
using Mirror;

namespace Lighthouse.World.WorldState.Net
{
    public sealed class NetworkClock : IClock
    {
        public double Now => NetworkTime.time;

        public bool IsReady => NetworkServer.active || NetworkTime.time > 0.0;
    }
}
