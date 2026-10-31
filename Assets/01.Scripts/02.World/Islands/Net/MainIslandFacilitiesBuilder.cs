using System;
using Lighthouse.Interaction.Targets.Net;
using Lighthouse.Mafia.Voting.Net;
using Lighthouse.Modules.Server;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.View;
using Lighthouse.Workbench.Net;
using Lighthouse.Workbench.View;
using Lighthouse.World.Islands.View;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Islands.Net
{
    // Runs after local island construction and before WorldReady. Art stays local;
    // only the host creates the two authoritative facilities.
    [DisallowMultipleComponent]
    public sealed class MainIslandFacilitiesBuilder : MonoBehaviour, ILocalMapBuilder
    {
        [SerializeField] private IslandInstanceSet _instances;
        [SerializeField] private WorkbenchNet _workbenchPrefab;
        [SerializeField] private TowerNet _towerPrefab;
        [SerializeField] private ModuleInteractionHandler _modules;
        [SerializeField] private VotingNetworkController _voting;
        [SerializeField] private WorkbenchScreen[] _workbenchScreens = new WorkbenchScreen[0];
        [SerializeField] private TowerScreen[] _towerScreens = new TowerScreen[0];
        [SerializeField] private FinalBattleHud[] _battleHuds = new FinalBattleHud[0];
        [SerializeField, Min(1f)] private float _bindingTimeoutSeconds = 30f;

        [SerializeField] private MainIslandPlayerSpawns _playerSpawns;

        private MainIslandPresentation _presentation;
        private WorkbenchNet _spawnedWorkbench;
        private TowerNet _spawnedTower;
        private bool _isBuilding;
        private bool _isBound;
        private float _deadline;
        private int _seed;

        public bool IsBound => _isBound;
        public WorkbenchNet Workbench { get; private set; }
        public TowerNet Tower { get; private set; }

        private void Awake()
        {
            // MainGame is loaded after the connection starts; register before scene readiness
            // allows incoming facility spawn messages on remote clients.
            if (_workbenchPrefab)
            {
                NetworkClient.RegisterPrefab(_workbenchPrefab.gameObject);
            }

            if (_towerPrefab)
            {
                NetworkClient.RegisterPrefab(_towerPrefab.gameObject);
            }
        }

        private void OnDisable()
        {
            Cancel();
        }

        public void Begin(MapGenerationConfig config, MapLayout layout, MapSeedInfo info)
        {
            Cancel();

            if (!isActiveAndEnabled || !_instances || !_workbenchPrefab || !_towerPrefab)
            {
                throw new InvalidOperationException("Main island facility configuration is incomplete.");
            }

            if (!_instances.TryGetRoot(0, out Transform island))
            {
                throw new InvalidOperationException("Main island index 0 has not been built.");
            }

            _presentation = island.GetComponent<MainIslandPresentation>();
            if (!_presentation || !_presentation.IsValid)
            {
                throw new InvalidOperationException("Main island prefab needs valid facility anchors and visuals.");
            }

            if (_presentation.SlotVisuals.Length != _towerPrefab.Layout.SlotCount)
            {
                throw new InvalidOperationException("Main island visuals do not match the tower layout.");
            }

            _seed = info.Seed;
            _deadline = Time.realtimeSinceStartup + _bindingTimeoutSeconds;
            _isBuilding = true;

            if (NetworkServer.active)
            {
                SpawnFacilities();
            }
        }

        public bool Step()
        {
            if (!_isBuilding || !_presentation)
            {
                throw new InvalidOperationException("Main island facility build is not active.");
            }

            if (!WorldServices.TryGet(out WorkbenchNet workbench)
                || !WorldServices.TryGet(out TowerNet tower))
            {
                if (Time.realtimeSinceStartup > _deadline)
                {
                    throw new InvalidOperationException("Timed out waiting for network facilities.");
                }

                return false;
            }

            tower.BindWorkbench(workbench);
            SummonCinematicView cinematic = tower.GetComponent<SummonCinematicView>();
            if (cinematic != null)
            {
                cinematic.BindIsland(_presentation.transform);
            }

            InteractionTarget workbenchTarget = workbench.GetComponent<InteractionTarget>();
            InteractionTarget towerTarget = tower.GetComponent<InteractionTarget>();
            for (int i = 0; i < _workbenchScreens.Length; i++)
            {
                if (_workbenchScreens[i])
                {
                    _workbenchScreens[i].Bind(workbench, workbenchTarget);
                }
            }

            for (int i = 0; i < _towerScreens.Length; i++)
            {
                if (_towerScreens[i])
                {
                    _towerScreens[i].Bind(tower, towerTarget);
                }
            }

            for (int i = 0; i < _battleHuds.Length; i++)
            {
                if (_battleHuds[i])
                {
                    _battleHuds[i].Bind(tower.GetComponent<TowerMatchNet>());
                }
            }

            if (_playerSpawns)
            {
                _playerSpawns.Bind(_presentation.PlayerSpawnPoints, _presentation.BoatSpawnPoints, workbench, _modules, _seed);
            }

            Workbench = workbench;
            Tower = tower;
            _isBuilding = false;
            _isBound = true;
            return true;
        }

        public void Cancel()
        {
            _isBuilding = false;
            _isBound = false;
            Workbench = null;
            Tower = null;
            _presentation = null;
            if (_playerSpawns)
            {
                _playerSpawns.ResetBindings();
            }

            for (int i = 0; i < _towerScreens.Length; i++)
            {
                if (_towerScreens[i])
                {
                    _towerScreens[i].Bind(null, null);
                }
            }

            for (int i = 0; i < _workbenchScreens.Length; i++)
            {
                if (_workbenchScreens[i])
                {
                    _workbenchScreens[i].Bind(null, null);
                }
            }

            for (int i = 0; i < _battleHuds.Length; i++)
            {
                if (_battleHuds[i])
                {
                    _battleHuds[i].Bind(null);
                }
            }

            if (NetworkServer.active)
            {
                if (_spawnedTower)
                {
                    NetworkServer.Destroy(_spawnedTower.gameObject);
                }

                if (_spawnedWorkbench)
                {
                    NetworkServer.Destroy(_spawnedWorkbench.gameObject);
                }
            }

            _spawnedTower = null;
            _spawnedWorkbench = null;
        }

        [Server]
        private void SpawnFacilities()
        {
            if (!_modules || !_voting)
            {
                throw new InvalidOperationException("Shared module and voting services are required.");
            }

            try
            {
                Transform benchAnchor = _presentation.WorkbenchAnchor;
                _spawnedWorkbench = Instantiate(_workbenchPrefab, benchAnchor.position, benchAnchor.rotation);
                NetworkServer.Spawn(_spawnedWorkbench.gameObject);

                Transform towerAnchor = _presentation.TowerAnchor;
                _spawnedTower = Instantiate(_towerPrefab, towerAnchor.position, towerAnchor.rotation);
                _spawnedTower.BindWorkbench(_spawnedWorkbench);
                _spawnedTower.GetComponent<TowerMatchFlow>().BindServices(_modules, _voting);
                NetworkServer.Spawn(_spawnedTower.gameObject);
            }
            catch
            {
                Cancel();
                throw;
            }
        }
    }
}
