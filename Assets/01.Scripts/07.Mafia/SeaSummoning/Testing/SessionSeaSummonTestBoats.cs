using Lighthouse.Network.Session.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Mafia.SeaSummoning.Testing
{
    // 작업대와 난파 복귀 연결 없이 바다 소환을 독립적으로 시험하기 위한 배 생성기다.
    public sealed class SessionSeaSummonTestBoats : MonoBehaviour
    {
        [SerializeField] private LighthouseNetworkManager _manager;
        [SerializeField] private BoatController _boatPrefab;
        [SerializeField] private LayerMask _landLayers;
        [SerializeField] private Vector3 _boatSpawnOffset = new Vector3(0f, -0.5f, 0f);

        private void OnEnable()
        {
            if (_manager != null)
            {
                _manager.ActorJoined += HandleActorJoined;
            }
        }

        private void OnDisable()
        {
            if (_manager != null)
            {
                _manager.ActorJoined -= HandleActorJoined;
            }
        }

        private void HandleActorJoined(uint actorId, NetworkConnectionToClient connection)
        {
            if (!NetworkServer.active || connection == null || connection.identity == null
                || !connection.identity.TryGetComponent(out PlayerController player)
                || player.OwnedBoat != null || _boatPrefab == null || _manager == null
                || !_manager.spawnPrefabs.Contains(_boatPrefab.gameObject))
            {
                return;
            }

            BoatController boat = Instantiate(_boatPrefab, player.transform.position + _boatSpawnOffset,
                player.transform.rotation);
            boat.GetComponent<BoatContactBoarding>().ConfigureLandLayers(_landLayers);
            if (!boat.TrySetOwner(player))
            {
                Destroy(boat.gameObject);
                return;
            }

            NetworkServer.Spawn(boat.gameObject, connection);
            if (!player.TryAssignOwnedBoat(boat))
            {
                NetworkServer.Destroy(boat.gameObject);
            }
        }
    }
}
