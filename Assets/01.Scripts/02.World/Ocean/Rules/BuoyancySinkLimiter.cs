using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class BuoyancySinkLimiter
    {
        private const float MaxDescentSpeed = 8f;

        public static bool TryLimit(
            Rigidbody body,
            BuoyancyProfile profile,
            ReadOnlySpan<Vector3> worldPoints,
            float surface,
            float submergedFraction)
        {
            if (body == null
                || profile == null
                || !profile.HasSinkLimit
                || worldPoints.Length == 0)
            {
                return false;
            }

            float lowest = worldPoints[0].y;

            for (int i = 1; i < worldPoints.Length; i++)
            {
                lowest = Mathf.Min(lowest, worldPoints[i].y);
            }

            Vector3 velocity = body.linearVelocity;
            float lift = surface - profile.MaxSinkDepth - lowest;

            if (lift > 0f)
            {
                Vector3 position = body.position;
                position.y += lift;
                body.position = position;

                velocity.y = Mathf.Max(velocity.y, 0f);
                body.linearVelocity = velocity;

                return true;
            }

            if (!profile.HasFloatBand || !(submergedFraction > 0f))
            {
                return false;
            }

            float excess = lowest - (surface - profile.MinSinkDepth);

            if (excess <= 0f)
            {
                return false;
            }

            float descent = Mathf.Min(excess / Time.fixedDeltaTime, MaxDescentSpeed);
            velocity.y = Mathf.Min(velocity.y, -descent);
            body.linearVelocity = velocity;

            return true;
        }
    }
}
