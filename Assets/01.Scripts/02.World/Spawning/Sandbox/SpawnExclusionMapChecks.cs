using System.Collections.Generic;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Spawning.Rules;
using UnityEngine;

namespace Lighthouse.World.Spawning.Sandbox
{
    public static class SpawnExclusionMapChecks
    {
        private const float IslandRadius = 20f;
        private const float ReefRadius = 6f;
        private const float IslandMargin = 5f;
        private const float ReefMargin = 4f;
        private const float Epsilon = 0.1f;

        public static bool RunAll(List<string> failures)
        {
            IslandPlacement[] islands =
            {
                new IslandPlacement(0, 1, 0, SeaZone.Inner, new Vector2(100f, 0f), IslandRadius, 0f)
            };
            ReefPlacement[] reefs =
            {
                new ReefPlacement(new Vector2(0f, 200f), ReefRadius, SeaZone.Middle)
            };
            SpawnExclusionMap map = new SpawnExclusionMap(new MapLayout(1, islands, reefs), IslandMargin, ReefMargin);

            Expect(failures, map.Count == 2, "count is not 2");
            Expect(failures, map.IsBlocked(new Vector3(100f, 0f, 0f)), "island center is free");
            Expect(failures, map.IsBlocked(new Vector3(100f + IslandRadius + IslandMargin - Epsilon, 0f, 0f)), "island margin edge is free");
            Expect(failures, !map.IsBlocked(new Vector3(100f + IslandRadius + IslandMargin + Epsilon, 0f, 0f)), "outside island is blocked");
            Expect(failures, map.IsBlocked(new Vector3(0f, 0f, 200f)), "reef center is free");
            Expect(failures, map.IsBlocked(new Vector3(ReefRadius + ReefMargin - Epsilon, 0f, 200f)), "reef margin edge is free");
            Expect(failures, !map.IsBlocked(new Vector3(ReefRadius + ReefMargin + Epsilon, 0f, 200f)), "outside reef is blocked");
            Expect(failures, !map.IsBlocked(new Vector3(-300f, 0f, -300f)), "open sea is blocked");
            Expect(failures, map.IsBlocked(new Vector3(float.NaN, 0f, 0f)), "NaN is not blocked");

            return failures.Count == 0;
        }

        private static void Expect(List<string> failures, bool condition, string message)
        {
            if (!condition)
            {
                failures.Add(message);
            }
        }
    }
}
