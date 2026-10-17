using UnityEngine;

namespace Lighthouse.Monsters.Spawning.Server
{
    internal static class MonsterSpawnPlacement
    {
        private static readonly Collider[] Overlaps = new Collider[128];

        internal static bool IsClear(CapsuleCollider capsule, Vector3 prefabScale, Vector3 position, Quaternion rotation)
        {
            Vector3 scale = prefabScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

            Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;

            float axisScale = capsule.direction == 0 ? scale.x : capsule.direction == 1 ? scale.y : scale.z;
            float radiusScale = capsule.direction == 0 ? Mathf.Max(scale.y, scale.z) 
                : capsule.direction == 1 ? Mathf.Max(scale.x, scale.z) : Mathf.Max(scale.x, scale.y);

            float radius = capsule.radius * radiusScale;
            float halfSegment = Mathf.Max(0f, capsule.height * axisScale * 0.5f - radius);

            Vector3 center = position + rotation * Vector3.Scale(capsule.center, prefabScale);
            Vector3 offset = rotation * axis * halfSegment;

            int count = Physics.OverlapCapsuleNonAlloc(center - offset, center + offset, radius, Overlaps, ~0, QueryTriggerInteraction.Collide);

            // 버퍼가 가득 차면 겹침 여부를 확정할 수 없으므로 생성하지 않고 재시도한다.
            bool isClear = count < Overlaps.Length;

            for (int i = 0; i < count; i++)
            {
                Collider overlap = Overlaps[i];

                if (MonsterSpawnCollisionRegistry.IsMonster(overlap))
                {
                    isClear = false;
                }

                Overlaps[i] = null;
            }

            return isClear;
        }
    }
}
