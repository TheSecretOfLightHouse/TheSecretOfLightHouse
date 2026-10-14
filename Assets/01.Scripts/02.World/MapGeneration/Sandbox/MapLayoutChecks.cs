using System.Collections.Generic;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Sandbox
{
    public static class MapLayoutChecks
    {
        private const uint EmptyHash = 2166136261u;
        private const uint GoldenInts = 2034659765u;
        private const uint GoldenFloat = 1150899519u;
        private const int Seed = 12345;

        public static bool RunAll()
        {
            bool isAllPassed = CheckGolden();
            isAllPassed &= CheckSameInputSameHash();
            isAllPassed &= CheckPositionChangesHash();
            isAllPassed &= CheckOrderChangesHash();
            isAllPassed &= CheckInputIsCopied();
            return isAllPassed;
        }

        private static bool CheckGolden()
        {
            StableHash ints = new StableHash();
            ints.Add(1);
            ints.Add(2);
            ints.Add(3);
            StableHash single = new StableHash();
            single.Add(150f);

            bool isPassed = new StableHash().Value == EmptyHash && ints.Value == GoldenInts && single.Value == GoldenFloat;
            MapCheckLog.Report("StableHash golden", isPassed, $"ints={ints.Value}, float={single.Value}");
            return isPassed;
        }

        private static bool CheckSameInputSameHash()
        {
            MapLayout a = new MapLayout(Seed, CreateIslands(), CreateReefs());
            MapLayout b = new MapLayout(Seed, CreateIslands(), CreateReefs());
            bool isPassed = a.Hash == b.Hash;
            MapCheckLog.Report("Layout same input", isPassed, $"{a.Hash} vs {b.Hash}");
            return isPassed;
        }

        private static bool CheckPositionChangesHash()
        {
            List<IslandPlacement> moved = CreateIslands();
            IslandPlacement original = moved[1];
            moved[1] = new IslandPlacement(original.Index, original.ArchetypeId, original.VariantIndex, original.Zone,
                original.Position + new Vector2(0.02f, 0f), original.Radius, original.YawDegrees);

            uint before = new MapLayout(Seed, CreateIslands(), CreateReefs()).Hash;
            uint after = new MapLayout(Seed, moved, CreateReefs()).Hash;
            bool isPassed = before != after;
            MapCheckLog.Report("Layout 2cm move", isPassed, $"{before} vs {after}");
            return isPassed;
        }

        private static bool CheckOrderChangesHash()
        {
            List<IslandPlacement> swapped = CreateIslands();
            (swapped[1], swapped[2]) = (swapped[2], swapped[1]);

            uint before = new MapLayout(Seed, CreateIslands(), CreateReefs()).Hash;
            uint after = new MapLayout(Seed, swapped, CreateReefs()).Hash;
            bool isPassed = before != after;
            MapCheckLog.Report("Layout order", isPassed, $"{before} vs {after}");
            return isPassed;
        }

        private static bool CheckInputIsCopied()
        {
            List<IslandPlacement> islands = CreateIslands();
            MapLayout layout = new MapLayout(Seed, islands, CreateReefs());
            islands.Clear();

            bool isPassed = layout.Islands.Count == 3 && layout.Reefs.Count == 1;
            MapCheckLog.Report("Layout defensive copy", isPassed, $"islands={layout.Islands.Count}, reefs={layout.Reefs.Count}");
            return isPassed;
        }

        private static List<IslandPlacement> CreateIslands()
        {
            return new List<IslandPlacement>
            {
                new IslandPlacement(0, 1000, 0, SeaZone.None, Vector2.zero, 25f, 0f),
                new IslandPlacement(1, 101, 1, SeaZone.Inner, new Vector2(80f, 40f), 18f, 90f),
                new IslandPlacement(2, 105, 0, SeaZone.Outer, new Vector2(-350f, 120f), 22f, 270f)
            };
        }

        private static List<ReefPlacement> CreateReefs()
        {
            return new List<ReefPlacement>
            {
                new ReefPlacement(new Vector2(200f, -60f), 6f, SeaZone.Middle)
            };
        }
    }
}
