using System.Collections.Generic;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public static class BuoyancyChecks
    {
        private const float Mass = 10f;
        private const float Gravity = 9.81f;
        private const float StepSeconds = 0.01f;
        private const float SettleSeconds = 40f;
        private const float FractionTolerance = 0.02f;
        private const float DepthTolerance = 0.05f;
        private const float SinkSeconds = 5f;
        private const float SinkLimit = -2f;
        private const float ForceEpsilon = 0.0001f;
        private const float FollowSpread = 0.2f;

        private static readonly Vector3Int Divisions = new Vector3Int(2, 2, 2);
        private static readonly Bounds UnitBounds = new Bounds(Vector3.zero, Vector3.one);

        public static bool RunAll()
        {
            bool isPassed = true;

            isPassed &= CheckFloatFraction("Half", 2f, 0.5f);
            isPassed &= CheckFloatFraction("Fin", 1f / 0.9f, 0.9f);
            isPassed &= CheckFloatFraction("Floating", 5f, 0.2f);
            isPassed &= CheckSubmerged();
            isPassed &= CheckSink();
            isPassed &= CheckFollow();
            isPassed &= CheckInvalid();
            isPassed &= CheckGrid();
            isPassed &= BuoyancyHeaveChecks.RunAll();

            return isPassed;
        }

        private static float Settle(
            BuoyancyProfile profile,
            float targetDepth,
            float startY,
            float seconds)
        {
            var offsets = new List<Vector3>();
            BuoyancyPointGrid.BuildOffsets(UnitBounds, Divisions, offsets);

            float thickness = profile.ResolveThickness(
                BuoyancyPointGrid.GetCellHeight(UnitBounds, Divisions));

            int count = offsets.Count;
            var world = new Vector3[count];
            var heights = new float[count];
            var velocities = new Vector3[count];
            var forces = new Vector3[count];

            float y = startY;
            float speed = 0f;
            int steps = Mathf.RoundToInt(seconds / StepSeconds);

            for (int step = 0; step < steps; step++)
            {
                for (int i = 0; i < count; i++)
                {
                    world[i] = new Vector3(offsets[i].x, y + offsets[i].y, offsets[i].z);
                    velocities[i] = new Vector3(0f, speed, 0f);
                }

                if (!BuoyancySolver.TryEvaluate(
                        profile, Mass, Gravity, UnitBounds.size.y, thickness, targetDepth,
                        world, heights, velocities, forces, out _))
                {
                    return float.NaN;
                }

                float total = 0f;

                for (int i = 0; i < count; i++)
                {
                    total += forces[i].y;
                }

                speed += (total / Mass - Gravity) * StepSeconds;
                y += speed * StepSeconds;
            }

            return y;
        }

        private static bool CheckFloatFraction(string name, float ratio, float expected)
        {
            var profile = new BuoyancyProfile(ratio, 0f, 1f, 0.3f, 0f, 5f, 0.3f);
            float y = Settle(profile, BuoyancySolver.NoTargetDepth, 0.3f, SettleSeconds);
            float fraction = Mathf.Clamp01(0.5f - y);
            bool isPassed = Mathf.Abs(fraction - expected) <= FractionTolerance;

            WaveCheckLog.Report(
                $"Buoyancy{name}",
                isPassed,
                $"ratio={ratio:F3}, fraction={fraction:F3}, expected={expected:F3}");

            return isPassed;
        }

        private static bool CheckSubmerged()
        {
            const float target = 1f;

            var profile = new BuoyancyProfile(2f, 0f, 1f, 0.3f, 0f, 5f, 0.3f);
            float y = Settle(profile, target, -0.3f, SettleSeconds);
            bool isPassed = Mathf.Abs(-y - target) <= DepthTolerance;

            WaveCheckLog.Report(
                "BuoyancySubmerged",
                isPassed,
                $"centroidDepth={-y:F3}, target={target:F3}");

            return isPassed;
        }

        private static bool CheckSink()
        {
            var profile = new BuoyancyProfile(0.6f, 0f, 1f, 0.3f, 0f, 5f, 0.3f);
            float y = Settle(profile, BuoyancySolver.NoTargetDepth, 0f, SinkSeconds);
            bool isPassed = y < SinkLimit;

            WaveCheckLog.Report("BuoyancySink", isPassed, $"y={y:F3}, limit={SinkLimit:F3}");

            return isPassed;
        }

        private static bool CheckFollow()
        {
            const int count = 4;

            var world = new Vector3[count];
            var heights = new float[count];
            var velocities = new Vector3[count];
            var forces = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                heights[i] = i % 2 == 0 ? FollowSpread : -FollowSpread;
            }

            var flat = new BuoyancyProfile(2f, 0f, 0f, 0f, 0.5f, 5f, 0.3f);
            var local = new BuoyancyProfile(2f, 0f, 1f, 1f, 0.5f, 5f, 0.3f);

            bool isEvaluated =
                BuoyancySolver.TryEvaluate(
                    flat, Mass, Gravity, 1f, 0.5f, BuoyancySolver.NoTargetDepth,
                    world, heights, velocities, forces, out _);

            float flatDifference = Mathf.Abs(forces[0].y - forces[1].y);

            isEvaluated &=
                BuoyancySolver.TryEvaluate(
                    local, Mass, Gravity, 1f, 0.5f, BuoyancySolver.NoTargetDepth,
                    world, heights, velocities, forces, out _);

            float localDifference = Mathf.Abs(forces[0].y - forces[1].y);
            bool isPassed = isEvaluated && flatDifference < ForceEpsilon && localDifference > ForceEpsilon;

            WaveCheckLog.Report(
                "BuoyancyFollow",
                isPassed,
                $"flatDiff={flatDifference:F5}, localDiff={localDifference:F5}");

            return isPassed;
        }

        private static bool CheckInvalid()
        {
            var profile = new BuoyancyProfile();
            var world = new Vector3[1];
            var heights = new float[1];
            var velocities = new Vector3[1];
            var forces = new Vector3[1];

            bool isMassRejected = !BuoyancySolver.TryEvaluate(
                profile, 0f, Gravity, 1f, 0.5f, BuoyancySolver.NoTargetDepth,
                world, heights, velocities, forces, out _);

            bool isLengthRejected = !BuoyancySolver.TryEvaluate(
                profile, Mass, Gravity, 1f, 0.5f, BuoyancySolver.NoTargetDepth,
                world, new float[2], velocities, forces, out _);

            bool isNullRejected = !BuoyancySolver.TryEvaluate(
                null, Mass, Gravity, 1f, 0.5f, BuoyancySolver.NoTargetDepth,
                world, heights, velocities, forces, out _);

            bool isPassed = isMassRejected && isLengthRejected && isNullRejected;

            WaveCheckLog.Report(
                "BuoyancyInvalid",
                isPassed,
                $"mass={isMassRejected}, length={isLengthRejected}, null={isNullRejected}");

            return isPassed;
        }

        private static bool CheckGrid()
        {
            var offsets = new List<Vector3>();
            int cube = BuoyancyPointGrid.BuildOffsets(UnitBounds, Divisions, offsets);
            bool isCubeOk = cube == 8
                && Mathf.Abs(offsets[0].x + 0.25f) < ForceEpsilon
                && Mathf.Abs(offsets[0].y + 0.25f) < ForceEpsilon;

            int mixed = BuoyancyPointGrid.BuildOffsets(UnitBounds, new Vector3Int(3, 1, 2), offsets);
            bool isMixedOk = mixed == 6;
            bool isZeroRejected = BuoyancyPointGrid.BuildOffsets(UnitBounds, new Vector3Int(0, 1, 1), offsets) == 0;
            bool isPassed = isCubeOk && isMixedOk && isZeroRejected;

            WaveCheckLog.Report(
                "BuoyancyGrid",
                isPassed,
                $"cube={isCubeOk}, mixed={isMixedOk}, zero={isZeroRejected}");

            return isPassed;
        }
    }
}
