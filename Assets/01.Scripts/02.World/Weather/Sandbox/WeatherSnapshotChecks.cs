using System;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Weather.Rules;
using UnityEngine;

namespace Lighthouse.World.Weather.Sandbox
{
    public static class WeatherSnapshotChecks
    {
        private const float Tolerance = 1e-4f;
        private const int SlotCount = 3;
        private const int ValiditySteps = 10;
        private const int AllocationIterations = 1000;

        private static readonly Vector2[] Directions =
        {
            Vector2.right,
            new Vector2(0.7071f, 0.7071f),
            new Vector2(0.6f, 0.8f)
        };

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

        private static WeatherSnapshot MakeCalm()
        {
            return Make(0.5f, 0.1f, 2f, 0.1f, 0f);
        }

        private static WeatherSnapshot MakeStorm()
        {
            return Make(2.5f, 0.25f, 15f, 0.9f, 0.8f);
        }

        private static WeatherSnapshot Make(float amplitude, float steepness, float windX, float scalar, float intensity)
        {
            WeatherSnapshot snapshot = WeatherSnapshot.CreateBuffer(SlotCount);

            for (int i = 0; i < SlotCount; i++)
            {
                snapshot.Waves[i] = new WaveParams
                {
                    Amplitude = amplitude * (i + 1),
                    Wavelength = 40f / (i + 1),
                    Direction = Directions[i],
                    Steepness = steepness,
                    Speed = 3f + i
                };
            }

            snapshot.Wind = new Vector2(windX, 0f);
            snapshot.Fog = scalar;
            snapshot.Rain = scalar * 0.5f;
            snapshot.Lightning = scalar * 10f;
            snapshot.TornadoChance = scalar * 0.2f;
            snapshot.StormIntensity = intensity;
            return snapshot;
        }

        private static bool Matches(in WeatherSnapshot x, in WeatherSnapshot y)
        {
            if (x.Waves.Length != y.Waves.Length)
            {
                return false;
            }

            bool isMatch = Near(x.Wind.x, y.Wind.x) && Near(x.Wind.y, y.Wind.y)
                && Near(x.Fog, y.Fog) && Near(x.Rain, y.Rain) && Near(x.Lightning, y.Lightning)
                && Near(x.TornadoChance, y.TornadoChance) && Near(x.StormIntensity, y.StormIntensity);

            for (int i = 0; i < x.Waves.Length; i++)
            {
                WaveParams p = x.Waves[i];
                WaveParams q = y.Waves[i];

                isMatch &= Near(p.Amplitude, q.Amplitude) && Near(p.Steepness, q.Steepness)
                    && Near(p.Wavelength, q.Wavelength) && Near(p.Speed, q.Speed)
                    && Near(p.Direction.x, q.Direction.x) && Near(p.Direction.y, q.Direction.y);
            }

            return isMatch;
        }

        private static bool Near(float x, float y)
        {
            return Mathf.Abs(x - y) <= Tolerance;
        }
    }
}
