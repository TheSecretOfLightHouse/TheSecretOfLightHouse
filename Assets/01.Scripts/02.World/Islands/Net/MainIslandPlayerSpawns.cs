using System;
using System.Collections.Generic;
using Lighthouse.Modules.Server;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Lighthouse.Player.Boat;
using Lighthouse.Workbench.Net;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Islands.Net
{
    [DisallowMultipleComponent]
    public sealed class MainIslandPlayerSpawns : MonoBehaviour, IGamePlayerSpawnSource
    {
        [SerializeField] private GameObject[] _playerPrefabs = new GameObject[0];
        [SerializeField] private PlayerBoatSpawner _boatSpawner;

        private Transform[] _playerPoints = new Transform[0];
        private readonly Dictionary<uint, Vector2Int> _assignments = new Dictionary<uint, Vector2Int>(4);
        private System.Random _random;
        private int _usedPoints;
        private int _usedPrefabs;

        public bool IsReady { get; private set; }

        private void Awake()
        {
            for (int i = 0; i < _playerPrefabs.Length; i++)
            {
                GameObject prefab = _playerPrefabs[i];
                if (!prefab)
                {
                    continue;
                }

                NetworkClient.RegisterPrefab(prefab);
                PlayerController player = prefab.GetComponent<PlayerController>();
                if (player && player.BoatPrefab)
                {
                    NetworkClient.RegisterPrefab(player.BoatPrefab.gameObject);
                }
            }
        }

        private void OnEnable()
        {
            WorldServices.Register<IGamePlayerSpawnSource>(this);
        }

        private void OnDisable()
        {
            ResetBindings();
            WorldServices.Unregister<IGamePlayerSpawnSource>(this);
        }

        public void Bind(Transform[] playerPoints, Transform[] boatPoints, WorkbenchNet workbench, ModuleInteractionHandler modules, int seed)
        {
            _playerPoints = playerPoints;
            if (_playerPoints == null || _playerPoints.Length != 4 || _playerPrefabs.Length != 4 || !_boatSpawner)
            {
                throw new InvalidOperationException("Main island needs four player spots, four jobs and a boat spawner.");
            }

            for (int i = 0; i < 4; i++)
            {
                if (!_playerPoints[i] || !_playerPrefabs[i]
                    || !_playerPrefabs[i].TryGetComponent(out PlayerController player) || !player.BoatPrefab)
                {
                    throw new InvalidOperationException("Main island player configuration is incomplete.");
                }
            }

            ResetBindings();
            _random = new System.Random(seed ^ 0x13572);
            if (NetworkServer.active)
            {
                LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
                if (manager)
                {
                    for (int i = 0; i < _playerPrefabs.Length; i++)
                    {
                        RegisterServerPrefab(manager, _playerPrefabs[i]);
                        RegisterServerPrefab(manager, _playerPrefabs[i].GetComponent<PlayerController>().BoatPrefab.gameObject);
                    }
                }

                _boatSpawner.BindMainIsland(boatPoints, workbench, modules, seed ^ 0x56321);
            }

            IsReady = true;
        }

        public void ResetBindings()
        {
            IsReady = false;
            _assignments.Clear();
            _usedPoints = 0;
            _usedPrefabs = 0;
        }

        public bool TryReserve(uint actorId, out GameObject prefab, out Transform point)
        {
            prefab = null;
            point = null;
            if (!IsReady)
            {
                return false;
            }

            if (!_assignments.TryGetValue(actorId, out Vector2Int selection))
            {
                int pointIndex = PickUnused(_usedPoints);
                int prefabIndex = PickUnused(_usedPrefabs);
                if (pointIndex < 0 || prefabIndex < 0)
                {
                    return false;
                }

                selection = new Vector2Int(pointIndex, prefabIndex);
                _assignments.Add(actorId, selection);
                _usedPoints |= 1 << pointIndex;
                _usedPrefabs |= 1 << prefabIndex;
            }

            point = _playerPoints[selection.x];
            prefab = _playerPrefabs[selection.y];
            return true;
        }

        public void Release(uint actorId)
        {
            if (_assignments.TryGetValue(actorId, out Vector2Int selection))
            {
                _usedPoints &= ~(1 << selection.x);
                _usedPrefabs &= ~(1 << selection.y);
                _assignments.Remove(actorId);
            }
        }

        private int PickUnused(int used)
        {
            int available = 0;
            for (int i = 0; i < 4; i++)
            {
                if ((used & (1 << i)) == 0)
                {
                    available++;
                }
            }

            if (available == 0)
            {
                return -1;
            }

            int choice = _random.Next(available);
            for (int i = 0; i < 4; i++)
            {
                if ((used & (1 << i)) == 0 && choice-- == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void RegisterServerPrefab(LighthouseNetworkManager manager, GameObject prefab)
        {
            if (!manager.spawnPrefabs.Contains(prefab))
            {
                manager.spawnPrefabs.Add(prefab);
            }
        }
    }
}
