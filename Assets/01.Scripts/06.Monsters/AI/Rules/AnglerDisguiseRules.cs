using UnityEngine;

namespace Lighthouse.Monsters.AI.Rules
{
    public static class AnglerDisguiseRules
    {
        public static bool ShouldReveal(Vector3 monster, Vector3 observer, float distance)
        {
            if (!Finite(monster) || !Finite(observer) || float.IsNaN(distance)
                || float.IsInfinity(distance) || distance <= 0f)
            {
                return false;
            }
            Vector3 offset = observer - monster;
            offset.y = 0f;
            return offset.sqrMagnitude <= distance * distance;
        }

        private static bool Finite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
