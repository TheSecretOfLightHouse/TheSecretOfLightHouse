using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public static class WaveModelChecks
    {
        private const float FiniteDifferenceStep = 0.05f;
        private const float NormalDotThreshold = 0.999f;
        private const float EmptyProbeOffsetX = 3f;
        private const float EmptyProbeOffsetZ = -7f;

        public static bool RunAll(WaveCheckContext context)
        {
            bool isAllPassed = CheckValidity(context);

            isAllPassed &= CheckDeterminism(context);
            isAllPassed &= CheckEmptyWaves(context);
            isAllPassed &= CheckAmplitudeBound(context);
            isAllPassed &= CheckInverse(context);
            isAllPassed &= CheckNormals(context);

            return isAllPassed;
        }

        private static bool CheckValidity(WaveCheckContext context)
        {
            ReadOnlySpan<WaveParams> waves = context.Waves;
            float totalSteepness = 0f;

            for (int i = 0; i < waves.Length; i++)
            {
                totalSteepness += waves[i].Steepness;
            }

            bool isPassed = WaveModel.IsValid(waves);
            WaveCheckLog.Report("Validity", isPassed, $"waves={waves.Length}, totalSteepness={totalSteepness:F3}, max={WaveModel.MaxTotalSteepness}");
            return isPassed;
        }

        private static bool CheckDeterminism(WaveCheckContext context)
        {
            ReadOnlySpan<WaveParams> waves = context.Waves;
            ReadOnlySpan<WaveModifierData> modifiers = context.Modifiers;
            Vector2[] points = context.SamplePoints;
            int mismatches = 0;

            for (int i = 0; i < points.Length; i++)
            {
                Vector2 point = points[i];

                float heightA = WaveModel.HeightAt(waves, modifiers, point.x, point.y, context.Time);
                float heightB = WaveModel.HeightAt(waves, modifiers, point.x, point.y, context.Time);
                Vector3 normalA = WaveModel.NormalAt(waves, modifiers, point.x, point.y, context.Time);
                Vector3 normalB = WaveModel.NormalAt(waves, modifiers, point.x, point.y, context.Time);

                bool isHeightSame = heightA == heightB;
                bool isNormalSame = normalA.x == normalB.x && normalA.y == normalB.y && normalA.z == normalB.z;

                if (!isHeightSame || !isNormalSame)
                {
                    mismatches++;
                }
            }

            bool isPassed = mismatches == 0;
            WaveCheckLog.Report("Determinism", isPassed, $"mismatches={mismatches}, modifiers={modifiers.Length}");
            return isPassed;
        }

        private static bool CheckEmptyWaves(WaveCheckContext context)
        {
            ReadOnlySpan<WaveParams> none = ReadOnlySpan<WaveParams>.Empty;
            float x = context.Center.x + EmptyProbeOffsetX;
            float z = context.Center.y + EmptyProbeOffsetZ;

            float height = WaveModel.HeightAt(none, ReadOnlySpan<WaveModifierData>.Empty, x, z, context.Time);
            Vector3 normal = WaveModel.NormalAt(none, ReadOnlySpan<WaveModifierData>.Empty, x, z, context.Time);

            bool isPassed = Mathf.Approximately(height, 0f) && Mathf.Approximately(normal.y, 1f);
            WaveCheckLog.Report("EmptyWaves", isPassed, $"height={height}, normal={normal}");
            return isPassed;
        }

        private static bool CheckAmplitudeBound(WaveCheckContext context)
        {
            ReadOnlySpan<WaveParams> waves = context.Waves;
            Vector2[] points = context.SamplePoints;
            float amplitudeSum = 0f;

            for (int i = 0; i < waves.Length; i++)
            {
                amplitudeSum += waves[i].Amplitude;
            }

            float maxAbsHeight = 0f;

            for (int i = 0; i < points.Length; i++)
            {
                float height = WaveModel.HeightAt(waves, ReadOnlySpan<WaveModifierData>.Empty, points[i].x, points[i].y, context.Time);
                maxAbsHeight = Mathf.Max(maxAbsHeight, Mathf.Abs(height));
            }

            bool isPassed = maxAbsHeight <= amplitudeSum + WaveCheckContext.BoundEpsilon;
            WaveCheckLog.Report("AmplitudeBound", isPassed, $"maxAbsHeight={maxAbsHeight:F4}, amplitudeSum={amplitudeSum:F4}");
            return isPassed;
        }

        private static bool CheckInverse(WaveCheckContext context)
        {
            ReadOnlySpan<WaveParams> waves = context.Waves;
            Vector2[] points = context.SamplePoints;
            float maxError = 0f;

            for (int i = 0; i < points.Length; i++)
            {
                Vector2 rest = points[i];
                Vector3 displacement = WaveModel.DisplacementAt(waves, rest.x, rest.y, context.Time);
                float height = WaveModel.HeightAt(
                    waves,
                    ReadOnlySpan<WaveModifierData>.Empty,
                    rest.x + displacement.x,
                    rest.y + displacement.z,
                    context.Time);

                maxError = Mathf.Max(maxError, Mathf.Abs(height - displacement.y));
            }

            bool isPassed = maxError <= context.InverseTolerance;
            WaveCheckLog.Report("Inverse", isPassed, $"maxError={maxError:F5}, tolerance={context.InverseTolerance}, iterations={WaveModel.InverseIterations}");
            return isPassed;
        }

        private static bool CheckNormals(WaveCheckContext context)
        {
            ReadOnlySpan<WaveParams> waves = context.Waves;

            float minDotWaves = MinNormalDot(context, waves, ReadOnlySpan<WaveModifierData>.Empty);
            float minDotBump = MinNormalDot(context, waves, context.BumpOnly);

            bool isPassed = minDotWaves >= NormalDotThreshold && minDotBump >= NormalDotThreshold;
            WaveCheckLog.Report("Normals", isPassed, $"minDot(waves)={minDotWaves:F5}, minDot(waves+bump)={minDotBump:F5}, threshold={NormalDotThreshold}");
            return isPassed;
        }

        private static float MinNormalDot(
            WaveCheckContext context,
            ReadOnlySpan<WaveParams> waves,
            ReadOnlySpan<WaveModifierData> modifiers)
        {
            Vector2[] points = context.SamplePoints;
            float doubleStep = FiniteDifferenceStep * 2f;
            float minDot = 1f;

            for (int i = 0; i < points.Length; i++)
            {
                float x = points[i].x;
                float z = points[i].y;

                float slopeX = (WaveModel.HeightAt(waves, modifiers, x + FiniteDifferenceStep, z, context.Time)
                    - WaveModel.HeightAt(waves, modifiers, x - FiniteDifferenceStep, z, context.Time)) / doubleStep;
                float slopeZ = (WaveModel.HeightAt(waves, modifiers, x, z + FiniteDifferenceStep, context.Time)
                    - WaveModel.HeightAt(waves, modifiers, x, z - FiniteDifferenceStep, context.Time)) / doubleStep;

                Vector3 expected = new Vector3(-slopeX, 1f, -slopeZ).normalized;
                Vector3 actual = WaveModel.NormalAt(waves, modifiers, x, z, context.Time);
                minDot = Mathf.Min(minDot, Vector3.Dot(actual, expected));
            }

            return minDot;
        }
    }
}
