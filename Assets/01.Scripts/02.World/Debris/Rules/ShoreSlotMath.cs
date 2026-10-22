using UnityEngine;

namespace Lighthouse.World.Debris.Rules
{
    public static class ShoreSlotMath
    {
        private const float MinDirectionSqr = 1e-6f;

        public static Vector2 GetOutward(Vector2 center, Vector2 origin)
        {
            Vector2 delta = origin - center;
            return delta.sqrMagnitude < MinDirectionSqr ? Vector2.right : delta.normalized;
        }

        public static float GetTangentOffset(int slot, float spacing)
        {
            if (slot <= 0)
            {
                return 0f;
            }

            int rank = (slot + 1) / 2;
            float sign = (slot & 1) == 1 ? 1f : -1f;
            return rank * spacing * sign;
        }

        public static Vector2 GetPoint(
            Vector2 center,
            Vector2 outward,
            float shoreDistance,
            float inset,
            int slot,
            float spacing)
        {
            float distance = Mathf.Max(0f, shoreDistance - inset);
            Vector2 tangent = new Vector2(-outward.y, outward.x);
            return center + outward * distance + tangent * GetTangentOffset(slot, spacing);
        }
    }
}
