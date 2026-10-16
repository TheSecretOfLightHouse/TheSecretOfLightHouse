using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Net;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Islands.Sandbox
{
    public class IslandNetTestSpawner : MonoBehaviour
    {
        [SerializeField] private IslandStateNet _prefab;
        [SerializeField] private ResourceCatalog _catalog;
        [SerializeField] private IslandArchetype _archetype;
        [SerializeField] private Transform _spotRoot;
        [SerializeField] private short _islandIndex = 1;
        [SerializeField] private int _seed = 12345;

        private IHarvestIslandRegistrar _registrar;
        private bool _isAttempted;

        private void Awake()
        {
            if (_prefab == null || _catalog == null || _archetype == null || _spotRoot == null)
            {
                Debug.LogError($"[{nameof(IslandNetTestSpawner)}] Prefab, catalog, archetype and spot root are required.");
                enabled = false;
            }
        }

        private void Update()
        {
            if (NetworkServer.active == _isAttempted)
            {
                return;
            }

            if (NetworkServer.active)
            {
                TrySpawn();
            }
            else
            {
                Release();
            }

            _isAttempted = NetworkServer.active;
        }

        private void OnDestroy()
        {
            Release();
        }

        private void TrySpawn()
        {
            if (!WorldServices.TryGet(out IHarvestIslandRegistrar registrar))
            {
                Debug.LogError($"[{nameof(IslandNetTestSpawner)}] HarvestHost is required in the scene.");
                return;
            }

            HarvestSpot[] spots = _spotRoot.GetComponentsInChildren<HarvestSpot>(true);
            if (!HarvestIslandSetup.TryRegister(registrar, _catalog, _archetype, _islandIndex, new SeededRandom(_seed), spots))
            {
                Debug.LogError($"[{nameof(IslandNetTestSpawner)}] Island {_islandIndex} registration failed.");
                return;
            }

            _registrar = registrar;
            IslandStateNet island = Instantiate(_prefab, _spotRoot.position, Quaternion.identity);
            if (!island.ServerInitialize(_islandIndex))
            {
                Debug.LogError($"[{nameof(IslandNetTestSpawner)}] IHarvestChangeSource is not registered.");
                Destroy(island.gameObject);
                Release();
                return;
            }

            NetworkServer.Spawn(island.gameObject);
            Debug.Log($"[{nameof(IslandNetTestSpawner)}] spawned island={_islandIndex} spots={spots.Length}");
        }

        private void Release()
        {
            if (_registrar == null)
            {
                return;
            }

            _registrar.UnregisterIsland(_islandIndex);
            _registrar = null;
        }
    }
}
