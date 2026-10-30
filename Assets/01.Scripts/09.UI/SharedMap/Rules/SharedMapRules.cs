using UnityEngine;

namespace Lighthouse.UI.SharedMap.Rules
{
    public static class SharedMapRules
    {
        public const int RequiredPlayers = 4;
        // Raster painting keeps the canvas mesh constant; bound the replay log separately.
        public const int MaxOperations = 48000;
        public const int ChunkSize = 96;
        public const int MaxBatchSize = 16;

        public static bool IsValidPoint(Vector2 point)
        {
            return !float.IsNaN(point.x) && !float.IsNaN(point.y)
                && !float.IsInfinity(point.x) && !float.IsInfinity(point.y)
                && point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f;
        }

        public static bool CanStart(bool isEvening, int ready, int players, int day, int editedDay)
        {
            return isEvening && ready == RequiredPlayers && players == RequiredPlayers && day != editedDay;
        }

        public static bool CanEdit(bool isEvening, bool isEditing, double now, double deadline)
        {
            return isEvening && isEditing && now < deadline;
        }

        public static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            float t = delta.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - from, delta) / delta.sqrMagnitude) : 0f;
            return Vector2.Distance(point, from + delta * t);
        }
    }
}
