using System;
using Lighthouse.World.MapGeneration.Rules;

namespace Lighthouse.World.Query.Rules
{
    public sealed class WorldQuerySession : IDisposable
    {
        private readonly IWorldQuery _world;
        private readonly IRegionGate _regions;

        public WorldQuerySession(IWorldQuery world, IRegionGate regions)
        {
            if (world == null || regions == null)
            {
                throw new ArgumentException("World query services are required.");
            }

            _world = world;
            _regions = regions;
        }

        public void Register()
        {
            if (WorldServices.TryGet<IWorldQuery>(out _)
                || WorldServices.TryGet<IRegionGate>(out _))
            {
                throw new InvalidOperationException("World query services are already registered.");
            }

            WorldServices.Register<IWorldQuery>(_world);
            WorldServices.Register<IRegionGate>(_regions);

            bool hasWorld = WorldServices.TryGet<IWorldQuery>(out IWorldQuery world)
                && ReferenceEquals(world, _world);
            bool hasRegions = WorldServices.TryGet<IRegionGate>(out IRegionGate regions)
                && ReferenceEquals(regions, _regions);

            if (!hasWorld || !hasRegions)
            {
                Dispose();
                throw new InvalidOperationException("World query registration failed.");
            }
        }

        public void Dispose()
        {
            WorldServices.Unregister<IWorldQuery>(_world);
            WorldServices.Unregister<IRegionGate>(_regions);
        }
    }
}
