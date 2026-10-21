using Lighthouse.Monsters.AI.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [DisallowMultipleComponent]
    public sealed class MonsterTestNetworkManager : NetworkManager
    {
        public override void OnServerReady(NetworkConnectionToClient connection)
        {
            base.OnServerReady(connection);
            if (connection.identity != null)
            {
                return;
            }

            // 플레이어 없는 테스트 연결에도 씬 몬스터의 생성·초기화 메시지를 전달한다.
            connection.Send(new ObjectSpawnStartedMessage());
            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (identity == null || !identity.gameObject.activeSelf || identity.serverOnly
                    || identity.GetComponent<MonsterBase>() == null)
                {
                    continue;
                }

                // 기존 가시성 규칙을 유지하면서 Ready 연결을 관찰자로 등록한다.
                NetworkServer.RebuildObservers(identity, true);
            }
            connection.Send(new ObjectSpawnFinishedMessage());
        }
    }
}
