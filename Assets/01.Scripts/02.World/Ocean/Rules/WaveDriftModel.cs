using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class WaveDriftModel
    {
        private const float MinNormalY = 0.05f;

        public static Vector3 ComputeTarget(
            WaveDriftProfile profile,
            Vector3 normal,
            Vector3 force,
            float mass,
            float shoreDistance)
        {
            if (profile == null || mass <= 0f)
            {
                return Vector3.zero;
            }

            float forceScale = 1f / (mass * profile.DriftDamping);
            float x = force.x * forceScale;
            float z = force.z * forceScale;

            if (normal.y > MinNormalY)
            {
                float slopeScale = profile.SlopeResponse / normal.y;
                x += normal.x * slopeScale;
                z += normal.z * slopeScale;
            }

            Vector3 target = new Vector3(x, 0f, z);
            float speed = target.magnitude;
            if (!float.IsFinite(speed))
            {
                return Vector3.zero;
            }

            if (speed > profile.MaxDriftSpeed)
            {
                target *= profile.MaxDriftSpeed / speed;
            }

            return target * ShoreFade(profile, shoreDistance);
        }

        public static Vector3 Advance(WaveDriftProfile profile, Vector3 current, Vector3 target, float deltaTime)
        {
            current.y = 0f;
            if (profile == null || !(deltaTime > 0f))
            {
                return current;
            }

            float blend = 1f - Mathf.Exp(-deltaTime / profile.ResponseTime);
            Vector3 next = current + (target - current) * blend;
            next.y = 0f;

            float speed = next.magnitude;
            if (speed > profile.MaxDriftSpeed)
            {
                next *= profile.MaxDriftSpeed / speed;
            }

            return next;
        }

        public static float ShoreFade(WaveDriftProfile profile, float shoreDistance)
        {
            float t = Mathf.InverseLerp(profile.ShoreFadeStart, profile.ShoreFadeEnd, shoreDistance);
            return Mathf.SmoothStep(0f, 1f, t);
        }
    }
}
