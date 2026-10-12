using Lighthouse.World.Clock.Rules;
using UnityEngine;

namespace Lighthouse.World.Clock.Sandbox
{
    public static class ClockChecks
    {
        private const float FixedDay = 300f;
        private const float FixedNight = 120f;
        private const float FixedWarning = 60f;
        private const double BeforeBoundary = 0.1;
        private const double Epsilon = 0.001;

        public static bool RunAll(DayCycleConfig config)
        {
            bool isPassed = RunFixedBoundaries();
            isPassed &= RunNoWarning();
            isPassed &= RunConfigCycles(config);
            return isPassed;
        }

        private static bool RunFixedBoundaries()
        {
            bool isPassed =
                Matches(0.0, FixedDay, FixedNight, FixedWarning, 1, DayPhase.Day)
                && Matches(240.0 - BeforeBoundary, FixedDay, FixedNight, FixedWarning, 1, DayPhase.Day)
                && Matches(240.0, FixedDay, FixedNight, FixedWarning, 1, DayPhase.Dusk)
                && Matches(300.0 - BeforeBoundary, FixedDay, FixedNight, FixedWarning, 1, DayPhase.Dusk)
                && Matches(300.0, FixedDay, FixedNight, FixedWarning, 1, DayPhase.Night)
                && Matches(420.0 - BeforeBoundary, FixedDay, FixedNight, FixedWarning, 1, DayPhase.Night)
                && Matches(420.0, FixedDay, FixedNight, FixedWarning, 2, DayPhase.Day)
                && Matches(-5.0, FixedDay, FixedNight, FixedWarning, 1, DayPhase.Day)
                && Matches(420.0 * 4.0 + 300.0, FixedDay, FixedNight, FixedWarning, 5, DayPhase.Night);

            Report("FixedBoundaries", isPassed, "300/120/60");
            return isPassed;
        }

        private static bool RunNoWarning()
        {
            bool isPassed =
                Matches(9.9, 10f, 5f, 0f, 1, DayPhase.Day)
                && Matches(10.0, 10f, 5f, 0f, 1, DayPhase.Night)
                && Matches(15.0, 10f, 5f, 0f, 2, DayPhase.Day);

            Report("NoWarning", isPassed, "10/5/0");
            return isPassed;
        }

        private static bool RunConfigCycles(DayCycleConfig config)
        {
            double day = config.DaySeconds;
            double cycle = config.CycleSeconds;
            double warning = config.ReturnWarningSeconds;
            bool isPassed = true;

            for (int k = 0; k <= config.TotalDays; k++)
            {
                double start = k * cycle;
                int dayIndex = k + 1;

                isPassed &= Matches(start, config, dayIndex, DayPhase.Day);
                isPassed &= Matches(start + day - warning, config, dayIndex, warning > 0.0 ? DayPhase.Dusk : DayPhase.Night);
                isPassed &= Matches(start + day, config, dayIndex, DayPhase.Night);
                isPassed &= Matches(start + cycle - Epsilon, config, dayIndex, DayPhase.Night);
            }

            Report("ConfigCycles", isPassed, $"totalDays={config.TotalDays}, cycle={cycle}");
            return isPassed;
        }

        private static bool Matches(double elapsed, DayCycleConfig config, int expectedDay, DayPhase expectedPhase)
        {
            return Matches(elapsed, config.DaySeconds, config.NightSeconds, config.ReturnWarningSeconds, expectedDay, expectedPhase);
        }

        private static bool Matches(double elapsed, float day, float night, float warning, int expectedDay, DayPhase expectedPhase)
        {
            DayCycleState state = DayCycleMath.Evaluate(elapsed, day, night, warning);
            return state.DayIndex == expectedDay && state.Phase == expectedPhase;
        }

        private static void Report(string name, bool isPassed, string detail)
        {
            Debug.Log($"[{nameof(ClockChecks)}] {(isPassed ? "PASS" : "FAIL")} {name} ({detail})");
        }
    }
}
