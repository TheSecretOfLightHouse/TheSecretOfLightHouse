using Lighthouse.Monsters.AI.Net;
using UnityEngine;

namespace Lighthouse.Monsters.Spawning.Rules
{
    public readonly struct MonsterSpawnShape
    {
        public Collider Collider { get; }
        public bool IsBox { get; }
        public Vector3 Center { get; }
        public Vector3 HalfExtents { get; }
        public Vector3 CapsuleOffset { get; }
        public float Radius { get; }

        private MonsterSpawnShape(Collider collider, bool isBox, Vector3 center,
            Vector3 halfExtents, Vector3 capsuleOffset, float radius)
        {
            Collider = collider;
            IsBox = isBox;
            Center = center;
            HalfExtents = halfExtents;
            CapsuleOffset = capsuleOffset;
            Radius = radius;
        }

        public static bool TryCreate(MonsterBase prefab, out MonsterSpawnShape shape)
        {
            shape = default;
            if (prefab == null || !prefab.gameObject.activeSelf)
            {
                return false;
            }
            Vector3 scale = prefab.transform.lossyScale;
            if (!MonsterSpawnerData.IsFinite(scale) || scale.x == 0f || scale.y == 0f || scale.z == 0f)
            {
                return false;
            }
            Collider selected = null;
            foreach (Collider collider in prefab.GetComponents<Collider>())
            {
                if (!collider.enabled)
                {
                    continue;
                }
                // 복합 몸체의 일부만 검사하면 겹친 생성이 가능하므로 단일 루트 형태만 받는다.
                if (selected != null || (!(collider is BoxCollider) && !(collider is CapsuleCollider)))
                {
                    return false;
                }
                selected = collider;
            }
            Vector3 absoluteScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            if (selected is BoxCollider box)
            {
                Vector3 center = Vector3.Scale(box.center, scale);
                Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, absoluteScale);
                if (!MonsterSpawnerData.IsFinite(center) || !MonsterSpawnerData.IsFinite(halfExtents)
                    || halfExtents.x <= 0f || halfExtents.y <= 0f || halfExtents.z <= 0f)
                {
                    return false;
                }
                shape = new MonsterSpawnShape(box, true, center, halfExtents, Vector3.zero, 0f);
                return true;
            }
            if (selected is CapsuleCollider capsule)
            {
                Vector3 center = Vector3.Scale(capsule.center, scale);
                Vector3 axis = capsule.direction == 0 ? Vector3.right
                    : capsule.direction == 1 ? Vector3.up : Vector3.forward;
                float axisScale = capsule.direction == 0 ? absoluteScale.x
                    : capsule.direction == 1 ? absoluteScale.y : absoluteScale.z;
                float radiusScale = capsule.direction == 0 ? Mathf.Max(absoluteScale.y, absoluteScale.z)
                    : capsule.direction == 1 ? Mathf.Max(absoluteScale.x, absoluteScale.z)
                    : Mathf.Max(absoluteScale.x, absoluteScale.y);
                float radius = capsule.radius * radiusScale;
                float halfHeight = capsule.height * 0.5f * axisScale;
                if (!MonsterSpawnerData.IsFinite(center) || !IsPositiveFinite(radius) || !IsPositiveFinite(halfHeight))
                {
                    return false;
                }
                shape = new MonsterSpawnShape(capsule, false, center, Vector3.zero,
                    axis * Mathf.Max(0f, halfHeight - radius), radius);
                return true;
            }
            return false;
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
