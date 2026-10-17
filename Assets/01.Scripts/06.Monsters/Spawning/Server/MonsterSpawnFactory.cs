using Lighthouse.Monsters.AI.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.Spawning.Server
{
    internal static class MonsterSpawnFactory
    {
        internal static MonsterBase Spawn(MonsterBase prefab, Vector3 position, Quaternion rotation)
        {
            MonsterBase monster = Object.Instantiate(prefab, position, rotation);

            try
            {
                NetworkServer.Spawn(monster.gameObject);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, monster);
                Despawn(monster);
                return null;
            }

            if (monster == null || !monster.IsInitialized || monster.netId == 0)
            {
                Despawn(monster);

                return null;
            }

            // 자동 동기화가 꺼져 있어도 같은 프레임의 다음 검사에 새 콜라이더가 반영되게 한다.
            Physics.SyncTransforms();

            return monster;
        }

        internal static void Despawn(MonsterBase monster)
        {
            if (monster != null)
            {
                NetworkServer.Destroy(monster.gameObject);
            }
        }
    }
}
