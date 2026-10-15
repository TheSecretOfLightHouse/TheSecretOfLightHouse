using Lighthouse.World.Weather.Rules;
using Lighthouse.World.Weather.Server;

namespace Lighthouse.World.Weather.Sandbox
{
    public static class WeatherSchedulerChecks
    {
        private const int Days = 5;
        private const ulong Seed = 7UL;
        private const WeatherType InvalidType = (WeatherType)99;
        private const int TamperableTypeCount = 4;

        public static bool RunAll()
        {
            bool isAllPassed = CheckQuery();

            isAllPassed &= CheckTamper();
            isAllPassed &= CheckTamperRejected();
            isAllPassed &= CheckDayProgress();
            isAllPassed &= CheckEmpty();

            return isAllPassed;
        }

        private static WeatherScheduler Create()
        {
            IForecast[] days = WeatherForecastGenerator.Generate(
                Seed, Days, WeatherType.Calm, WeatherForecastChecks.CreateDefaultTable());
            return new WeatherScheduler(days);
        }

        private static bool CheckQuery()
        {
            WeatherScheduler scheduler = Create();
            bool isPassed = scheduler.DayCount == Days;

            for (int day = 1; day <= Days; day++)
            {
                isPassed &= scheduler.TryGetOriginal(day, out WeatherType original)
                    && scheduler.TryGetActual(day, out WeatherType actual)
                    && original == actual;
            }

            isPassed &= !scheduler.TryGetOriginal(0, out _)
                && !scheduler.TryGetOriginal(Days + 1, out _)
                && !scheduler.TryGetOriginal(-1, out _);

            WeatherCheckLog.Report("SchedulerQuery", isPassed, "original equals actual before tamper; bounds checked");
            return isPassed;
        }

        private static bool CheckTamper()
        {
            WeatherScheduler scheduler = Create();
            scheduler.NotifyDayStarted(1);
            scheduler.TryGetOriginal(2, out WeatherType original);
            WeatherType first = (WeatherType)(((int)original + 1) % TamperableTypeCount);
            WeatherType second = (WeatherType)(((int)original + 2) % TamperableTypeCount);

            bool isPassed = scheduler.Tamper(2, first);
            isPassed &= scheduler.TryGetOriginal(2, out WeatherType keptOriginal) && keptOriginal == original;
            isPassed &= scheduler.TryGetActual(2, out WeatherType changed) && changed == first;

            isPassed &= scheduler.Tamper(2, second);
            isPassed &= scheduler.TryGetOriginal(2, out keptOriginal) && keptOriginal == original;
            isPassed &= scheduler.TryGetActual(2, out changed) && changed == second;

            WeatherCheckLog.Report("Tamper", isPassed, "original kept, latest override wins, no nesting");
            return isPassed;
        }

        private static bool CheckTamperRejected()
        {
            WeatherScheduler scheduler = Create();
            scheduler.NotifyDayStarted(1);
            scheduler.TryGetOriginal(3, out WeatherType original);

            bool isPassed = !scheduler.Tamper(1, WeatherType.Storm)
                && !scheduler.Tamper(0, WeatherType.Storm)
                && !scheduler.Tamper(Days + 1, WeatherType.Storm)
                && !scheduler.Tamper(3, WeatherType.Maelstrom)
                && !scheduler.Tamper(3, InvalidType);

            isPassed &= scheduler.TryGetActual(3, out WeatherType actual) && actual == original;

            WeatherCheckLog.Report("TamperRejected", isPassed, "started day, out of range, Maelstrom, invalid type");
            return isPassed;
        }

        private static bool CheckDayProgress()
        {
            WeatherScheduler scheduler = Create();
            scheduler.NotifyDayStarted(1);
            scheduler.TryGetOriginal(2, out WeatherType original);

            scheduler.NotifyDayStarted(2);
            bool isPassed = !scheduler.Tamper(2, WeatherType.Storm);
            isPassed &= scheduler.TryGetActual(2, out WeatherType actual) && actual == original;

            scheduler.NotifyDayStarted(1);
            isPassed &= scheduler.CurrentDay == 2 && !scheduler.Tamper(2, WeatherType.Storm);
            isPassed &= scheduler.Tamper(3, WeatherType.Storm);

            WeatherCheckLog.Report("DayProgress", isPassed, "current day only moves forward; started day locked");
            return isPassed;
        }

        private static bool CheckEmpty()
        {
            var scheduler = new WeatherScheduler(null);

            bool isPassed = scheduler.DayCount == 0
                && !scheduler.TryGetOriginal(1, out _)
                && !scheduler.Tamper(1, WeatherType.Storm);

            WeatherCheckLog.Report("SchedulerEmpty", isPassed, "null schedule is safe");
            return isPassed;
        }
    }
}
