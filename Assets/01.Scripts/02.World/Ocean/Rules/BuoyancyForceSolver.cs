using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class BuoyancyForceSolver
    {
        public static bool TryApply(
            Rigidbody body,
            float mass,
            float gravity,
            BuoyancyTuning tuning,
            ReadOnlySpan<Vector3> worldPoints,
            ReadOnlySpan<float> waterHeights,
            out float meanDepth,
            out float submergedFraction)
        {
            meanDepth = 0f;
            submergedFraction = 0f;

            if (body == null
                || body.isKinematic
                || tuning == null
                || worldPoints.Length == 0
                || worldPoints.Length != waterHeights.Length
                || !(mass > 0f)
                || float.IsInfinity(mass)
                || !(gravity >= 0f)
                || float.IsInfinity(gravity))
            {
                return false;
            }

            float perPointMass = mass / worldPoints.Length;
            float depthSum = 0f;
            int submergedCount = 0;

            for (int i = 0; i < worldPoints.Length; i++)
            {
                Vector3 point = worldPoints[i];
                float depth = waterHeights[i] - point.y;

                if (depth <= 0f)
                {
                    continue;
                }

                float submerged = Mathf.Min(depth, tuning.MaxDepth);

                Vector3 force =
                    Vector3.up
                    * tuning.GetUpwardForce(
                        submerged,
                        perPointMass,
                        gravity)
                    + tuning.GetDragForce(
                        body.GetPointVelocity(point),
                        perPointMass);

                body.AddForceAtPosition(force, point);

                depthSum += submerged;
                submergedCount++;
            }

            meanDepth = depthSum / worldPoints.Length;
            submergedFraction =
                submergedCount / (float)worldPoints.Length;

            return true;
        }
    }
}
