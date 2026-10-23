using Lighthouse.Monsters.Spawning.Server;
using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Spawning.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Spawning.Server
{
    [DisallowMultipleComponent]
    public sealed class WorldMonsterSpawnHost : MonoBehaviour
    {
        [SerializeField] private MapStateNet _mapState;
        [SerializeField] private MonsterSpawnManager _manager;
        [SerializeField] private MonsterSpawnBindings _bindings;
        [SerializeField, Min(0.5f)] private float _syncInterval = 3f;

        private WorldMonsterSpawnSession _session;
        private float _nextSyncTime;

        public MonsterSpawnManager Manager => _manager;
        public int RegisteredCount => _session != null ? _session.RegisteredCount : 0;

        private void Awake()
        {
            if (_mapState == null || _manager == null || _bindings == null)
            {
                Debug.LogError($"[{nameof(WorldMonsterSpawnHost)}] MapStateNet, MonsterSpawnManager and MonsterSpawnBindings are required.");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _mapState.LayoutReady += HandleLayoutReady;
            _mapState.LayoutReleased += HandleLayoutReleased;

            if (_mapState.IsLayoutReady)
            {
                HandleLayoutReady(_mapState.Layout, _mapState.Info);
            }
        }

        private void OnDisable()
        {
            _mapState.LayoutReady -= HandleLayoutReady;
            _mapState.LayoutReleased -= HandleLayoutReleased;
            EndSession();
        }

        private void Update()
        {
            if (_session == null)
            {
                return;
            }

            if (!NetworkServer.active)
            {
                EndSession();
                return;
            }

            if (Time.unscaledTime < _nextSyncTime)
            {
                return;
            }

            _nextSyncTime = Time.unscaledTime + _syncInterval;
            _session.Sync();
        }

        private void HandleLayoutReady(MapLayout layout, MapSeedInfo info)
        {
            if (!NetworkServer.active)
            {
                return;
            }

            EndSession();

            if (!_bindings.TryValidate(out string error))
            {
                Debug.LogError($"[{nameof(WorldMonsterSpawnHost)}] Invalid bindings: {error}");
                return;
            }

            if (!WorldServices.TryGet<ISpawnZoneProvider>(out ISpawnZoneProvider spawners))
            {
                Debug.LogError($"[{nameof(WorldMonsterSpawnHost)}] Spawn zone provider is not registered.");
                return;
            }

            _session = new WorldMonsterSpawnSession(_manager, _bindings, spawners);
            _session.Sync();
            _nextSyncTime = Time.unscaledTime + _syncInterval;
        }

        private void HandleLayoutReleased()
        {
            EndSession();
        }

        private void EndSession()
        {
            if (_session == null)
            {
                return;
            }

            _session.Dispose();
            _session = null;
        }
    }
}
