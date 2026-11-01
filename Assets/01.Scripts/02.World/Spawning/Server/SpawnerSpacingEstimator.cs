using Lighthouse.World.MapGeneration.Rules;
using UnityEngine;

namespace Lighthouse.World.Spawning.Server
{
    public static class SpawnerSpacingEstimator
    {
        private const int SampleCount = 4000;

        public static float Estimate(SeededRandom random, SpawnerSampleDomain domain,
            int spawnerCount, float spreadFactor, float floor)
        {
            Rect bounds = domain.Bounds;
            if (random == null || spawnerCount <= 0
                || bounds.width <= 0f || bounds.height <= 0f)
            {
                return floor;
            }

            int hitCount = 0;
            for (int i = 0; i < SampleCount; i++)
            {
                var point = new Vector2(
                    random.NextFloat(bounds.xMin, bounds.xMax),
                    random.NextFloat(bounds.yMin, bounds.yMax));

                if (domain.Contains(point))
                {
                    hitCount++;
                }
            }

            float freeArea = bounds.width * bounds.height * hitCount / SampleCount;
            float spacing = spreadFactor * Mathf.Sqrt(freeArea / spawnerCount);
            return Mathf.Max(floor, spacing);
        }
    }
}
