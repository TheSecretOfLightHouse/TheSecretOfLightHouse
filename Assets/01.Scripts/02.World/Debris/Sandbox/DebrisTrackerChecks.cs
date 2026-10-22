using Lighthouse.World.Clock.Sandbox;
using Lighthouse.World.Debris.Rules;
using Lighthouse.World.Debris.Server;
using UnityEngine;

namespace Lighthouse.World.Debris.Sandbox
{
    public static class DebrisTrackerChecks
    {
        private const double LifetimeSeconds = 120.0;

        public static bool RunAll()
        {
            bool isPassed = RunExpiry();
            isPassed &= RunStaleGeneration();
            isPassed &= RunReleased();
            isPassed &= RunDisabled();
            isPassed &= RunDispose();
            return isPassed;
        }

        private static bool RunExpiry()
        {
            DebrisConfig config = NewConfig(true);
            var clock = new ManualClock();
            var tracker = new DebrisLifetimeTracker(clock, config);
            var drop = new FakeExpirableDrop(7UL);

            bool isPassed = tracker.Register(drop, DebrisKind.Resource) && tracker.Count == 1;

            clock.Now = LifetimeSeconds - 1.0;
            tracker.Tick();
            isPassed &= drop.ExpireCount == 0 && tracker.Count == 1;

            clock.Now = LifetimeSeconds;
            tracker.Tick();
            isPassed &= drop.ExpireCount == 1 && tracker.Count == 0;

            Object.DestroyImmediate(config);
            Report("Expiry", isPassed, "before/at lifetime, reentrant release");
            return isPassed;
        }

        private static bool RunStaleGeneration()
        {
            DebrisConfig config = NewConfig(true);
            var clock = new ManualClock();
            var tracker = new DebrisLifetimeTracker(clock, config);
            var drop = new FakeExpirableDrop(1UL);

            tracker.Register(drop, DebrisKind.Resource);
            drop.Generation = 2UL;
            clock.Now = LifetimeSeconds;
            tracker.Tick();

            bool isPassed = drop.ExpireCount == 0 && tracker.Count == 0;

            Object.DestroyImmediate(config);
            Report("StaleGeneration", isPassed, "reused drop is not expired");
            return isPassed;
        }

        private static bool RunReleased()
        {
            DebrisConfig config = NewConfig(true);
            var clock = new ManualClock();
            var tracker = new DebrisLifetimeTracker(clock, config);
            var drop = new FakeExpirableDrop(1UL);

            tracker.Register(drop, DebrisKind.Resource);
            drop.Release();
            clock.Now = LifetimeSeconds * 2.0;
            tracker.Tick();

            bool isPassed = tracker.Count == 0 && drop.ExpireCount == 0;

            Object.DestroyImmediate(config);
            Report("Released", isPassed, "collected drop leaves the list");
            return isPassed;
        }

        private static bool RunDisabled()
        {
            DebrisConfig config = NewConfig(false);
            var tracker = new DebrisLifetimeTracker(new ManualClock(), config);
            var drop = new FakeExpirableDrop(1UL);

            bool isPassed = tracker.Register(drop, DebrisKind.Resource) && tracker.Count == 0;

            Object.DestroyImmediate(config);
            Report("Disabled", isPassed, "expiry off keeps drop untracked");
            return isPassed;
        }

        private static bool RunDispose()
        {
            DebrisConfig config = NewConfig(true);
            var tracker = new DebrisLifetimeTracker(new ManualClock(), config);
            var drop = new FakeExpirableDrop(1UL);

            tracker.Register(drop, DebrisKind.Resource);
            tracker.Dispose();
            drop.Release();

            bool isPassed = tracker.Count == 0;

            Object.DestroyImmediate(config);
            Report("Dispose", isPassed, "subscriptions removed");
            return isPassed;
        }

        private static DebrisConfig NewConfig(bool isResourceExpiryEnabled)
        {
            DebrisConfig config = ScriptableObject.CreateInstance<DebrisConfig>();
            string json = isResourceExpiryEnabled
                ? "{\"_isResourceExpiryEnabled\":true}"
                : "{\"_isResourceExpiryEnabled\":false}";
            JsonUtility.FromJsonOverwrite(json, config);
            return config;
        }

        private static void Report(string name, bool isPassed, string detail)
        {
            Debug.Log($"[{nameof(DebrisTrackerChecks)}] {name} {(isPassed ? "PASS" : "FAIL")} {detail}");
        }
    }
}
