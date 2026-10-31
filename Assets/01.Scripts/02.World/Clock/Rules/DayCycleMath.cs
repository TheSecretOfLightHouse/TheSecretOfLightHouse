namespace Lighthouse.World.Clock.Rules
{
    public static class DayCycleMath
    {
        public static DayCycleState Evaluate(double elapsed, float daySeconds, float nightSeconds, float warningSeconds)
        {
            double day = daySeconds;
            double cycle = day + nightSeconds;

            if (cycle <= 0.0)
            {
                return new DayCycleState(1, DayPhase.Day);
            }

            if (elapsed < 0.0)
            {
                elapsed = 0.0;
            }

            double cycleIndex = System.Math.Floor(elapsed / cycle);
            double inCycle = elapsed - cycleIndex * cycle;

            if (inCycle < 0.0)
            {
                cycleIndex -= 1.0;
                inCycle += cycle;
            }
            else if (inCycle >= cycle)
            {
                cycleIndex += 1.0;
                inCycle -= cycle;
            }

            return new DayCycleState((int)cycleIndex + 1, ResolvePhase(inCycle, day, warningSeconds));
        }

        public static bool TryGetMorningBoundary(double gameStartTime, float daySeconds, float nightSeconds,
            int completedDaySerial, out double boundaryTime)
        {
            boundaryTime = 0.0;

            if (!IsFinite(gameStartTime) || !IsValidDuration(daySeconds) || !IsValidDuration(nightSeconds)
                || completedDaySerial <= 0)
            {
                return false;
            }

            double cycle = (double)daySeconds + nightSeconds;
            double boundary = gameStartTime + completedDaySerial * cycle;

            if (!IsFinite(boundary) || boundary <= gameStartTime)
            {
                return false;
            }

            boundaryTime = boundary;
            return true;
        }

        public static bool TryGetCompletedDayCount(double gameStartTime, double serverTime,
            float daySeconds, float nightSeconds, out int completedDayCount)
        {
            completedDayCount = 0;

            if (!IsFinite(serverTime)
                || !TryGetMorningBoundary(gameStartTime, daySeconds, nightSeconds, 1, out _))
            {
                return false;
            }

            if (serverTime <= gameStartTime)
            {
                return true;
            }

            double cycle = (double)daySeconds + nightSeconds;
            double completed = System.Math.Floor((serverTime - gameStartTime) / cycle);

            if (!IsFinite(completed) || completed > int.MaxValue - 1)
            {
                return false;
            }

            // DayIndex is the completed-day count plus one and must remain representable.
            completedDayCount = (int)completed;
            return true;
        }

        private static bool IsValidDuration(float seconds)
        {
            return !float.IsNaN(seconds) && !float.IsInfinity(seconds) && seconds > 0f;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static DayPhase ResolvePhase(double inCycle, double day, double warningSeconds)
        {
            if (inCycle >= day)
            {
                return DayPhase.Night;
            }

            if (inCycle >= day - warningSeconds)
            {
                return DayPhase.Dusk;
            }

            return DayPhase.Day;
        }
    }
}
