using System.Collections.Generic;
using Lighthouse.Network.Session.Net;
using Lighthouse.Network.Session.Rules;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.IllusionIsland.Net;
using Lighthouse.World.IllusionIsland.Rules;
using Lighthouse.World.Islands.Net;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Spawning.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.IllusionIsland.Server
{
    [DisallowMultipleComponent]
    public class IllusionService : MonoBehaviour, IIllusionService, IIllusionFootprint
    {
        [SerializeField] private IllusionConfig _config;

        private readonly List<IllusionRecord> _records = new List<IllusionRecord>();
        private readonly List<IllusionIslandProxy> _proxies = new List<IllusionIslandProxy>();
        private readonly List<Vector3> _islandPositions = new List<Vector3>();
        private readonly List<Vector3> _spawnerPositions = new List<Vector3>();
        private readonly List<Vector3> _illusionPositions = new List<Vector3>();
        private readonly List<SpawnerPoint> _spawnerQuery = new List<SpawnerPoint>();

        private LighthouseNetworkManager _manager;
        private int _nextId = 1;

        public IReadOnlyList<IllusionRecord> Records => _records;
        public IllusionConfig Config => _config;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError($"[{nameof(IllusionService)}] IllusionConfig is required.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            IllusionPrefabRegistrar.Register(_config);
            WorldServices.Register<IIllusionService>(this);
            WorldServices.Register<IIllusionFootprint>(this);
            WorldServices.Register<IllusionService>(this);

            _manager = LighthouseNetworkManager.Instance;
            if (_manager != null)
            {
                _manager.MatchEnded += HandleMatchEnded;
            }
        }

        private void OnDisable()
        {
            if (_manager != null)
            {
                _manager.MatchEnded -= HandleMatchEnded;
                _manager = null;
            }

            WorldServices.Unregister<IIllusionService>(this);
            WorldServices.Unregister<IIllusionFootprint>(this);
            WorldServices.Unregister<IllusionService>(this);
            ClearAll();
        }

        public bool TryCreate(uint casterId, Vector3 position, out int illusionId)
        {
            illusionId = 0;
            if (!NetworkServer.active || !isActiveAndEnabled || _config == null || _config.IslandPrefab == null)
            {
                return false;
            }

            if (!IsPlacementValid(position))
            {
                return false;
            }

            GameObject instance = Instantiate(_config.IslandPrefab, position, Quaternion.identity);
            if (!instance.TryGetComponent(out IllusionIslandProxy proxy) || !instance.TryGetComponent(out NetworkIdentity _))
            {
                Destroy(instance);
                return false;
            }

            int id = _nextId++;
            proxy.ServerInitialize(_config, id);
            proxy.Removed += HandleProxyRemoved;
            _records.Add(new IllusionRecord(id, position, GetCurrentDay(), casterId));
            _proxies.Add(proxy);

            NetworkServer.Spawn(instance);
            if (proxy.netId == 0)
            {
                proxy.Removed -= HandleProxyRemoved;
                _records.RemoveAt(_records.Count - 1);
                _proxies.RemoveAt(_proxies.Count - 1);
                Destroy(instance);
                return false;
            }

            NotifyMafia(proxy, casterId);
            illusionId = id;
            return true;
        }

        public bool IsOnIllusion(Vector3 position)
        {
            if (_config == null)
            {
                return false;
            }

            float sqrRadius = _config.SummonBlockRadius * _config.SummonBlockRadius;
            for (int i = 0; i < _records.Count; i++)
            {
                float dx = position.x - _records[i].Position.x;
                float dz = position.z - _records[i].Position.z;
                if (dx * dx + dz * dz <= sqrRadius)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetRecord(int illusionId, out IllusionRecord record)
        {
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].Id == illusionId)
                {
                    record = _records[i];
                    return true;
                }
            }

            record = default;
            return false;
        }

        public void ClearAll()
        {
            while (_proxies.Count > 0)
            {
                int last = _proxies.Count - 1;
                IllusionIslandProxy proxy = _proxies[last];
                _proxies.RemoveAt(last);
                _records.RemoveAt(last);

                if (proxy == null)
                {
                    continue;
                }

                proxy.Removed -= HandleProxyRemoved;
                if (NetworkServer.active && proxy.netId != 0)
                {
                    NetworkServer.Destroy(proxy.gameObject);
                }
                else
                {
                    Destroy(proxy.gameObject);
                }
            }
        }

        private bool IsPlacementValid(Vector3 position)
        {
            _islandPositions.Clear();
            IReadOnlyList<IslandStateNet> islands = IslandStateNet.Active;
            for (int i = 0; i < islands.Count; i++)
            {
                if (islands[i] != null)
                {
                    _islandPositions.Add(islands[i].transform.position);
                }
            }

            _spawnerPositions.Clear();
            if (WorldServices.TryGet(out ISpawnZoneProvider spawners))
            {
                _spawnerQuery.Clear();
                spawners.QuerySpawners(position, _config.MinDistanceToSpawner, _spawnerQuery);
                for (int i = 0; i < _spawnerQuery.Count; i++)
                {
                    _spawnerPositions.Add(_spawnerQuery[i].Position);
                }
            }

            _illusionPositions.Clear();
            for (int i = 0; i < _records.Count; i++)
            {
                _illusionPositions.Add(_records[i].Position);
            }

            return IllusionPlacementRules.IsPlacementValid(
                position,
                _islandPositions,
                _config.MinDistanceToIsland,
                _spawnerPositions,
                _config.MinDistanceToSpawner,
                _illusionPositions,
                _config.MinDistanceToIllusion);
        }

        private static void NotifyMafia(IllusionIslandProxy proxy, uint casterId)
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            if (manager != null && manager.Connections.TryGetConnection(casterId, out NetworkConnectionToClient mafia))
            {
                proxy.ServerMarkForMafia(mafia);
            }
        }

        private static int GetCurrentDay()
        {
            return WorldServices.TryGet(out IGameClock clock) ? clock.DayIndex : 0;
        }

        private void HandleProxyRemoved(int illusionId)
        {
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].Id == illusionId)
                {
                    _records.RemoveAt(i);
                    _proxies.RemoveAt(i);
                    return;
                }
            }
        }

        private void HandleMatchEnded(uint matchId, SessionEndReason reason, uint actorId)
        {
            ClearAll();
        }
    }
}
