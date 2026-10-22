using Lighthouse.Modules.Server;
using Lighthouse.Network.Session.Net;
using Lighthouse.Workbench.Net;
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

        [SerializeField]
        private Transform _playerRecoveryPoint;

        [SerializeField]
        private WorkbenchNet _moduleWorkbench;
        [SerializeField]
        private ModuleInteractionHandler _moduleHandler;

        private int _nextBoatSpawnIndex;


        public Transform PlayerRecoveryPoint => _playerRecoveryPoint;

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

            if (!player.TryGetComponent(out PlayerWreckRecovery recovery))
            {
                return;
            }

            Transform spawnPoint = _boatSpawnPoints[_nextBoatSpawnIndex];
            if (!_moduleWorkbench || !_moduleHandler || !player.TryGetComponent(out PlayerInventory inventory))
            {
                return;
            }

            inventory.BindServerModuleSource(_moduleWorkbench, _moduleHandler, actorId);

            recovery.ConfigureRecovery(this, spawnPoint, actorId);

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

        [Server]
        public bool TryCreateRecoveryBoat(PlayerController player, Transform spawnPoint, out BoatController boat)
        {
            boat = null;

            if (!player || !player.IsDowned || !spawnPoint)
            {
                return false;
            }

            NetworkConnectionToClient connection = player.connectionToClient;

            if (connection == null || connection.identity != player.netIdentity)
            {
                return false;
            }

            if (!_networkManager || !_boatPrefab || !_networkManager.spawnPrefabs.Contains(_boatPrefab.gameObject))
            {
                return false;
            }

            BoatController instance = Instantiate(_boatPrefab, spawnPoint.position, spawnPoint.rotation);

            instance.GetComponent<BoatContactBoarding>().ConfigureLandLayers(_landLayers);

            if (!instance.TrySetOwner(player))
            {
                Destroy(instance.gameObject);
                return false;
            }

            NetworkServer.Spawn(instance.gameObject, connection);
            boat = instance;

            return true;
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
