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
        private Transform[] _boatSpawnPoints = new Transform[0];

        [SerializeField]
        private LayerMask _landLayers;

        private int _nextBoatSpawnIndex;

        private void Awake()
        {
            ResolveNetworkManager();
        }

        private void OnEnable()
        {
            _nextBoatSpawnIndex = 0;
            ResolveNetworkManager();

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

            if (_nextBoatSpawnIndex >= _boatSpawnPoints.Length || !_boatSpawnPoints[_nextBoatSpawnIndex])
            {
                return;
            }

            Transform spawnPoint = _boatSpawnPoints[_nextBoatSpawnIndex];

            BoatController boat = Instantiate(_boatPrefab, spawnPoint.position, spawnPoint.rotation);

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
                return;
            }

            _nextBoatSpawnIndex++;
        }

        private void ResolveNetworkManager()
        {
            if (_networkManager)
            {
                return;
            }

            _networkManager = GetComponent<LighthouseNetworkManager>();
            if (!_networkManager)
            {
                _networkManager = NetworkManager.singleton as LighthouseNetworkManager;
            }
        }
    }
}
