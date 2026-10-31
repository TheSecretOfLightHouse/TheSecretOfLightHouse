using System.Collections.Generic;
using Lighthouse.World.MapGeneration.Rules;
using UnityEngine;

namespace Lighthouse.World.Spawning.Server
{
    public static class SpawnerRandomPlacer
    {
        private const int AttemptsPerStep = 200;
        private const float RelaxFactor = 0.9f;

        public static bool TryPlace(SeededRandom random, SpawnerSampleDomain domain,
            float spacing, float floor, int target,
            List<Vector2> results, IReadOnlyList<Vector2> existing)
        {
            if (results == null)
            {
                return false;
            }

            results.Clear();
            Rect bounds = domain.Bounds;
            if (random == null || target <= 0 || spacing <= 0f || floor <= 0f
                || bounds.width <= 0f || bounds.height <= 0f)
            {
                return false;
            }

            float current = Mathf.Max(spacing, floor);
            while (results.Count < target)
            {
                if (TryFindPoint(random, domain, bounds, current, results, existing,
                    out Vector2 point))
                {
                    results.Add(point);
                    continue;
                }

                if (current <= floor)
                {
                    return false;
                }

                current = Mathf.Max(floor, current * RelaxFactor);
            }

            return true;
        }

        private static bool TryFindPoint(SeededRandom random, SpawnerSampleDomain domain,
            Rect bounds, float spacing, List<Vector2> results,
            IReadOnlyList<Vector2> existing, out Vector2 point)
        {
            float minSqr = spacing * spacing;
            for (int i = 0; i < AttemptsPerStep; i++)
            {
                point = new Vector2(
                    random.NextFloat(bounds.xMin, bounds.xMax),
                    random.NextFloat(bounds.yMin, bounds.yMax));

                if (domain.Contains(point)
                    && IsFarEnough(point, results, minSqr)
                    && IsFarEnough(point, existing, minSqr))
                {
                    return true;
                }
            }

            point = Vector2.zero;
            return false;
        }

        private static bool IsFarEnough(Vector2 point, IReadOnlyList<Vector2> others,
            float minSqr)
        {
            if (others == null)
            {
                return true;
            }

            for (int i = 0; i < others.Count; i++)
            {
                if ((point - others[i]).sqrMagnitude < minSqr)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
