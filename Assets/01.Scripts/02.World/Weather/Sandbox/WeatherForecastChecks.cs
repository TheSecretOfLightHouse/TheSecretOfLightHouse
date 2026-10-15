using Lighthouse.World.Weather.Rules;
using Lighthouse.World.Weather.Server;
using UnityEngine;

namespace Lighthouse.World.Weather.Sandbox
{
    public static class WeatherForecastChecks
    {
        private const int SeedSamples = 200;
        private const int SampleDays = 10;
        private const ulong FixedSeed = 0xDEADBEEF12345678UL;
        private const float DaySeconds = 12f;
        private const float ExpectedTransitionSeconds = 2.4f;
        private const WeatherType InvalidType = (WeatherType)99;

        public static bool RunAll()
        {
            bool isAllPassed = CheckTable();

            isAllPassed &= CheckProfile();
            isAllPassed &= CheckSameSeed();
            isAllPassed &= CheckLength();
            isAllPassed &= CheckDifferentSeeds();
            isAllPassed &= CheckRules();
            isAllPassed &= CheckDistribution();

            return isAllPassed;
        }

        public static WeatherWeightTable CreateDefaultTable()
        {
            var profile = ScriptableObject.CreateInstance<WeatherScheduleProfile>();
            WeatherWeightTable table = profile.BuildTable();
            Object.DestroyImmediate(profile);
            return table;
        }

        private static bool CheckTable()
        {
            var table = new WeatherWeightTable(new[]
            {
                new WeatherWeight { Type = WeatherType.Calm, Weight = 1f },
                new WeatherWeight { Type = WeatherType.Rain, Weight = 3f },
                new WeatherWeight { Type = WeatherType.Maelstrom, Weight = 100f },
                new WeatherWeight { Type = WeatherType.Storm, Weight = 0f }
            });
            var onlyMaelstrom = new WeatherWeightTable(new[]
            {
                new WeatherWeight { Type = WeatherType.Maelstrom, Weight = 1f }
            });

            bool isPassed = !table.IsEmpty
                && table.Pick(0f) == WeatherType.Calm
                && table.Pick(0.24f) == WeatherType.Calm
                && table.Pick(0.26f) == WeatherType.Rain
                && table.Pick(0.999f) == WeatherType.Rain
                && table.Pick(5f) == WeatherType.Rain
                && onlyMaelstrom.IsEmpty
                && new WeatherWeightTable(null).IsEmpty;

            WeatherCheckLog.Report("WeightTable", isPassed, "cumulative pick; maelstrom and zero weights excluded");
            return isPassed;
        }

        private static bool CheckProfile()
        {
            var profile = ScriptableObject.CreateInstance<WeatherScheduleProfile>();

            bool isPassed = profile.FirstDayType == WeatherType.Calm
                && Mathf.Approximately(profile.GetTransitionSeconds(DaySeconds), ExpectedTransitionSeconds)
                && !profile.BuildTable().IsEmpty;

            Object.DestroyImmediate(profile);

            WeatherCheckLog.Report("Profile", isPassed, "defaults: first day Calm, transition 20% of day");
            return isPassed;
        }

        private static bool CheckSameSeed()
        {
            WeatherWeightTable table = CreateDefaultTable();
            IForecast[] a = WeatherForecastGenerator.Generate(FixedSeed, SampleDays, WeatherType.Calm, table);
            IForecast[] b = WeatherForecastGenerator.Generate(FixedSeed, SampleDays, WeatherType.Calm, table);

            bool isPassed = IsSame(a, b);

            WeatherCheckLog.Report("SameSeed", isPassed, "same seed and days give the same schedule");
            return isPassed;
        }

        private static bool CheckLength()
        {
            WeatherWeightTable table = CreateDefaultTable();

            bool isPassed = WeatherForecastGenerator.Generate(FixedSeed, 3, WeatherType.Calm, table).Length == 3
                && WeatherForecastGenerator.Generate(FixedSeed, 5, WeatherType.Calm, table).Length == 5
                && WeatherForecastGenerator.Generate(FixedSeed, 8, WeatherType.Calm, table).Length == 8
                && WeatherForecastGenerator.Generate(FixedSeed, 0, WeatherType.Calm, table).Length == 0
                && WeatherForecastGenerator.Generate(FixedSeed, -1, WeatherType.Calm, table).Length == 0;

            WeatherCheckLog.Report("Length", isPassed, "schedule length equals TotalDays");
            return isPassed;
        }

        private static bool CheckDifferentSeeds()
        {
            WeatherWeightTable table = CreateDefaultTable();
            IForecast[] baseline = WeatherForecastGenerator.Generate(0UL, SampleDays, WeatherType.Calm, table);
            int differentCount = 0;

            for (ulong seed = 1UL; seed <= SeedSamples; seed++)
            {
                if (!IsSame(baseline, WeatherForecastGenerator.Generate(seed, SampleDays, WeatherType.Calm, table)))
                {
                    differentCount++;
                }
            }

            bool isPassed = differentCount > SeedSamples / 2;

            WeatherCheckLog.Report("DifferentSeeds", isPassed, $"{differentCount}/{SeedSamples} seeds differ from seed 0");
            return isPassed;
        }

        private static bool CheckRules()
        {
            WeatherWeightTable table = CreateDefaultTable();
            WeatherWeightTable empty = new WeatherWeightTable(null);
            bool isPassed = true;

            for (ulong seed = 0UL; seed < SeedSamples; seed++)
            {
                IForecast[] days = WeatherForecastGenerator.Generate(seed, SampleDays, WeatherType.Calm, table);
                isPassed &= days[0].GetWeather() == WeatherType.Calm;

                for (int i = 0; i < days.Length; i++)
                {
                    isPassed &= days[i].Day == i + 1 && days[i].GetWeather() != WeatherType.Maelstrom;
                }
            }

            isPassed &= WeatherForecastGenerator.Generate(1UL, 3, WeatherType.Maelstrom, table)[0].GetWeather() == WeatherType.Calm;

            IForecast[] fallback = WeatherForecastGenerator.Generate(1UL, 4, WeatherType.Rain, empty);

            for (int i = 0; i < fallback.Length; i++)
            {
                isPassed &= fallback[i].GetWeather() == WeatherType.Rain;
            }

            isPassed &= WeatherForecastGenerator.Generate(1UL, 3, InvalidType, table).Length == 3;

            WeatherCheckLog.Report("Rules", isPassed, "first day fixed, no Maelstrom, empty table falls back");
            return isPassed;
        }

        private static bool CheckDistribution()
        {
            WeatherWeightTable table = CreateDefaultTable();
            int[] counts = new int[(int)WeatherType.Maelstrom + 1];

            for (ulong seed = 1UL; seed <= SeedSamples; seed++)
            {
                IForecast[] days = WeatherForecastGenerator.Generate(seed, SampleDays, WeatherType.Calm, table);

                for (int i = 1; i < days.Length; i++)
                {
                    counts[(int)days[i].GetWeather()]++;
                }
            }

            bool isPassed = counts[(int)WeatherType.Calm] > 0
                && counts[(int)WeatherType.Overcast] > 0
                && counts[(int)WeatherType.Rain] > 0
                && counts[(int)WeatherType.Storm] > 0
                && counts[(int)WeatherType.Maelstrom] == 0
                && counts[(int)WeatherType.Storm] < counts[(int)WeatherType.Calm];

            WeatherCheckLog.Report("Distribution", isPassed,
                $"calm {counts[0]}, overcast {counts[1]}, rain {counts[2]}, storm {counts[3]}");
            return isPassed;
        }

        private static bool IsSame(IForecast[] a, IForecast[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].Day != b[i].Day || a[i].GetWeather() != b[i].GetWeather())
                {
                    return false;
                }
            }

            return true;
        }
    }
}
