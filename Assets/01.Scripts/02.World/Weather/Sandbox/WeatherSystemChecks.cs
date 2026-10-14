using System;
using Lighthouse.World.Weather.Rules;
using static Lighthouse.World.Weather.Sandbox.WeatherCheckData;

namespace Lighthouse.World.Weather.Sandbox
{
    public static class WeatherSystemChecks
    {
        private const double Start = 10.0;
        private const float Duration = 20f;
        private const int AllocationIterations = 1000;
        private const WeatherType InvalidType = (WeatherType)99;

        public static bool RunAll()
        {
            bool isAllPassed = CheckCreate();

            isAllPassed &= CheckTransition();
            isAllPassed &= CheckIdempotent();
            isAllPassed &= CheckInvalidType();
            isAllPassed &= CheckMaelstromLock();
            isAllPassed &= CheckLateJoin();
            isAllPassed &= CheckCanApply();
            isAllPassed &= CheckReset();
            isAllPassed &= CheckZeroDuration();
            isAllPassed &= CheckListeners();
            isAllPassed &= CheckNoAllocation();

            return isAllPassed;
        }

        private static bool CheckCreate()
        {
            WeatherSystem system = CreateSystem(WeatherType.Storm);
            WeatherSnapshot storm = MakeStorm();
            WeatherState[] misordered = { new StormState(MakeStorm()), new CalmState(MakeCalm()) };

            bool isPassed = system != null && system.Target == WeatherType.Storm && !system.IsTransitioning
                && Matches(in system.Snapshot, in storm);
            isPassed &= !WeatherSystem.TryCreate(null, WeatherType.Calm, out _);
            isPassed &= !WeatherSystem.TryCreate(misordered, WeatherType.Calm, out _);
            isPassed &= !WeatherSystem.TryCreate(BuildStates(), InvalidType, out _);

            WeatherCheckLog.Report("Create", isPassed, "initial snapshot equals initial state; bad input rejected");
            return isPassed;
        }

        private static bool CheckTransition()
        {
            WeatherSystem system = CreateSystem(WeatherType.Calm);
            var listener = new CountingListener();
            system.AddListener(listener);
            WeatherSnapshot calm = MakeCalm();
            WeatherSnapshot storm = MakeStorm();
            WeatherSnapshot mid = Make(1.5f, 0.175f, 8.5f, 0.5f, 0.4f);

            bool isPassed = system.Apply(WeatherType.Calm, WeatherType.Storm, Start, Duration);

            system.Tick(Start - 5.0);
            isPassed &= Matches(in system.Snapshot, in calm) && system.IsTransitioning;

            system.Tick(Start + Duration * 0.5);
            isPassed &= Matches(in system.Snapshot, in mid);

            system.Tick(Start + Duration);
            isPassed &= Matches(in system.Snapshot, in storm) && !system.IsTransitioning && system.Target == WeatherType.Storm;

            system.Tick(Start + Duration + 1.0);
            isPassed &= listener.Count == 3;

            WeatherCheckLog.Report("Transition", isPassed, $"notified {listener.Count} times, settled at storm");
            return isPassed;
        }

        private static bool CheckIdempotent()
        {
            WeatherSystem system = CreateSystem(WeatherType.Calm);

            bool isPassed = system.Apply(WeatherType.Calm, WeatherType.Storm, Start, Duration);
            isPassed &= !system.Apply(WeatherType.Calm, WeatherType.Storm, Start, Duration);
            isPassed &= !system.Apply(WeatherType.Calm, WeatherType.Rain, Start, Duration);
            isPassed &= system.Target == WeatherType.Storm;
            isPassed &= system.Apply(WeatherType.Storm, WeatherType.Rain, Start + 1.0, Duration);
            isPassed &= system.Target == WeatherType.Rain;

            WeatherCheckLog.Report("Idempotent", isPassed, "same startTime applies once");
            return isPassed;
        }

        private static bool CheckInvalidType()
        {
            WeatherSystem system = CreateSystem(WeatherType.Calm);

            bool isPassed = !system.Apply(InvalidType, WeatherType.Storm, Start, Duration)
                && !system.Apply(WeatherType.Calm, InvalidType, Start + 1.0, Duration)
                && !system.IsTransitioning;

            WeatherCheckLog.Report("InvalidType", isPassed, "unknown weather types are rejected");
            return isPassed;
        }

        private static bool CheckMaelstromLock()
        {
            WeatherSystem system = CreateSystem(WeatherType.Calm);
            WeatherSnapshot maelstrom = MakeMaelstrom();

            bool isPassed = system.Apply(WeatherType.Calm, WeatherType.Storm, 0.0, 0f);
            system.Tick(1.0);
            isPassed &= system.Apply(WeatherType.Storm, WeatherType.Maelstrom, 5.0, 10f);
            system.Tick(100.0);
            isPassed &= system.Target == WeatherType.Maelstrom && Matches(in system.Snapshot, in maelstrom);

            isPassed &= !system.Apply(WeatherType.Maelstrom, WeatherType.Calm, 200.0, 10f);
            isPassed &= !system.Apply(WeatherType.Storm, WeatherType.Calm, 201.0, 10f);
            isPassed &= system.Apply(WeatherType.Maelstrom, WeatherType.Maelstrom, 202.0, 10f);
            system.Tick(300.0);
            isPassed &= system.Target == WeatherType.Maelstrom && Matches(in system.Snapshot, in maelstrom);

            WeatherCheckLog.Report("MaelstromLock", isPassed, "no way out of maelstrom");
            return isPassed;
        }

        private static bool CheckLateJoin()
        {
            WeatherSystem system = CreateSystem(WeatherType.Calm);
            WeatherSnapshot maelstrom = MakeMaelstrom();

            bool isPassed = system.Apply(WeatherType.Storm, WeatherType.Maelstrom, 0.0, 10f);
            system.Tick(100.0);
            isPassed &= system.Target == WeatherType.Maelstrom && !system.IsTransitioning
                && Matches(in system.Snapshot, in maelstrom);

            WeatherCheckLog.Report("LateJoin", isPassed, "a stale transition settles on the target");
            return isPassed;
        }

        private static bool CheckCanApply()
        {
            WeatherSystem system = CreateSystem(WeatherType.Calm);

            bool isPassed = system.CanApply(WeatherType.Calm, WeatherType.Storm)
                && !system.CanApply(InvalidType, WeatherType.Storm)
                && !system.CanApply(WeatherType.Calm, InvalidType);

            system.Apply(WeatherType.Calm, WeatherType.Maelstrom, 0.0, 0f);
            isPassed &= !system.CanApply(WeatherType.Maelstrom, WeatherType.Calm)
                && system.CanApply(WeatherType.Maelstrom, WeatherType.Maelstrom)
                && system.Target == WeatherType.Maelstrom;

            WeatherCheckLog.Report("CanApply", isPassed, "query matches Apply rules and does not change state");
            return isPassed;
        }

        private static bool CheckReset()
        {
            WeatherSystem system = CreateSystem(WeatherType.Calm);
            var listener = new CountingListener();
            system.AddListener(listener);
            WeatherSnapshot calm = MakeCalm();

            system.Apply(WeatherType.Calm, WeatherType.Maelstrom, Start, 0f);
            system.Tick(Start + 1.0);

            int before = listener.Count;
            system.Reset();

            bool isPassed = system.Target == WeatherType.Calm && !system.IsTransitioning
                && Matches(in system.Snapshot, in calm) && listener.Count == before + 1;
            isPassed &= system.Apply(WeatherType.Calm, WeatherType.Storm, Start, Duration);

            WeatherCheckLog.Report("Reset", isPassed, "returns to initial weather and accepts the same startTime again");
            return isPassed;
        }

        private static bool CheckZeroDuration()
        {
            WeatherSystem system = CreateSystem(WeatherType.Calm);
            WeatherSnapshot calm = MakeCalm();
            WeatherSnapshot storm = MakeStorm();

            bool isPassed = system.Apply(WeatherType.Calm, WeatherType.Storm, Start, 0f);

            system.Tick(Start - 0.1);
            isPassed &= Matches(in system.Snapshot, in calm) && system.IsTransitioning;

            system.Tick(Start);
            isPassed &= Matches(in system.Snapshot, in storm) && !system.IsTransitioning;

            WeatherCheckLog.Report("ZeroDuration", isPassed, "duration 0 jumps at startTime");
            return isPassed;
        }

        private static bool CheckListeners()
        {
            WeatherSystem system = CreateSystem(WeatherType.Calm);
            var listener = new CountingListener();

            bool isPassed = system.AddListener(listener) && !system.AddListener(listener) && !system.AddListener(null);

            system.Apply(WeatherType.Calm, WeatherType.Rain, Start, Duration);
            system.Tick(Start + 1.0);
            isPassed &= listener.Count == 1;

            isPassed &= system.RemoveListener(listener) && !system.RemoveListener(listener);
            system.Tick(Start + 2.0);
            isPassed &= listener.Count == 1;

            WeatherCheckLog.Report("Listeners", isPassed, "add, duplicate, null, remove");
            return isPassed;
        }

        private static bool CheckNoAllocation()
        {
            WeatherSystem system = CreateSystem(WeatherType.Calm);
            system.AddListener(new CountingListener());
            system.Apply(WeatherType.Calm, WeatherType.Storm, 0.0, AllocationIterations * 2f);
            system.Tick(0.5);

            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < AllocationIterations; i++)
            {
                system.Tick(i);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            bool isPassed = allocated == 0L;

            WeatherCheckLog.Report("NoAllocation", isPassed, $"{AllocationIterations} ticks allocated {allocated} B");
            return isPassed;
        }

        private static WeatherState[] BuildStates()
        {
            return new WeatherState[]
            {
                new CalmState(MakeCalm()),
                new OvercastState(MakeOvercast()),
                new RainState(MakeRain()),
                new StormState(MakeStorm()),
                new MaelstromState(MakeMaelstrom())
            };
        }

        private static WeatherSystem CreateSystem(WeatherType initial)
        {
            WeatherSystem.TryCreate(BuildStates(), initial, out WeatherSystem system);
            return system;
        }

        private sealed class CountingListener : IWeatherListener
        {
            public int Count { get; private set; }

            public void HandleWeatherChanged(in WeatherSnapshot snapshot)
            {
                Count++;
            }
        }
    }
}
