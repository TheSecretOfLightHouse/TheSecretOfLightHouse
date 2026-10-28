using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class OceanOpticsModel
    {
        private const float HalfRotation = 180f;
        private const float MinHorizonBlend = 0.01f;

        public static float GetDaylight(
            double elapsed,
            float daySeconds,
            float nightSeconds,
            float horizonBlend)
        {
            if (daySeconds <= 0f || nightSeconds <= 0f)
            {
                return 0f;
            }

            double inCycle =
                Math.Max(0.0, elapsed) % (daySeconds + nightSeconds);

            float angle;

            if (inCycle < daySeconds)
            {
                angle = (float)(inCycle / daySeconds) * HalfRotation;
            }
            else
            {
                angle = HalfRotation +
                    (float)((inCycle - daySeconds) / nightSeconds) *
                    HalfRotation;
            }

            float elevation = Mathf.Sin(angle * Mathf.Deg2Rad);
            float blend = Mathf.Max(MinHorizonBlend, horizonBlend);

            return Mathf.SmoothStep(
                0f, 1f, Mathf.Clamp01(elevation / blend));
        }

        public static OceanOpticsState Evaluate(
            OceanOpticsProfile profile,
            float daylight,
            float stormWeight,
            float abyssWeight)
        {
            OceanOpticsState daytime = OceanOpticsState.Lerp(
                profile.Day, profile.Storm, stormWeight);

            OceanOpticsState regular = OceanOpticsState.Lerp(
                profile.Night, daytime, daylight);

            return OceanOpticsState.Lerp(
                regular, profile.Abyss, abyssWeight);
        }
    }
}
