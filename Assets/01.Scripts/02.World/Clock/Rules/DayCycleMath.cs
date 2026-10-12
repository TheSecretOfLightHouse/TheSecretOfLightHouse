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
