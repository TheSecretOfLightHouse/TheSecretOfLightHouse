using System;

namespace Lighthouse.World.WorldState.Server
{
    public static class MatchSeedSource
    {
        public static int Next()
        {
            return Guid.NewGuid().GetHashCode();
        }
    }
}
