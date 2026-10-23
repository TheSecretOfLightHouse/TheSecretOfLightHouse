using System.Collections;
using Lighthouse.Mafia.SeaSummoning.Net;
using Lighthouse.Mafia.SeaSummoning.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Mafia.SeaSummoning.Testing
{
    public sealed class SeaSummonTestNetworkManager : NetworkManager
    {
        [SerializeField] private SeaSummonPermissionController _permission;
        [SerializeField] private BoatController _boatPrefab;
        [SerializeField] private LayerMask _landLayers;
        [SerializeField] private Vector3 _boatSpawnOffset = new Vector3(0f, -0.5f, 0f);
        [SerializeField, Min(1f)] private float _boardingTimeout = 10f;
        [SerializeField] private Vector3 _playerSpawnOrigin = new Vector3(0f, 1f, 0f);
        [SerializeField] private Vector3 _playerSpawnStep = new Vector3(0f, 0f, 8f);
        private uint _nextActorId;

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            StartCoroutine(BoardLocalPlayer());
        }

        public override void OnStartServer()
        {
            _nextActorId = 0;
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient connection)
        {
            // 부두 방향으로 나열하면 원격 참가자의 배가 육지와 겹치므로 수면 쪽으로 간격을 둔다.
            Vector3 spawnPosition = _playerSpawnOrigin + _playerSpawnStep * _nextActorId;
            GameObject player = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            player.GetComponent<Lighthouse.Network.Connections.Net.NetworkActor>().AssignActorId(++_nextActorId);
            NetworkServer.AddPlayerForConnection(connection, player);

            PlayerController controller = player.GetComponent<PlayerController>();
            if (_boatPrefab != null)
            {
                BoatController boat = Instantiate(_boatPrefab, player.transform.position + _boatSpawnOffset, Quaternion.identity);
                boat.GetComponent<BoatContactBoarding>().ConfigureLandLayers(_landLayers);
                if (boat.TrySetOwner(controller))
                {
                    NetworkServer.Spawn(boat.gameObject, connection);
                    controller.TryAssignOwnedBoat(boat);
                }
                else
                {
                    Destroy(boat.gameObject);
                }
            }

            // 시험 씬에서만 호스트 플레이어를 지정하고 원격 참가자에게는 권한을 주지 않는다.
            if (connection == NetworkServer.localConnection && _permission != null)
            {
                _permission.SetAuthority(new TrialSeaSummonAuthority(_nextActorId));
            }
        }

        public override void OnServerDisconnect(NetworkConnectionToClient connection)
        {
            if (connection == NetworkServer.localConnection && _permission != null)
            {
                _permission.SetAuthority(null);
            }
            base.OnServerDisconnect(connection);
        }

        private IEnumerator BoardLocalPlayer()
        {
            float expiresAt = Time.unscaledTime + _boardingTimeout;
            var delay = new WaitForSecondsRealtime(0.25f);
            while (NetworkClient.active && Time.unscaledTime < expiresAt)
            {
                if (NetworkClient.localPlayer != null
                    && NetworkClient.localPlayer.TryGetComponent(out PlayerController player)
                    && player.OwnedBoat != null)
                {
                    if (player.IsOnBoat)
                    {
                        if (_permission != null)
                        {
                            _permission.RequestOwnPermission();
                        }
                        yield break;
                    }
                    // 시험에서도 강제로 탑승 값을 바꾸지 않고 실제 승선 요청을 사용한다.
                    player.TryBoard(player.OwnedBoat);
                }
                yield return delay;
            }
            if (NetworkClient.active)
            {
                Debug.LogWarning($"[{nameof(SeaSummonTestNetworkManager)}] 자동 승선 실패: 소유 배와 플레이어의 거리를 확인해 주세요.");
            }
        }
    }
}
