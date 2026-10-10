using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public static class WaveModifierChecks
    {
        public static bool RunAll(WaveCheckContext context)
        {
            bool isAllPassed = CheckCenter(context);

            isAllPassed &= CheckFalloffRange(context);
            isAllPassed &= CheckValidity();

            return isAllPassed;
        }

        private static bool CheckCenter(WaveCheckContext context)
        {
            ReadOnlySpan<WaveParams> waves = context.Waves;
            Vector2 center = context.Center;

            float baseHeight = WaveModel.HeightAt(waves, ReadOnlySpan<WaveModifierData>.Empty, center.x, center.y, context.Time);
            float bumpDelta = WaveModel.HeightAt(waves, context.BumpOnly, center.x, center.y, context.Time) - baseHeight;
            float vortexDelta = WaveModel.HeightAt(waves, context.VortexOnly, center.x, center.y, context.Time) - baseHeight;

            bool isPassed = Mathf.Abs(bumpDelta - context.ModifierStrength) <= WaveCheckContext.BoundEpsilon
                && Mathf.Abs(vortexDelta + context.ModifierStrength) <= WaveCheckContext.BoundEpsilon;
            WaveCheckLog.Report("ModifierCenter", isPassed, $"bumpDelta={bumpDelta:F4}, vortexDelta={vortexDelta:F4}, strength={context.ModifierStrength}");
            return isPassed;
        }

        private static bool CheckFalloffRange(WaveCheckContext context)
        {
            ReadOnlySpan<WaveParams> waves = context.Waves;
            ReadOnlySpan<WaveModifierData> none = ReadOnlySpan<WaveModifierData>.Empty;
            Vector2[] points = context.SamplePoints;
            Vector2 center = context.Center;
            float radiusSqr = context.ModifierRadius * context.ModifierRadius;
            int outsideCount = 0;
            int outsideMismatches = 0;
            int insideMismatches = 0;

            for (int i = 0; i < points.Length; i++)
            {
                Vector2 point = points[i];

                float baseHeight = WaveModel.HeightAt(waves, none, point.x, point.y, context.Time);
                float bumpOffset = WaveModel.HeightAt(waves, context.BumpOnly, point.x, point.y, context.Time) - baseHeight;
                float vortexOffset = WaveModel.HeightAt(waves, context.VortexOnly, point.x, point.y, context.Time) - baseHeight;

                float offsetX = point.x - center.x;
                float offsetZ = point.y - center.y;
                bool isOutside = offsetX * offsetX + offsetZ * offsetZ >= radiusSqr;

                if (isOutside)
                {
                    outsideCount++;
                    if (bumpOffset != 0f || vortexOffset != 0f)
                    {
                        outsideMismatches++;
                    }
                }
                else if (bumpOffset < -WaveCheckContext.BoundEpsilon || vortexOffset > WaveCheckContext.BoundEpsilon)
                {
                    insideMismatches++;
                }
            }

            bool isPassed = outsideMismatches == 0 && insideMismatches == 0;
            WaveCheckLog.Report("ModifierFalloff", isPassed, $"outside={outsideCount} (mismatch {outsideMismatches}), insideMismatch={insideMismatches}");
            return isPassed;
        }

        private static bool CheckValidity()
        {
            WaveModifierData[] valid = { new WaveModifierData(WaveModifierKind.Bump, Vector2.zero, 1f, 1f, 0f) };
            WaveModifierData[] tooMany = new WaveModifierData[WaveModifierModel.MaxModifiers + 1];

            bool isPassed = WaveModifierModel.AreValid(valid) && !WaveModifierModel.AreValid(tooMany);
            WaveCheckLog.Report("ModifierValidity", isPassed, $"validity={isPassed}, max={WaveModifierModel.MaxModifiers}");
            return isPassed;
        }
    }
}
