using System.Collections.Generic;
using Lighthouse.World.Debris.Rules;
using Lighthouse.World.Debris.Server;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules.Mocks;
using UnityEngine;

namespace Lighthouse.World.Debris.Sandbox
{
    public static class DebrisLocatorChecks
    {
        private const float DiscRadius = 10.5f;
        private const float ExpectedShoreDistance = 10f;
        private const float DiscHeight = 2f;
        private const float Tolerance = 0.01f;
        private const float MinSlotGap = 0.5f;
        private const float MaxSlotGap = 1f;

        public static bool RunAll()
        {
            DebrisConfig config = ScriptableObject.CreateInstance<DebrisConfig>();
            var islands = new List<IslandPlacement> { Place(0, 0f, 0f), Place(1, 100f, 0f) };
            var world = new FlatWorldQuery();
            var regions = new OpenRegionGate();
            var disc = new DiscGround(Vector2.zero, DiscRadius, DiscHeight);
            var locator = new ShoreDropLocator(islands, disc, world, regions, config);

            ShoreDropResult first = locator.GetDropPoint(
                new ShoreDropRequest(new Vector3(30f, 0f, 0f), DebrisKind.Resource, 0));
            ShoreDropResult second = locator.GetDropPoint(
                new ShoreDropRequest(new Vector3(30f, 0f, 0f), DebrisKind.Resource, 1));
            ShoreDropResult module = locator.GetDropPoint(
                new ShoreDropRequest(new Vector3(95f, 0f, 0f), DebrisKind.Module, 0));

            var noIslands = new ShoreDropLocator(new List<IslandPlacement>(), disc, world, regions, config);
            var noGround = new ShoreDropLocator(islands, new DiscGround(Vector2.zero, -1f, 0f), world, regions, config);
            var request = new ShoreDropRequest(new Vector3(30f, 0f, 0f), DebrisKind.Resource, 0);

            float gap = (first.Position - second.Position).magnitude;
            float expectedDistance = ExpectedShoreDistance - config.ShoreInset;

            bool isPassed =
                first.IsSuccess && first.IslandIndex == 0
                && Mathf.Abs(first.Position.x - expectedDistance) <= Tolerance
                && Mathf.Abs(first.Position.y - DiscHeight) <= Tolerance
                && second.IsSuccess && gap > MinSlotGap && gap < MaxSlotGap
                && module.IsSuccess && module.IslandIndex == 0
                && noIslands.GetDropPoint(request).Failure == ShoreFailure.NoCandidateIsland
                && noGround.GetDropPoint(request).Failure == ShoreFailure.ShoreNotFound;

            Object.DestroyImmediate(config);

            Debug.Log($"[{nameof(DebrisLocatorChecks)}] Locator {(isPassed ? "PASS" : "FAIL")} resource/slot/module/no island/no ground");
            return isPassed;
        }

        private static IslandPlacement Place(short index, float x, float z)
        {
            return new IslandPlacement(index, 0, 0, SeaZone.Inner, new Vector2(x, z), 20f, 0f);
        }

        private sealed class OpenRegionGate : IRegionGate
        {
            public SeaZone UnlockedZones => SeaZone.All;

            public int GetUnlockDay(SeaZone zone)
            {
                return 1;
            }
        }
    }
}
