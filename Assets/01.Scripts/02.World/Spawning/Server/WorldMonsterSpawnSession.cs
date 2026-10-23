using System;
using System.Collections.Generic;
using Lighthouse.Monsters.Spawning.Rules;
using Lighthouse.Monsters.Spawning.Server;
using Lighthouse.World.Spawning.Rules;
using UnityEngine;

namespace Lighthouse.World.Spawning.Server
{
    public sealed class WorldMonsterSpawnSession : IDisposable
    {
        private const float QueryRadius = 100000f;

        private readonly MonsterSpawnManager _manager;
        private readonly MonsterSpawnBindings _bindings;
        private readonly ISpawnZoneProvider _spawners;
        private readonly SpawnRegistrationLedger _ledger = new SpawnRegistrationLedger();
        private readonly List<SpawnerPoint> _points = new List<SpawnerPoint>();
        private readonly List<long> _batchIds = new List<long>();
        private readonly HashSet<int> _reportedProfiles = new HashSet<int>();
        private bool _hasReportedRegisterError;

        public int RegisteredCount => _ledger.Count;

        public WorldMonsterSpawnSession(
            MonsterSpawnManager manager,
            MonsterSpawnBindings bindings,
            ISpawnZoneProvider spawners)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _spawners = spawners ?? throw new ArgumentNullException(nameof(spawners));
        }

        public void Sync()
        {
            _points.Clear();
            _spawners.QuerySpawners(Vector3.zero, QueryRadius, _points);

            for (int i = 0; i < _points.Count; i++)
            {
                SpawnerPoint point = _points[i];
                if (_ledger.Contains(point))
                {
                    continue;
                }

                if (!_bindings.TryGet(point.ProfileId, out MonsterSpawnerData data))
                {
                    ReportMissingBinding(point.ProfileId);
                    continue;
                }

                if (!_manager.TryRegister(data, point.Position, out long batchId, out string error))
                {
                    ReportRegisterError(error);
                    return;
                }

                _ledger.TryAdd(point, batchId);
            }
        }

        public void Dispose()
        {
            _ledger.CopyBatchIds(_batchIds);
            if (_manager != null)
            {
                for (int i = 0; i < _batchIds.Count; i++)
                {
                    _manager.Unregister(_batchIds[i]);
                }
            }

            _ledger.Clear();
            _batchIds.Clear();
        }

        private void ReportMissingBinding(int profileId)
        {
            if (_reportedProfiles.Add(profileId))
            {
                Debug.LogWarning($"[{nameof(WorldMonsterSpawnSession)}] No monster binding for spawner profile {profileId}.");
            }
        }

        private void ReportRegisterError(string error)
        {
            if (_hasReportedRegisterError)
            {
                return;
            }

            _hasReportedRegisterError = true;
            Debug.LogWarning($"[{nameof(WorldMonsterSpawnSession)}] Register failed: {error}");
        }
    }
}
