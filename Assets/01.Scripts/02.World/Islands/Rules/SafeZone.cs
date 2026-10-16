using System;
using UnityEngine;

namespace Lighthouse.World.Islands.Rules
{
    public sealed class SafeZone
    {
        private readonly Vector2 _center;
        private readonly double _radiusSquared;

        public SafeZone(Vector2 center, float radius)
        {
            if (!IsFinite(center.x) || !IsFinite(center.y)
                || !IsFinite(radius) || radius < 0f)
            {
                throw new ArgumentException("Invalid safe zone dimensions.");
            }

            _center = center;
            _radiusSquared = (double)radius * radius;
        }

        public bool Contains(Vector3 position)
        {
            if (!IsFinite(position.x) || !IsFinite(position.y)
                || !IsFinite(position.z))
            {
                return false;
            }

            double x = (double)position.x - _center.x;
            double z = (double)position.z - _center.y;
            return x * x + z * z <= _radiusSquared;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
