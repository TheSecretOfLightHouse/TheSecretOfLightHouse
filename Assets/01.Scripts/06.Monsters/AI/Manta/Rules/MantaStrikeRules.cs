using UnityEngine;

namespace Lighthouse.Monsters.AI.Rules
{
    public static class MantaStrikeRules
    {
        public static bool TryGetCrossingOffset(Vector3 previousRelative, Vector3 currentRelative, out Vector3 offset)
        {
            offset = Vector3.zero;
            if (!Finite(previousRelative) || !Finite(currentRelative)
                || previousRelative.y > 0f || currentRelative.y < 0f
                || currentRelative.y <= previousRelative.y)
            {
                return false;
            }
            // 물리 갱신 사이에 배가 이동한 거리도 상대 이동에 반영한다.
            float fraction = -previousRelative.y / (currentRelative.y - previousRelative.y);
            offset = previousRelative + (currentRelative - previousRelative) * fraction;
            offset.y = 0f;
            return Finite(offset);
        }

        private static bool Finite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
