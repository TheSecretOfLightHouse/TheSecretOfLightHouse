using System;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Clock.View;
using UnityEngine;

namespace Lighthouse.World.Environment.View
{
    public static class EnvironmentClockModel
    {
        public static float GetDaylight(
            double elapsed,
            DayCycleConfig cycle,
            DayNightSunConfig sun)
        {
            if (cycle == null || sun == null ||
                cycle.DaySeconds <= 0f || cycle.NightSeconds <= 0f)
            {
                return 0f;
            }

            double inCycle = Math.Max(0d, elapsed) % cycle.CycleSeconds;
            float angle;

            if (inCycle < cycle.DaySeconds)
            {
                angle = (float)(inCycle / cycle.DaySeconds) * 180f;
            }
            else
            {
                angle = 180f +
                    (float)((inCycle - cycle.DaySeconds) /
                    cycle.NightSeconds) * 180f;
            }

            float elevation = Mathf.Sin(angle * Mathf.Deg2Rad);
            float twilight = Mathf.Sin(
                sun.SkyTwilightElevation * Mathf.Deg2Rad);

            return Mathf.SmoothStep(
                0f, 1f,
                Mathf.InverseLerp(-twilight, twilight, elevation));
        }
    }
}
