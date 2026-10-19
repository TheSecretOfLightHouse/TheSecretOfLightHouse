using UnityEngine;

namespace Lighthouse.Monsters.AI.Rules
{
    public static class MantaElectricRules
    {
        public static Vector3 PredictCenter(Vector3 position, Vector3 velocity, Vector3 forward,
            float predictionSeconds, float minimumLead)
        {
            velocity.y = 0f;
            forward.y = 0f;
            Vector3 direction = velocity.sqrMagnitude > 0.01f ? velocity.normalized : forward.normalized;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = Vector3.forward;
            }
            return position + direction * Mathf.Max(minimumLead, velocity.magnitude * predictionSeconds);
        }

        public static Vector3 LinePoint(Vector3 center, Vector3 direction, float length,
            float zigzag, int index, int count)
        {
            direction.y = 0f;
            direction.Normalize();
            Vector3 along = Vector3.Cross(Vector3.up, direction);
            float fraction = (float)index / (count - 1);
            float bend = index == 0 || index == count - 1 ? 0f
                : Mathf.Sin(index * 2.399963f) * zigzag;
            return center + along * ((fraction - 0.5f) * length) + direction * bend;
        }

        public static bool Intersects(Vector3 previous, Vector3 current, Vector3 start, Vector3 end, float radius)
        {
            previous.y = current.y = start.y = end.y = 0f;
            Vector3 movement = current - previous;
            Vector3 line = end - start;
            Vector3 offset = start - previous;
            float cross = Cross(movement, line);
            if (Mathf.Abs(cross) > 0.000001f)
            {
                float first = Cross(offset, line) / cross;
                float second = Cross(offset, movement) / cross;
                if (first >= 0f && first <= 1f && second >= 0f && second <= 1f)
                {
                    return true;
                }
            }
            float squared = radius * radius;
            return DistanceSquared(previous, start, end) <= squared
                || DistanceSquared(current, start, end) <= squared
                || DistanceSquared(start, previous, current) <= squared
                || DistanceSquared(end, previous, current) <= squared;
        }

        private static float DistanceSquared(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector3 delta = end - start;
            float fraction = delta.sqrMagnitude > 0f
                ? Mathf.Clamp01(Vector3.Dot(point - start, delta) / delta.sqrMagnitude) : 0f;
            return (point - start - delta * fraction).sqrMagnitude;
        }

        private static float Cross(Vector3 first, Vector3 second)
        {
            return first.x * second.z - first.z * second.x;
        }
    }
}
