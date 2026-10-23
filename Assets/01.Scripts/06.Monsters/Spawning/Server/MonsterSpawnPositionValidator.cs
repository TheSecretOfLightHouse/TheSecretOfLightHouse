using UnityEngine;

namespace Lighthouse.Monsters.Spawning.Server
{
    public class MonsterSpawnPositionValidator : MonoBehaviour
    {
        // 월드 연결 시 이곳에서 지형·안전지대를 검사하고 수면 높이를 반영한다.
        public virtual bool TryResolvePosition(Vector3 candidate, out Vector3 position)
        {
            position = candidate;
            return true;
        }

        public virtual bool TryResolvePosition(Vector3 origin, Vector3 candidate, out Vector3 position)
        {
            return TryResolvePosition(candidate, out position);
        }
    }
}
