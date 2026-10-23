using System.Collections.Generic;
using Lighthouse.Monsters.Spawning.Server;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Spawning.Rules;
using Lighthouse.World.Spawning.Server;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Spawning.Sandbox
{
    public class WorldMonsterSpawnProbe : MonoBehaviour
    {
        private const float QueryRadius = 100000f;

        [SerializeField] private WorldMonsterSpawnHost _host;

        private readonly List<SpawnerPoint> _points = new List<SpawnerPoint>();

        [ContextMenu("SpawnWorld/Run Ledger Checks")]
        private void RunLedgerChecks()
        {
            List<string> failures = new List<string>();
            bool isPassed = SpawnRegistrationLedgerChecks.RunAll(failures);
            Debug.Log($"[{nameof(WorldMonsterSpawnProbe)}] ledger checks passed={isPassed} {string.Join(" | ", failures)}");
        }

        [ContextMenu("SpawnWorld/Log State")]
        private void LogState()
        {
            if (_host == null)
            {
                Debug.LogError($"[{nameof(WorldMonsterSpawnProbe)}] Host is not assigned.");
                return;
            }

            int queried = 0;
            if (WorldServices.TryGet<ISpawnZoneProvider>(out ISpawnZoneProvider spawners))
            {
                _points.Clear();
                spawners.QuerySpawners(Vector3.zero, QueryRadius, _points);
                queried = _points.Count;
            }

            int batches = 0;
            int alive = 0;
            int failed = 0;
            if (_host.Manager != null)
            {
                for (int i = 0; i < _host.Manager.Batches.Count; i++)
                {
                    MonsterSpawnerRuntime batch = _host.Manager.Batches[i];
                    batches++;
                    alive += batch.AliveCount;
                    failed += batch.Error != null ? 1 : 0;
                }
            }

            Debug.Log($"[{nameof(WorldMonsterSpawnProbe)}] {Role()} unlockedPoints={queried} registered={_host.RegisteredCount} batches={batches} alive={alive} failedBatches={failed}");
        }

        private static string Role()
        {
            if (NetworkServer.active && NetworkClient.active)
            {
                return "Host";
            }

            return NetworkServer.active ? "Server" : "Client";
        }
    }
}
