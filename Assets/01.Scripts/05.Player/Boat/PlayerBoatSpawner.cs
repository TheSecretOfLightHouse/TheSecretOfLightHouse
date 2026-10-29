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

        public void BindMainIsland(Transform[] points, WorkbenchNet workbench, ModuleInteractionHandler modules, int seed)
        {
            if (points == null || points.Length != 4 || !workbench || !modules)
            {
                throw new System.InvalidOperationException("Main island boat configuration is incomplete.");
            }

            _boatSpawnPoints = (Transform[])points.Clone();
            var random = new System.Random(seed);
            for (int i = _boatSpawnPoints.Length - 1; i > 0; i--)
            {
                if (!_boatSpawnPoints[i])
                {
                    throw new System.InvalidOperationException("Boat spawn spot is missing.");
                }

                int next = random.Next(i + 1);
                Transform swap = _boatSpawnPoints[i];
                _boatSpawnPoints[i] = _boatSpawnPoints[next];
                _boatSpawnPoints[next] = swap;
            }

            if (!_boatSpawnPoints[0])
            {
                throw new System.InvalidOperationException("Boat spawn spot is missing.");
            }

            _nextBoatSpawnIndex = 0;
            _moduleWorkbench = workbench;
            _moduleHandler = modules;
            ResolveNetworkManager();
            if (_networkManager)
            {
                _networkManager.ActorJoined -= HandleActorJoined;
                _networkManager.ActorJoined += HandleActorJoined;
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

            if (!TryGetBoatPrefab(player, out BoatController boatPrefab))
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

            BoatController boat = Instantiate(boatPrefab, spawnPoint.position, spawnPoint.rotation);

            boat.name = $"{boatPrefab.name} [actor={actorId}]";
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

            if (!TryGetBoatPrefab(player, out BoatController boatPrefab))
            {
                return false;
            }

            BoatController instance = Instantiate(boatPrefab, spawnPoint.position, spawnPoint.rotation);

            instance.name = $"{boatPrefab.name} [actor={player.ActorID}]";
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

        private bool TryGetBoatPrefab(PlayerController player, out BoatController boatPrefab)
        {
            boatPrefab = player.BoatPrefab ? player.BoatPrefab : _boatPrefab;

            if (!_networkManager || !boatPrefab)
            {
                return false;
            }

            if (!_networkManager.spawnPrefabs.Contains(boatPrefab.gameObject))
            {
                Debug.LogError($"[{nameof(PlayerBoatSpawner)}] Boat prefab '{boatPrefab.name}' is not registered in NetworkManager.spawnPrefabs.");
                return false;
            }

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
