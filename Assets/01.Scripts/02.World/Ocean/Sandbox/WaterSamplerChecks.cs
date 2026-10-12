using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public static class WaterSamplerChecks
    {
        private const int GridSide = 10;
        private const int PointCount = GridSide * GridSide;
        private const float PointSpacing = 3f;
        private const int GcPointCount = 32;
        private const int GcRunCount = 100;
        private const float HeightTolerance = 0.0001f;

        public static bool RunAll(WaveParams[] waves, WaveModifierData[] modifiers, float time)
        {
            bool isAllPassed = CheckJobMatchesModel(waves, modifiers, time);
            isAllPassed &= CheckNoGc(waves, modifiers, time);
            return isAllPassed;
        }

        private static bool CheckJobMatchesModel(WaveParams[] waves, WaveModifierData[] modifiers, float time)
        {
            using (var sampler = new WaterSampler(PointCount, 0))
            {
                if (!TryFillGrid(sampler, PointCount, out SamplerTicket ticket))
                {
                    WaveCheckLog.Report("JobMatchesModel", false, "allocation failed");
                    return false;
                }

                sampler.Run(waves, modifiers, time);

                float maxError = 0f;
                for (int i = 0; i < PointCount; i++)
                {
                    Vector3 point = GetGridPoint(i);
                    float expected = WaveModel.HeightAt(waves, modifiers, point.x, point.z, time);
                    maxError = Mathf.Max(maxError, Mathf.Abs(expected - sampler.GetHeight(in ticket, i)));
                }

                bool isPassed = maxError <= HeightTolerance;
                WaveCheckLog.Report("JobMatchesModel", isPassed, $"maxError={maxError:F6}, tolerance={HeightTolerance}");
                return isPassed;
            }
        }

        private static bool CheckNoGc(WaveParams[] waves, WaveModifierData[] modifiers, float time)
        {
            using (var sampler = new WaterSampler(GcPointCount, 0))
            {
                if (!TryFillGrid(sampler, GcPointCount, out _))
                {
                    WaveCheckLog.Report("NoGc", false, "allocation failed");
                    return false;
                }

                sampler.Run(waves, modifiers, time);

                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int run = 0; run < GcRunCount; run++)
                {
                    sampler.Run(waves, modifiers, time + run);
                }

                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                bool isPassed = allocated == 0;
                WaveCheckLog.Report("NoGc", isPassed, $"allocated={allocated} bytes over {GcRunCount} runs");
                return isPassed;
            }
        }

        private static bool TryFillGrid(WaterSampler sampler, int count, out SamplerTicket ticket)
        {
            if (!sampler.TryAllocate(SamplerPool.Ship, count, out ticket))
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                sampler.SetPoint(in ticket, i, GetGridPoint(i));
            }

            return true;
        }

        private static Vector3 GetGridPoint(int index)
        {
            float x = (index % GridSide - GridSide / 2) * PointSpacing;
            float z = (index / GridSide - GridSide / 2) * PointSpacing;
            return new Vector3(x, 0f, z);
        }
    }
}
