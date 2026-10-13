using System.Collections.Generic;
using Lighthouse.World.MapGeneration.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Sandbox
{
    public static class PoissonDiskChecks
    {
        private const float DistanceTolerance = 0.0001f;
        private const float MinDensity = 0.5f;
        private const int UnboundedPointCount = 100000;
        private const int LimitedPointCount = 10;
        private const float CloseExistingGap = 0.1f;

        public static bool RunAll(int seed, RingDomain ring, float minDistance)
        {
            bool isAllPassed = CheckDeterminism(seed, ring, minDistance);

            isAllPassed &= CheckMinDistance(seed, ring, minDistance);
            isAllPassed &= CheckInsideDomain(seed, ring, minDistance);
            isAllPassed &= CheckDensity(seed, ring, minDistance);
            isAllPassed &= CheckExisting(seed, ring, minDistance);
            isAllPassed &= CheckDifferentSeed(seed, ring, minDistance);
            isAllPassed &= CheckMaxPoints(seed, ring, minDistance);
            isAllPassed &= CheckInvalidArguments(seed, ring);

            return isAllPassed;
        }

        private static bool CheckDeterminism(int seed, RingDomain ring, float minDistance)
        {
            List<Vector2> a = Generate(seed, ring, minDistance, UnboundedPointCount, null);
            List<Vector2> b = Generate(seed, ring, minDistance, UnboundedPointCount, null);
            int mismatches = a.Count == b.Count ? 0 : 1;

            for (int i = 0; i < a.Count && i < b.Count; i++)
            {
                if (a[i].x != b[i].x || a[i].y != b[i].y)
                {
                    mismatches++;
                }
            }

            bool isPassed = mismatches == 0 && a.Count > 0;
            MapCheckLog.Report("Determinism", isPassed, $"countA={a.Count}, countB={b.Count}, mismatches={mismatches}");
            return isPassed;
        }

        private static bool CheckMinDistance(int seed, RingDomain ring, float minDistance)
        {
            List<Vector2> points = Generate(seed, ring, minDistance, UnboundedPointCount, null);
            float closest = float.MaxValue;
            int violations = 0;

            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    float distance = Vector2.Distance(points[i], points[j]);
                    closest = Mathf.Min(closest, distance);
                    violations += distance < minDistance - DistanceTolerance ? 1 : 0;
                }
            }

            bool isPassed = violations == 0 && points.Count > 1;
            MapCheckLog.Report("MinDistance", isPassed, $"count={points.Count}, closest={closest:F3}, min={minDistance}, violations={violations}");
            return isPassed;
        }

        private static bool CheckInsideDomain(int seed, RingDomain ring, float minDistance)
        {
            List<Vector2> points = Generate(seed, ring, minDistance, UnboundedPointCount, null);
            int outside = 0;

            for (int i = 0; i < points.Count; i++)
            {
                outside += ring.Contains(points[i]) ? 0 : 1;
            }

            MapCheckLog.Report("InsideDomain", outside == 0, $"count={points.Count}, outside={outside}");
            return outside == 0;
        }

        private static bool CheckDensity(int seed, RingDomain ring, float minDistance)
        {
            List<Vector2> points = Generate(seed, ring, minDistance, UnboundedPointCount, null);
            float density = points.Count * minDistance * minDistance / ring.Area;

            bool isPassed = density >= MinDensity;
            MapCheckLog.Report("Density", isPassed, $"count={points.Count}, count*r^2/area={density:F3}, min={MinDensity}");
            return isPassed;
        }

        private static bool CheckExisting(int seed, RingDomain ring, float minDistance)
        {
            float middle = (ring.InnerRadius + ring.OuterRadius) * 0.5f;
            Vector2[] existing =
            {
                ring.Center + new Vector2(middle, 0f),
                ring.Center + new Vector2(middle + CloseExistingGap, 0f),
                ring.Center + new Vector2(0f, middle)
            };

            List<Vector2> points = Generate(seed, ring, minDistance, UnboundedPointCount, existing);
            int violations = 0;

            for (int i = 0; i < points.Count; i++)
            {
                for (int j = 0; j < existing.Length; j++)
                {
                    violations += Vector2.Distance(points[i], existing[j]) < minDistance - DistanceTolerance ? 1 : 0;
                }
            }

            bool isPassed = violations == 0 && points.Count > 0;
            MapCheckLog.Report("Existing", isPassed, $"existing={existing.Length}, count={points.Count}, violations={violations}");
            return isPassed;
        }

        private static bool CheckDifferentSeed(int seed, RingDomain ring, float minDistance)
        {
            List<Vector2> a = Generate(seed, ring, minDistance, UnboundedPointCount, null);
            List<Vector2> b = Generate(seed + 1, ring, minDistance, UnboundedPointCount, null);
            bool isDifferent = a.Count != b.Count || a.Count == 0 || a[0].x != b[0].x || a[0].y != b[0].y;

            MapCheckLog.Report("DifferentSeed", isDifferent, $"countA={a.Count}, countB={b.Count}");
            return isDifferent;
        }

        private static bool CheckMaxPoints(int seed, RingDomain ring, float minDistance)
        {
            List<Vector2> points = Generate(seed, ring, minDistance, LimitedPointCount, null);

            bool isPassed = points.Count == LimitedPointCount;
            MapCheckLog.Report("MaxPoints", isPassed, $"requested={LimitedPointCount}, count={points.Count}");
            return isPassed;
        }

        private static bool CheckInvalidArguments(int seed, RingDomain ring)
        {
            List<Vector2> results = new List<Vector2>();
            bool zeroDistance = PoissonDiskSampler.TrySample(new SeededRandom(seed), ring, 0f, LimitedPointCount, results);
            bool nullResults = PoissonDiskSampler.TrySample(new SeededRandom(seed), ring, 1f, LimitedPointCount, null);
            bool nullRandom = PoissonDiskSampler.TrySample(null, ring, 1f, LimitedPointCount, results);

            bool isPassed = !zeroDistance && !nullResults && !nullRandom;
            MapCheckLog.Report("InvalidArguments", isPassed, $"zeroDistance={zeroDistance}, nullResults={nullResults}, nullRandom={nullRandom}");
            return isPassed;
        }

        private static List<Vector2> Generate(int seed, RingDomain ring, float minDistance, int maxPoints, IReadOnlyList<Vector2> existing)
        {
            List<Vector2> results = new List<Vector2>();
            PoissonDiskSampler.TrySample(new SeededRandom(seed), ring, minDistance, maxPoints, results, existing);
            return results;
        }
    }
}
