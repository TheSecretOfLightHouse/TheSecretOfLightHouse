using Lighthouse.World.Query.Rules;
using Lighthouse.World.Weather.Net;
using Lighthouse.World.Weather.Rules;

namespace Lighthouse.World.Weather.Sandbox
{
    public static class WeatherBinderChecks
    {
        private const double StartA = 10.0;
        private const double StartB = 20.0;
        private const float Duration = 20f;

        public static bool RunAll()
        {
            if (WorldServices.TryGet(out WeatherSystem _))
            {
                UnityEngine.Debug.LogWarning($"[{nameof(WeatherBinderChecks)}] SKIPPED: a WeatherSystem service is already registered. Run in Edit mode.");
                return true;
            }

            if (!WeatherSystem.TryCreate(WeatherSystemChecks.BuildStates(), WeatherType.Calm, out WeatherSystem system))
            {
                WeatherCheckLog.Report("Binder", false, "WeatherSystem could not be created");
                return false;
            }

            WorldServices.Register(system);

            try
            {
                bool isPassed = CheckDuplicate();
                isPassed &= CheckNotReady();
                isPassed &= CheckReset();
                return isPassed;
            }
            finally
            {
                WorldServices.Unregister(system);
            }
        }

        private static WeatherTransitionInfo Make(WeatherType from, WeatherType to, double startTime)
        {
            return new WeatherTransitionInfo
            {
                From = from,
                To = to,
                StartTime = startTime,
                Duration = Duration,
                IsSet = true
            };
        }

        private static bool CheckDuplicate()
        {
            int applied = 0;
            WeatherApplyBinder binder = new WeatherApplyBinder(() => true);
            binder.TransitionApplied += (info, isApplied) => applied++;
            WeatherTransitionInfo first = Make(WeatherType.Calm, WeatherType.Storm, StartA);

            binder.Submit(first);
            binder.Submit(first);
            bool isOk = applied == 1;
            binder.Submit(default);
            isOk &= applied == 1;
            binder.Submit(Make(WeatherType.Storm, WeatherType.Calm, StartB));
            isOk &= applied == 2;

            WeatherCheckLog.Report("BinderDuplicate", isOk, "same info twice applies once, unset ignored, new startTime applies");
            return isOk;
        }

        private static bool CheckNotReady()
        {
            int applied = 0;
            bool isReady = false;
            WeatherApplyBinder binder = new WeatherApplyBinder(() => isReady);
            binder.TransitionApplied += (info, isApplied) => applied++;
            WeatherTransitionInfo info = Make(WeatherType.Calm, WeatherType.Storm, StartA);

            binder.Submit(info);
            binder.Submit(info);
            binder.Tick();
            bool isOk = applied == 0;
            isReady = true;
            binder.Tick();
            isOk &= applied == 1;
            binder.Tick();
            isOk &= applied == 1;

            WeatherCheckLog.Report("BinderNotReady", isOk, "held until ready, applied once");
            return isOk;
        }

        private static bool CheckReset()
        {
            int applied = 0;
            WeatherApplyBinder binder = new WeatherApplyBinder(() => true);
            binder.TransitionApplied += (info, isApplied) => applied++;
            WeatherTransitionInfo info = Make(WeatherType.Calm, WeatherType.Storm, StartA);

            binder.Submit(info);
            binder.Reset();
            binder.Submit(info);
            bool isOk = applied == 2;

            WeatherCheckLog.Report("BinderReset", isOk, "same info applies again after Reset");
            return isOk;
        }
    }
}
