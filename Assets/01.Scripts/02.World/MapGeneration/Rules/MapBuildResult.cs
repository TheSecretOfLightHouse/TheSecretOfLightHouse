using System.Collections.Generic;

namespace Lighthouse.World.MapGeneration.Rules
{
    public sealed class MapBuildResult
    {
        public MapBuildResult(MapLayout layout, int seed, int attempts, IReadOnlyList<string> issues)
        {
            Layout = layout;
            Seed = seed;
            Attempts = attempts;
            Issues = issues;
        }

        public MapLayout Layout { get; }

        public int Seed { get; }

        public int Attempts { get; }

        public IReadOnlyList<string> Issues { get; }

        public bool IsValid => Layout != null && Issues.Count == 0;
    }
}
