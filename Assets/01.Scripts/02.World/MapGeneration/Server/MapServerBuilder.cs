using Lighthouse.World.MapGeneration.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Server
{
    public static class MapServerBuilder
    {
        public static bool TryBuild(MapBuilder builder, MapGenerationConfig config, int baseSeed, out MapLayout layout, out MapSeedInfo info)
        {
            MapBuildResult result = builder.Build(baseSeed, true);
            layout = result.Layout;
            info = default;

            if (!result.IsValid)
            {
                for (int i = 0; i < result.Issues.Count; i++)
                {
                    Debug.LogError($"[{nameof(MapServerBuilder)}] {result.Issues[i]}");
                }

                return false;
            }

            info = MapSeedInfo.From(result, MapContentHasher.Compute(config));
            return true;
        }
    }
}
