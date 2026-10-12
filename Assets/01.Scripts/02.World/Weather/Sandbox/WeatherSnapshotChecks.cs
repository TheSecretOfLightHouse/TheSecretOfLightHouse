using System;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Weather.Rules;
using static Lighthouse.World.Weather.Sandbox.WeatherCheckData;

namespace Lighthouse.World.Weather.Sandbox
{
    public static class WeatherSnapshotChecks
    {
        private const int ValiditySteps = 10;
        private const int AllocationIterations = 1000;

        public static bool RunAll()
        {
            bool isAllPassed = CheckEndpoints();

            isAllPassed &= CheckMidpoint();
            isAllPassed &= CheckClamp();
            isAllPassed &= CheckWaveValidity();
            isAllPassed &= CheckSlotMismatch();
            isAllPassed &= CheckNoAllocation();

            return isAllPassed;
        }

        private static bool CheckEndpoints()
        {
            WeatherSnapshot a = MakeCalm();
            WeatherSnapshot b = MakeStorm();
            WeatherSnapshot result = WeatherSnapshot.CreateBuffer(SlotCount);

            bool isPassed = WeatherSnapshot.TryLerp(in a, in b, 0f, ref result) && Matches(in result, in a);
            isPassed &= WeatherSnapshot.TryLerp(in a, in b, 1f, ref result) && Matches(in result, in b);

            WeatherCheckLog.Report("Endpoints", isPassed, "t=0 -> a, t=1 -> b");
            return isPassed;
        }

        private static bool CheckMidpoint()
        {
            WeatherSnapshot a = MakeCalm();
            WeatherSnapshot b = MakeStorm();
            WeatherSnapshot result = WeatherSnapshot.CreateBuffer(SlotCount);
            WeatherSnapshot expected = Make(1.5f, 0.175f, 8.5f, 0.5f, 0.4f);

            bool isPassed = WeatherSnapshot.TryLerp(in a, in b, 0.5f, ref result) && Matches(in result, in expected);

            WeatherCheckLog.Report("Midpoint", isPassed, "t=0.5 is the mean of every field");
            return isPassed;
        }

        private static bool CheckClamp()
        {
            WeatherSnapshot a = MakeCalm();
            WeatherSnapshot b = MakeStorm();
            WeatherSnapshot result = WeatherSnapshot.CreateBuffer(SlotCount);

            bool isPassed = WeatherSnapshot.TryLerp(in a, in b, -1f, ref result) && Matches(in result, in a);
            isPassed &= WeatherSnapshot.TryLerp(in a, in b, 2f, ref result) && Matches(in result, in b);

            WeatherCheckLog.Report("Clamp", isPassed, "t outside 0..1 clamps");
            return isPassed;
        }

        private static bool CheckWaveValidity()
        {
            WeatherSnapshot a = MakeCalm();
            WeatherSnapshot b = MakeStorm();
            WeatherSnapshot result = WeatherSnapshot.CreateBuffer(SlotCount);
            bool isPassed = true;

            for (int step = 0; step <= ValiditySteps; step++)
            {
                isPassed &= WeatherSnapshot.TryLerp(in a, in b, step / (float)ValiditySteps, ref result);
                isPassed &= WaveModel.IsValid(result.WaveSpan);
            }

            WeatherCheckLog.Report("WaveValidity", isPassed, $"{ValiditySteps + 1} steps pass WaveModel.IsValid");
            return isPassed;
        }

        private static bool CheckSlotMismatch()
        {
            WeatherSnapshot a = MakeCalm();
            WeatherSnapshot other = WeatherSnapshot.CreateBuffer(SlotCount - 1);
            WeatherSnapshot result = MakeCalm();
            WeatherSnapshot untouched = MakeCalm();

            bool isPassed = !WeatherSnapshot.TryLerp(in a, in other, 0.5f, ref result) && Matches(in result, in untouched);

            WeatherCheckLog.Report("SlotMismatch", isPassed, "different slot counts are rejected without writing");
            return isPassed;
        }

        private static bool CheckNoAllocation()
        {
            WeatherSnapshot a = MakeCalm();
            WeatherSnapshot b = MakeStorm();
            WeatherSnapshot result = WeatherSnapshot.CreateBuffer(SlotCount);

            WeatherSnapshot.TryLerp(in a, in b, 0.5f, ref result);
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < AllocationIterations; i++)
            {
                WeatherSnapshot.TryLerp(in a, in b, i / (float)AllocationIterations, ref result);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            bool isPassed = allocated == 0L;

            WeatherCheckLog.Report("NoAllocation", isPassed, $"{AllocationIterations} calls allocated {allocated} B");
            return isPassed;
        }
    }
}
