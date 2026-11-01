using UnityEngine;

namespace Lighthouse.World.Environment.View
{
    public readonly struct EnvironmentWeights
    {
        public readonly float Daylight;
        public readonly float TwilightDaylight;
        public readonly float SkyDaylight;
        public readonly float Storm;
        public readonly float Abyss;
        public readonly float LightScale;

        public float Night => 1f - TwilightDaylight;

        public EnvironmentWeights(
            float daylight,
            float twilightDaylight,
            float storm,
            float abyss,
            float lightScale)
        {
            Daylight = Mathf.Clamp01(daylight);
            TwilightDaylight = Mathf.Clamp01(twilightDaylight);
            Storm = Mathf.Clamp01(storm);
            Abyss = Mathf.Clamp01(abyss);
            LightScale = Mathf.Clamp01(lightScale);
            SkyDaylight = TwilightDaylight * (1f - Abyss);
        }

        public static EnvironmentWeights Default =>
            new EnvironmentWeights(1f, 1f, 0f, 0f, 1f);
    }
}
