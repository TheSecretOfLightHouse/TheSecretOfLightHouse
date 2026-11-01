using UnityEngine;

namespace Lighthouse.World.Weather.Rules
{
    public static class WaveResponse
    {
        private const float MinRate = 0.1f;
        private const float MaxRate = 10f;

        public static float Normalize(float rate)
        {
            return rate > 0f ? Mathf.Clamp(rate, MinRate, MaxRate) : 1f;
        }

        // Rates above 1 finish early (wind sea), below 1 finish late (swell); the curve still maps 0 to 0 and 1 to 1.
        public static float Shape(float progress, float rate)
        {
            float t = Mathf.Clamp01(progress);
            float r = Normalize(rate);
            if (Mathf.Approximately(r, 1f))
            {
                return t;
            }

            return r > 1f ? 1f - Mathf.Pow(1f - t, r) : Mathf.Pow(t, 1f / r);
        }
    }
}
