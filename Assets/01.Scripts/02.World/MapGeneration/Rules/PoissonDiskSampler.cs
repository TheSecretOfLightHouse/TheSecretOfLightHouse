using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    public static class PoissonDiskSampler
    {
        public const int DefaultCandidates = 30;

        private const int StartAttempts = 1000;
        private const float ReachFactor = 2f;

        public static bool TrySample<TDomain>(
            SeededRandom random,
            TDomain domain,
            float minDistance,
            int maxPoints,
            List<Vector2> results,
            IReadOnlyList<Vector2> existing = null,
            int candidatesPerPoint = DefaultCandidates)
            where TDomain : struct, ISampleDomain
        {
            if (results == null)
            {
                return false;
            }

            results.Clear();
            Rect bounds = domain.Bounds;
            bool isValid = random != null
                && minDistance > 0f
                && maxPoints > 0
                && candidatesPerPoint > 0
                && bounds.width > 0f
                && bounds.height > 0f;
            if (!isValid)
            {
                return false;
            }

            List<Vector2> points = new List<Vector2>();
            PoissonGrid grid = new PoissonGrid(bounds, minDistance, points);
            int existingCount = RegisterExisting(existing, points, grid);

            if (TryFindStart(random, domain, grid, bounds, out Vector2 start))
            {
                Grow(random, domain, grid, points, start, minDistance, maxPoints, candidatesPerPoint);
            }

            for (int i = existingCount; i < points.Count; i++)
            {
                results.Add(points[i]);
            }

            return true;
        }

        private static int RegisterExisting(IReadOnlyList<Vector2> existing, List<Vector2> points, PoissonGrid grid)
        {
            if (existing != null)
            {
                for (int i = 0; i < existing.Count; i++)
                {
                    points.Add(existing[i]);
                    grid.Add(points.Count - 1);
                }
            }

            return points.Count;
        }

        private static bool TryFindStart<TDomain>(SeededRandom random, TDomain domain, PoissonGrid grid, Rect bounds, out Vector2 start)
            where TDomain : struct, ISampleDomain
        {
            for (int i = 0; i < StartAttempts; i++)
            {
                start = new Vector2(
                    random.NextFloat(bounds.xMin, bounds.xMax),
                    random.NextFloat(bounds.yMin, bounds.yMax));

                if (domain.Contains(start) && grid.IsFarEnough(start))
                {
                    return true;
                }
            }

            start = Vector2.zero;
            return false;
        }

        private static void Grow<TDomain>(
            SeededRandom random,
            TDomain domain,
            PoissonGrid grid,
            List<Vector2> points,
            Vector2 start,
            float minDistance,
            int maxPoints,
            int candidatesPerPoint)
            where TDomain : struct, ISampleDomain
        {
            List<int> active = new List<int>();
            AddPoint(start, points, grid, active);
            int placed = 1;

            while (active.Count > 0 && placed < maxPoints)
            {
                int slot = random.NextInt(0, active.Count);
                Vector2 origin = points[active[slot]];

                if (TryPlaceAround(random, domain, grid, origin, minDistance, candidatesPerPoint, out Vector2 candidate))
                {
                    AddPoint(candidate, points, grid, active);
                    placed++;
                }
                else
                {
                    int last = active.Count - 1;
                    active[slot] = active[last];
                    active.RemoveAt(last);
                }
            }
        }

        private static void AddPoint(Vector2 point, List<Vector2> points, PoissonGrid grid, List<int> active)
        {
            points.Add(point);
            grid.Add(points.Count - 1);
            active.Add(points.Count - 1);
        }

        private static bool TryPlaceAround<TDomain>(
            SeededRandom random,
            TDomain domain,
            PoissonGrid grid,
            Vector2 origin,
            float minDistance,
            int candidatesPerPoint,
            out Vector2 candidate)
            where TDomain : struct, ISampleDomain
        {
            for (int i = 0; i < candidatesPerPoint; i++)
            {
                candidate = SampleAround(random, origin, minDistance);
                if (domain.Contains(candidate) && grid.IsFarEnough(candidate))
                {
                    return true;
                }
            }

            candidate = Vector2.zero;
            return false;
        }

        private static Vector2 SampleAround(SeededRandom random, Vector2 origin, float minDistance)
        {
            float reach = minDistance * ReachFactor;
            float minSqr = minDistance * minDistance;
            float maxSqr = reach * reach;

            while (true)
            {
                float dx = random.NextFloat(-reach, reach);
                float dy = random.NextFloat(-reach, reach);
                float sqr = dx * dx + dy * dy;

                if (sqr >= minSqr && sqr <= maxSqr)
                {
                    return new Vector2(origin.x + dx, origin.y + dy);
                }
            }
        }
    }
}
