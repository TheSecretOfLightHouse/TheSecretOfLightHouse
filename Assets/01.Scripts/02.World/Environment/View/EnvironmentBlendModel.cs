using UnityEngine;

namespace Lighthouse.World.Environment.View
{
    public static class EnvironmentBlendModel
    {
        public static SkyEnvironmentState Evaluate(
            in SkyEnvironmentState fromDay,
            in SkyEnvironmentState fromNight,
            in SkyEnvironmentState toDay,
            in SkyEnvironmentState toNight,
            float regionBlend,
            float daylight,
            in WeatherEnvironmentState weather,
            in SkyEnvironmentState abyssSky)
        {
            SkyEnvironmentState day = SkyEnvironmentState.Lerp(
                in fromDay, in toDay, regionBlend);

            SkyEnvironmentState night = SkyEnvironmentState.Lerp(
                in fromNight, in toNight, regionBlend);

            SkyEnvironmentState region = SkyEnvironmentState.Lerp(
                in night, in day, daylight);

            float clouds = 1f -
                (1f - region.CloudCoverage) *
                (1f - weather.CloudCoverage);

            SkyEnvironmentState normal = new SkyEnvironmentState(
                Multiply(region.Zenith, weather.Tint),
                Multiply(region.Horizon, weather.Tint),
                Multiply(region.NebulaColor, weather.Tint),
                region.StarStrength * weather.StarVisibility,
                region.NebulaStrength * weather.NebulaVisibility,
                region.LightScale * weather.LightScale,
                clouds);

            return SkyEnvironmentState.Lerp(
                in normal, in abyssSky, weather.AbyssWeight);
        }

        private static Color Multiply(Color color, Color tint)
        {
            return new Color(
                color.r * tint.r,
                color.g * tint.g,
                color.b * tint.b,
                1f);
        }
    }
}
