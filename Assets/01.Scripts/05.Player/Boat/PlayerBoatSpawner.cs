using Lighthouse.Network.Session.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Player.Boat
{
    [DisallowMultipleComponent]
    public sealed class PlayerBoatSpawner : MonoBehaviour
    {
        [SerializeField]
        private LighthouseNetworkManager _networkManager;
        [SerializeField]
        private BoatController _boatPrefab;
        [SerializeField]
        private Vector3 _spawnOffset = new Vector3(1.5f, -0.2f, 0f);
        [SerializeField]
        private LayerMask _landLayers;

        private void Awake()
        {
            if (!_networkManager)
            {
                _networkManager = GetComponent<LighthouseNetworkManager>();
            }
        }

        private void OnEnable()
        {
            if (!_networkManager)
            {
                return;
            }

            _networkManager.ActorJoined += HandleActorJoined;
        }

        private void OnDisable()
        {
            if (_networkManager)
            {
                _networkManager.ActorJoined -= HandleActorJoined;
            }
        }

        private void HandleActorJoined(uint actorId, NetworkConnectionToClient connection)
        {
            if (!NetworkServer.active || connection == null || !connection.identity
                || !connection.identity.TryGetComponent(out PlayerController player)
                || player.OwnedBoat)
            {
                return;
            }

            if (!_boatPrefab || !_networkManager.spawnPrefabs.Contains(_boatPrefab.gameObject))
            {
                return;
            }

            Vector3 position = player.transform.position + player.transform.rotation * _spawnOffset;
            BoatController boat = Instantiate(_boatPrefab, position, player.transform.rotation);
            boat.name = $"{_boatPrefab.name} [actor={actorId}]";
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
