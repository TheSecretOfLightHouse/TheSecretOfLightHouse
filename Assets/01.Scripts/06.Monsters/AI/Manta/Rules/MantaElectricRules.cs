using UnityEngine;

namespace Lighthouse.Monsters.AI.Rules
{
    // 위치, 시전 번호, 서버 생성 시각을 Mirror에서 하나의 항목으로 동기화한다.
    public struct MantaTrailPoint
    {
        public uint CastId;
        public uint Sequence;
        public Vector3 Position;
        public double CreatedAt;

        public MantaTrailPoint(uint castId, Vector3 position, double createdAt, uint sequence = 0)
        {
            CastId = castId;
            Sequence = sequence;
            Position = position;
            CreatedAt = createdAt;
        }
    }

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

        public static Vector3 SweepPoint(Vector3 start, Vector3 end, Vector3 side, float offset, float progress)
        {
            float t = Mathf.Clamp01(progress);
            return Vector3.Lerp(start, end, t) + side * (4f * offset * t * (1f - t));
        }

        public static Vector3 SweepTangent(Vector3 start, Vector3 end, Vector3 side, float offset, float progress)
        {
            float t = Mathf.Clamp01(progress);
            return end - start + side * (4f * offset * (1f - 2f * t));
        }

        public static bool TryClipLiveSegment(MantaTrailPoint first, MantaTrailPoint second,
            double cutoff, out Vector3 start, out Vector3 end)
        {
            start = first.Position;
            end = second.Position;
            if (first.CastId != second.CastId || second.CreatedAt <= cutoff)
            {
                return false;
            }
            if (first.CreatedAt < cutoff && second.CreatedAt > first.CreatedAt)
            {
                float fraction = Mathf.Clamp01((float)((cutoff - first.CreatedAt) / (second.CreatedAt - first.CreatedAt)));
                start = Vector3.Lerp(first.Position, second.Position, fraction);
            }
            return true;
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
