using System;
using System.Collections.Generic;
using Lighthouse.World.Debris.Rules;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Debris.Sandbox
{
    public static class ShoreChecks
    {
        private const float Step = 1f;
        private const float MaxDistance = 30f;
        private const float MinHeight = 1f;
        private const int MissCount = 3;
        private const float Tolerance = 0.001f;

        public static bool RunAll()
        {
            bool isPassed = RunSelector();
            isPassed &= RunLineSearch();
            isPassed &= RunSlotMath();
            return isPassed;
        }

        private static bool RunSelector()
        {
            var islands = new List<IslandPlacement>
            {
                Place(0, SeaZone.Inner, 0f, 0f),
                Place(1, SeaZone.Inner, 100f, 0f),
                Place(2, SeaZone.Middle, 50f, 50f),
                Place(3, SeaZone.Outer, 200f, 0f)
            };

            bool isPassed =
                Select(islands, new Vector3(90f, 0f, 0f), DebrisKind.Resource, SeaZone.Inner, null) == 1
                && Select(islands, new Vector3(60f, 0f, 40f), DebrisKind.Resource, SeaZone.Inner | SeaZone.Middle, null) == 2
                && Select(islands, new Vector3(190f, 0f, 0f), DebrisKind.Resource, SeaZone.Inner, null) == 1
                && Select(islands, new Vector3(190f, 0f, 0f), DebrisKind.Module, SeaZone.All, null) == 3
                && Select(islands, new Vector3(190f, 0f, 0f), DebrisKind.Module, SeaZone.Inner, null) == 1
                && Select(islands, new Vector3(-5f, 0f, 0f), DebrisKind.Module, SeaZone.Inner, null) == 0
                && Select(islands, new Vector3(90f, 0f, 0f), DebrisKind.Resource, SeaZone.Inner, island => island.Index != 1) == 0
                && Select(new List<IslandPlacement>(), Vector3.zero, DebrisKind.Resource, SeaZone.All, null) == -1;

            Report("Selector", isPassed, "nearest/locked/module nearest/filter/empty");
            return isPassed;
        }

        private static bool RunLineSearch()
        {
            var disc = new RadialGround(0f, 10f);
            var crescent = new RadialGround(6f, 10f);
            var gap = new RadialGround(0f, 5f, 7f, 10f);
            var empty = new RadialGround();

            bool isPassed =
                Near(Search(disc, MinHeight, MissCount), 10f)
                && Near(Search(crescent, MinHeight, MissCount), 10f)
                && Near(Search(gap, MinHeight, MissCount), 10f)
                && Near(Search(gap, MinHeight, 1), 5f)
                && Search(empty, MinHeight, MissCount) < 0f
                && Search(disc, 3f, MissCount) < 0f
                && !ShoreLineSearch.TryFindShore(null, 0, Vector2.zero, Vector2.right, MaxDistance, Step, MinHeight, MissCount, out _)
                && !ShoreLineSearch.TryFindShore(disc, 0, Vector2.zero, Vector2.zero, MaxDistance, Step, MinHeight, MissCount, out _);

            Report("LineSearch", isPassed, "disc/crescent/gap/empty/water/null");
            return isPassed;
        }

        private static bool RunSlotMath()
        {
            bool isPassed =
                Near(ShoreSlotMath.GetTangentOffset(0, 1f), 0f)
                && Near(ShoreSlotMath.GetTangentOffset(1, 1f), 1f)
                && Near(ShoreSlotMath.GetTangentOffset(2, 1f), -1f)
                && Near(ShoreSlotMath.GetTangentOffset(3, 1f), 2f)
                && Near(ShoreSlotMath.GetTangentOffset(4, 1f), -2f)
                && Near(ShoreSlotMath.GetTangentOffset(-3, 1f), 0f)
                && ShoreSlotMath.GetOutward(Vector2.zero, Vector2.zero) == Vector2.right
                && ShoreSlotMath.GetOutward(Vector2.zero, new Vector2(0f, 5f)) == Vector2.up
                && NearPoint(ShoreSlotMath.GetPoint(Vector2.zero, Vector2.right, 10f, 2f, 1, 0.8f), new Vector2(8f, 0.8f))
                && NearPoint(ShoreSlotMath.GetPoint(Vector2.zero, Vector2.right, 1f, 2f, 0, 0.8f), Vector2.zero);

            Report("SlotMath", isPassed, "alternate offsets/direction/point/inset clamp");
            return isPassed;
        }

        private static IslandPlacement Place(short index, SeaZone zone, float x, float z)
        {
            return new IslandPlacement(index, 0, 0, zone, new Vector2(x, z), 10f, 0f);
        }

        private static int Select(
            List<IslandPlacement> islands,
            Vector3 origin,
            DebrisKind kind,
            SeaZone unlocked,
            Predicate<IslandPlacement> filter)
        {
            return ShoreIslandSelector.TrySelect(islands, origin, kind, unlocked, filter, out IslandPlacement selected)
                ? selected.Index
                : -1;
        }

        private static float Search(IGroundHeightSource ground, float minHeight, int missCount)
        {
            return ShoreLineSearch.TryFindShore(ground, 0, Vector2.zero, Vector2.right, MaxDistance, Step, minHeight, missCount, out float distance)
                ? distance
                : -1f;
        }

        private static bool Near(float a, float b)
        {
            return Mathf.Abs(a - b) <= Tolerance;
        }

        private static bool NearPoint(Vector2 a, Vector2 b)
        {
            return (a - b).sqrMagnitude <= Tolerance * Tolerance;
        }

        private static void Report(string name, bool isPassed, string detail)
        {
            Debug.Log($"[{nameof(ShoreChecks)}] {name} {(isPassed ? "PASS" : "FAIL")} {detail}");
        }

        private sealed class RadialGround : IGroundHeightSource
        {
            private const float GroundHeight = 2f;

            private readonly float[] _ranges;

            public RadialGround(params float[] ranges)
            {
                _ranges = ranges;
            }

            public bool TryGetGroundHeight(short islandIndex, float x, float z, out float height)
            {
                float distance = Mathf.Sqrt(x * x + z * z);
                for (int i = 0; i + 1 < _ranges.Length; i += 2)
                {
                    if (distance >= _ranges[i] && distance <= _ranges[i + 1])
                    {
                        height = GroundHeight;
                        return true;
                    }
                }

                height = 0f;
                return false;
            }
        }
    }
}
