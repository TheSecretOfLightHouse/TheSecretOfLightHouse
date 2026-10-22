namespace Lighthouse.World.MapGeneration.Rules
{
    public interface ILocalMapBuilder
    {
        void Begin(MapGenerationConfig config, MapLayout layout, MapSeedInfo info);
        bool Step();
        void Cancel();
    }
}
