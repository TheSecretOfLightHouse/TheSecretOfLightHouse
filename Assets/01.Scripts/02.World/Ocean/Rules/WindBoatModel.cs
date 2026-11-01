using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class WindBoatModel
    {
        private const float MinWindSpeed = 0.0001f;
        private const float MinHeadingSqr = 0.0001f;

        public static WindBoatEffect Evaluate(Vector2 wind, Vector3 travelDirection, WindBoatProfile profile)
        {
            float windSpeed = wind.magnitude;

            if (profile == null || windSpeed < MinWindSpeed)
            {
                return WindBoatEffect.None;
            }

            Vector3 drift = new Vector3(wind.x, 0f, wind.y) * profile.DriftPerWind;
            float strength = Mathf.Clamp01(windSpeed / profile.ReferenceWind);
            float along = GetAlongTravel(wind / windSpeed, travelDirection);
            bool isTailwind = along > 0f;

            float speedGain = isTailwind ? profile.TailwindSpeedBonus : profile.HeadwindSpeedPenalty;
            float accelerationGain = isTailwind ? profile.TailwindAccelerationBonus : profile.HeadwindAccelerationPenalty;

            return new WindBoatEffect
            {
                Drift = Vector3.ClampMagnitude(drift, profile.MaxDrift),
                SpeedMultiplier = ClampMultiplier(1f + along * strength * speedGain, profile),
                AccelerationMultiplier = ClampMultiplier(1f + along * strength * accelerationGain, profile)
            };
        }

        private static float GetAlongTravel(Vector2 windDirection, Vector3 travelDirection)
        {
            Vector2 travel = new Vector2(travelDirection.x, travelDirection.z);

            if (travel.sqrMagnitude < MinHeadingSqr)
            {
                return 0f;
            }

            return Vector2.Dot(windDirection, travel.normalized);
        }

        private static float ClampMultiplier(float value, WindBoatProfile profile)
        {
            return Mathf.Clamp(value, profile.MinMultiplier, profile.MaxMultiplier);
        }
    }
}
