using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class BuoyancySolver
    {
        public const float NoTargetDepth = -1f;

        private const float HalfFactor = 0.5f;

        public static bool TryEvaluate(
            BuoyancyProfile profile,
            float mass,
            float gravity,
            float bodyHeight,
            float thickness,
            float targetDepth,
            ReadOnlySpan<Vector3> worldPoints,
            ReadOnlySpan<float> waterHeights,
            ReadOnlySpan<Vector3> pointVelocities,
            Span<Vector3> forces,
            out BuoyancyReading reading)
        {
            reading = default;

            int count = worldPoints.Length;

            if (profile == null
                || count == 0
                || waterHeights.Length != count
                || pointVelocities.Length != count
                || forces.Length < count
                || !(mass > 0f)
                || float.IsInfinity(mass)
                || !(gravity >= 0f)
                || float.IsInfinity(gravity)
                || !(thickness > 0f)
                || float.IsInfinity(thickness)
                || !(bodyHeight >= 0f))
            {
                return false;
            }

            bool hasTarget = targetDepth > 0f;
            float ratio = profile.ResolveRatio(hasTarget);
            float follow = profile.ResolveFollow(hasTarget);
            float restore = profile.ResolveRestoreStrength(hasTarget, targetDepth, thickness);
            float depthCap = profile.ResolveDepthCap(bodyHeight, thickness);
            float half = thickness * HalfFactor;
            float share = mass * gravity / count;
            float perPointMass = mass / count;

            float meanSurface = 0f;

            for (int i = 0; i < count; i++)
            {
                meanSurface += waterHeights[i];
            }

            meanSurface /= count;

            float fractionSum = 0f;
            float depthSum = 0f;

            for (int i = 0; i < count; i++)
            {
                float y = worldPoints[i].y;
                float surface = meanSurface + follow * (waterHeights[i] - meanSurface);
                float submerged = Mathf.Clamp01((surface - y + half) / thickness);
                float beyond = Mathf.Min(Mathf.Max(0f, surface - y - half), depthCap);

                Vector3 velocity = pointVelocities[i];

                forces[i] = new Vector3(
                    -velocity.x * profile.HorizontalDrag * perPointMass * submerged,
                    share * (ratio * submerged + restore * beyond)
                        - velocity.y * profile.VerticalDrag * perPointMass * submerged,
                    -velocity.z * profile.HorizontalDrag * perPointMass * submerged);

                fractionSum += submerged;
                depthSum += Mathf.Clamp(waterHeights[i] - y, 0f, depthCap);
            }

            reading = new BuoyancyReading(
                meanSurface,
                fractionSum / count,
                depthSum / count);

            return true;
        }
    }
}
