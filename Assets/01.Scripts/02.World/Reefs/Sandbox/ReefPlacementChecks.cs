using System.Collections.Generic;
using Lighthouse.World.Reefs.Rules;

namespace Lighthouse.World.Reefs.Sandbox
{
    public static class ReefPlacementChecks
    {
        private const int Seed = 12345;
        private const int OtherSeed = 54321;
        private const int PrefabCount = 4;
        private const int ReefCount = 200;
        private const float FullTurnDegrees = 360f;

        public static bool RunAll(List<string> failures)
        {
            CheckSameSeed(failures);
            CheckRange(failures);
            CheckDifferentSeed(failures);
            CheckSpread(failures);
            CheckInvalidCount(failures);
            return failures.Count == 0;
        }

        private static void CheckSameSeed(List<string> failures)
        {
            for (int i = 0; i < ReefCount; i++)
            {
                ReefVariant a = ReefPlacementMath.Pick(Seed, i, PrefabCount);
                ReefVariant b = ReefPlacementMath.Pick(Seed, i, PrefabCount);
                if (a.PrefabIndex != b.PrefabIndex || a.YawDegrees != b.YawDegrees)
                {
                    failures.Add($"same seed differs at reef {i}");
                    return;
                }
            }
        }

        private static void CheckRange(List<string> failures)
        {
            for (int i = 0; i < ReefCount; i++)
            {
                ReefVariant v = ReefPlacementMath.Pick(Seed, i, PrefabCount);
                if (v.PrefabIndex < 0 || v.PrefabIndex >= PrefabCount
                    || v.YawDegrees < 0f || v.YawDegrees >= FullTurnDegrees)
                {
                    failures.Add($"out of range at reef {i}");
                    return;
                }
            }
        }

        private static void CheckDifferentSeed(List<string> failures)
        {
            for (int i = 0; i < ReefCount; i++)
            {
                ReefVariant a = ReefPlacementMath.Pick(Seed, i, PrefabCount);
                ReefVariant b = ReefPlacementMath.Pick(OtherSeed, i, PrefabCount);
                if (a.PrefabIndex != b.PrefabIndex || a.YawDegrees != b.YawDegrees)
                {
                    return;
                }
            }

            failures.Add("different seeds gave identical picks");
        }

        private static void CheckSpread(List<string> failures)
        {
            bool[] seen = new bool[PrefabCount];
            for (int i = 0; i < ReefCount; i++)
            {
                seen[ReefPlacementMath.Pick(Seed, i, PrefabCount).PrefabIndex] = true;
            }

            for (int i = 0; i < PrefabCount; i++)
            {
                if (!seen[i])
                {
                    failures.Add($"prefab {i} never picked in {ReefCount} reefs");
                }
            }
        }

        private static void CheckInvalidCount(List<string> failures)
        {
            try
            {
                ReefPlacementMath.Pick(Seed, 0, 0);
                failures.Add("zero prefab count did not throw");
            }
            catch (System.ArgumentOutOfRangeException)
            {
            }
        }
    }
}
