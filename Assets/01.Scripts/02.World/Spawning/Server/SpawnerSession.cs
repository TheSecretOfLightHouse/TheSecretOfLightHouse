using System;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.MapGeneration.Server;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Spawning.Rules;

namespace Lighthouse.World.Spawning.Server
{
    public sealed class SpawnerSession : IDisposable
    {
        private ServerMapContext _context;

        public bool IsStarted => _context != null;
        public int Count => _context != null ? _context.Spawners.Count : 0;

        public void Start(MapLayout layout, MapGenerationConfig mapConfig,
            SpawnerPlacementConfig spawningConfig)
        {
            if (IsStarted)
            {
                throw new InvalidOperationException("Spawner session already started.");
            }

            if (WorldServices.TryGet<ISpawnZoneProvider>(out _))
            {
                throw new InvalidOperationException("A spawn zone provider is already registered.");
            }

            ulong seed = SpawnerSeedSource.Next();
            SpawnerRegistry registry = SpawnerPlacementStep.Build(
                layout, mapConfig, spawningConfig, seed);
            registry.SetUnlockedZones(SeaZone.Inner);

            WorldServices.Register<ISpawnZoneProvider>(registry);
            if (!WorldServices.TryGet<ISpawnZoneProvider>(out ISpawnZoneProvider registered)
                || !ReferenceEquals(registered, registry))
            {
                throw new InvalidOperationException("Spawn zone provider registration failed.");
            }

            _context = new ServerMapContext(layout, registry);
        }

        public void Dispose()
        {
            if (_context == null)
            {
                return;
            }

            WorldServices.Unregister<ISpawnZoneProvider>(_context.Spawners);
            _context = null;
        }
    }
}
