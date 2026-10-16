using System;

namespace Lighthouse.World.MapGeneration.Server
{
    public static class MapSeedSource
    {
        public static int Next()
        {
            return Guid.NewGuid().GetHashCode();
        }
    }
}
