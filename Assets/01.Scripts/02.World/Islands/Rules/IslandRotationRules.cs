using System;
using UnityEngine;

namespace Lighthouse.World.Islands.Rules
{
    public static class IslandRotationRules
    {
        public static bool HasTerrain(GameObject prefab)
        {
            return prefab != null && prefab.GetComponentInChildren<Terrain>(true) != null;
        }

        public static float SelectYaw(GameObject prefab, float rolledYaw)
        {
            return HasTerrain(prefab) ? 0f : rolledYaw;
        }

        public static bool IsValidYaw(GameObject prefab, float yawDegrees)
        {
            return prefab != null && !float.IsNaN(yawDegrees) && !float.IsInfinity(yawDegrees)
                && (!HasTerrain(prefab) || yawDegrees == 0f);
        }

        public static Quaternion GetRotation(GameObject prefab, float yawDegrees)
        {
            if (!IsValidYaw(prefab, yawDegrees))
            {
                throw new ArgumentException("Island yaw must be finite; Terrain islands require yaw=0.", nameof(yawDegrees));
            }

            return Quaternion.Euler(0f, yawDegrees, 0f);
        }

        public static void ValidateParent(GameObject prefab, Transform parent)
        {
            if (!HasTerrain(prefab) || parent == null)
            {
                return;
            }

            Vector3 scale = parent.lossyScale;
            if (Quaternion.Angle(parent.rotation, Quaternion.identity) > 0.001f
                || scale.x <= 0f || scale.y <= 0f || scale.z <= 0f
                || !Mathf.Approximately(scale.x, scale.y) || !Mathf.Approximately(scale.x, scale.z))
            {
                throw new ArgumentException("Terrain island parent must be unrotated with positive uniform scale.", nameof(parent));
            }
        }
    }
}
