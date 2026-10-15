namespace Lighthouse.World.MapGeneration.Rules
{
    public static class MapLayoutLoader
    {
        public static MapChecksumResult Load(MapBuilder builder, MapGenerationConfig config, MapSeedInfo expected, out MapLayout layout, out uint contentHash)
        {
            contentHash = MapContentHasher.Compute(config);
            layout = builder.Build(expected.Seed, false).Layout;

            if (layout == null)
            {
                return MapChecksumResult.BuildFailed;
            }

            return expected.Verify(layout.Hash, contentHash);
        }
    }
}
