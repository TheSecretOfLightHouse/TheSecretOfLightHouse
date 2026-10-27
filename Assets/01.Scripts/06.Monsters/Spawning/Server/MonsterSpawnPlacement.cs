using Lighthouse.Monsters.Spawning.Rules;
using UnityEngine;

namespace Lighthouse.Monsters.Spawning.Server
{
    internal static class MonsterSpawnPlacement
    {
        private static readonly Collider[] Overlaps = new Collider[128];

        internal static bool IsClear(MonsterSpawnShape shape, Vector3 position, Quaternion rotation)
        {
            Vector3 center = position + rotation * shape.Center;
            int count;
            if (shape.IsBox)
            {
                count = Physics.OverlapBoxNonAlloc(center, shape.HalfExtents, Overlaps,
                    rotation, ~0, QueryTriggerInteraction.Collide);
            }
            else
            {
                Vector3 offset = rotation * shape.CapsuleOffset;
                count = Physics.OverlapCapsuleNonAlloc(center - offset, center + offset, shape.Radius,
                    Overlaps, ~0, QueryTriggerInteraction.Collide);
            }

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
