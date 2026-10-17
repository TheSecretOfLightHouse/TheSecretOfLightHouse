using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Spawning.Server;

namespace Lighthouse.World.MapGeneration.Server
{
    public sealed class ServerMapContext
    {
        public MapLayout Layout { get; }
        public SpawnerRegistry Spawners { get; }

        public ServerMapContext(MapLayout layout, SpawnerRegistry spawners)
        {
            Layout = layout;
            Spawners = spawners;
        }
    }
}
