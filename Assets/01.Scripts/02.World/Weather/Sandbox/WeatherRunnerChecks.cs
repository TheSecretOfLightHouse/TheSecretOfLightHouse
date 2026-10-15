using System;
using System.Collections.Generic;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Weather.Rules;
using Lighthouse.World.Weather.Server;
using UnityEngine;

namespace Lighthouse.World.Weather.Sandbox
{
    public static class WeatherRunnerChecks
    {
        private const int Days = 3;
        private const ulong Seed = 42UL;
        private const float DaySeconds = 12f;
        private const float ExpectedDuration = 2.4f;
        private const int TamperableTypeCount = 4;

        public static bool RunAll()
        {
            bool isAllPassed = CheckSequence();

            isAllPassed &= CheckGuards();
            isAllPassed &= CheckTamper();
            isAllPassed &= CheckLocked();
            isAllPassed &= CheckDispose();

            return isAllPassed;
        }

        private static bool CheckSequence()
        {
            var profile = ScriptableObject.CreateInstance<WeatherScheduleProfile>();
            var clock = new FakeGameClock { TotalDays = Days };
            var sink = new FakeSink();
            int seedCalls = 0;

            using (var runner = new WeatherScheduleRunner(clock, profile, DaySeconds, sink, () => { seedCalls++; return Seed; }))
            {
                clock.Raise(1);
                clock.Raise(2);
                clock.Raise(3);
            }

            IForecast[] expected = WeatherForecastGenerator.Generate(Seed, Days, profile.FirstDayType, profile.BuildTable());
            bool isPassed = sink.Types.Count == Days && seedCalls == 1;

            for (int i = 0; i < sink.Types.Count && i < expected.Length; i++)
            {
                isPassed &= sink.Types[i] == expected[i].GetWeather()
                    && Mathf.Approximately(sink.Durations[i], ExpectedDuration);
            }

            UnityEngine.Object.DestroyImmediate(profile);

            WeatherCheckLog.Report("RunnerSequence", isPassed, "one transition per day from the schedule, seed asked once");
            return isPassed;
        }

        private static bool CheckGuards()
        {
            var profile = ScriptableObject.CreateInstance<WeatherScheduleProfile>();
            var clock = new FakeGameClock { TotalDays = Days };
            var sink = new FakeSink();

            using (var runner = new WeatherScheduleRunner(clock, profile, DaySeconds, sink, () => Seed))
            {
                runner.Process(1);
                clock.Raise(1);
                clock.Raise(2);
                clock.Raise(2);
                clock.Raise(1);
                clock.Raise(3);
                clock.Raise(4);
                clock.Raise(5);
            }

            bool isPassed = sink.Types.Count == Days;

            UnityEngine.Object.DestroyImmediate(profile);

            WeatherCheckLog.Report("RunnerGuards", isPassed, "catch-up and duplicates once, past TotalDays nothing");
            return isPassed;
        }

        private static bool CheckTamper()
        {
            var profile = ScriptableObject.CreateInstance<WeatherScheduleProfile>();
            var clock = new FakeGameClock { TotalDays = Days };
            var sink = new FakeSink();

            using (var runner = new WeatherScheduleRunner(clock, profile, DaySeconds, sink, () => Seed))
            {
                bool isPassed = !runner.TryGetOriginal(1, out _) && !runner.Tamper(2, WeatherType.Storm);

                clock.Raise(1);
                runner.TryGetOriginal(2, out WeatherType original);
                WeatherType other = (WeatherType)(((int)original + 1) % TamperableTypeCount);

                isPassed &= runner.Tamper(2, other);
                clock.Raise(2);
                isPassed &= sink.Types.Count == 2 && sink.Types[1] == other
                    && runner.TryGetOriginal(2, out WeatherType kept) && kept == original;

                UnityEngine.Object.DestroyImmediate(profile);

                WeatherCheckLog.Report("RunnerTamper", isPassed, "tampered day uses the override, original is kept");
                return isPassed;
            }
        }

        private static bool CheckLocked()
        {
            var profile = ScriptableObject.CreateInstance<WeatherScheduleProfile>();
            var clock = new FakeGameClock { TotalDays = Days };
            var sink = new FakeSink { IsLocked = true };

            using (var runner = new WeatherScheduleRunner(clock, profile, DaySeconds, sink, () => Seed))
            {
                clock.Raise(1);
                bool isPassed = sink.Types.Count == 0;

                sink.IsLocked = false;
                clock.Raise(2);
                isPassed &= sink.Types.Count == 1;

                UnityEngine.Object.DestroyImmediate(profile);

                WeatherCheckLog.Report("RunnerLocked", isPassed, "no transition while locked");
                return isPassed;
            }
        }

        private static bool CheckDispose()
        {
            var profile = ScriptableObject.CreateInstance<WeatherScheduleProfile>();
            var clock = new FakeGameClock { TotalDays = Days };
            var sink = new FakeSink();
            var runner = new WeatherScheduleRunner(clock, profile, DaySeconds, sink, () => Seed);

            runner.Dispose();
            clock.Raise(1);
            bool isPassed = sink.Types.Count == 0;

            UnityEngine.Object.DestroyImmediate(profile);

            WeatherCheckLog.Report("RunnerDispose", isPassed, "no events after Dispose");
            return isPassed;
        }

        private sealed class FakeGameClock : IGameClock
        {
            public int TotalDays { get; set; }
            public int DayIndex { get; private set; } = 1;
            public DayPhase Phase => DayPhase.Day;

            public event Action<int> DayStarted;
            public event Action<int> NightStarted
            {
                add { }
                remove { }
            }

            public event Action<int> ReturnWarningIssued
            {
                add { }
                remove { }
            }

            public void Raise(int day)
            {
                DayIndex = day;
                DayStarted?.Invoke(day);
            }
        }

        private sealed class FakeSink : IWeatherTransitionSink
        {
            public bool IsLocked { get; set; }
            public List<WeatherType> Types { get; } = new List<WeatherType>();
            public List<float> Durations { get; } = new List<float>();

            public bool BeginTransition(WeatherType to, float duration)
            {
                Types.Add(to);
                Durations.Add(duration);
                return true;
            }
        }
    }
}
