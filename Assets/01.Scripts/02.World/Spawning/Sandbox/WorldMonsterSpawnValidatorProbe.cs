using System.Collections.Generic;
using Lighthouse.Monsters.Spawning.Server;
using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Spawning.Server;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Spawning.Sandbox
{
    public class WorldMonsterSpawnValidatorProbe : MonoBehaviour
    {
        private const int SamplesPerBatch = 200;
        private const int RandomSeed = 1234;

        [SerializeField] private MapStateNet _mapState;
        [SerializeField] private WorldMonsterSpawnHost _host;
        [SerializeField] private WorldMonsterSpawnPositionValidator _validator;

        [ContextMenu("SpawnWorld/Run Exclusion Checks")]
        private void RunExclusionChecks()
        {
            List<string> failures = new List<string>();
            bool isPassed = SpawnExclusionMapChecks.RunAll(failures);
            Debug.Log($"[{nameof(WorldMonsterSpawnValidatorProbe)}] exclusion checks passed={isPassed} {string.Join(" | ", failures)}");
        }

        [ContextMenu("SpawnWorld/Verify Validator")]
        private void VerifyValidator()
        {
            if (_mapState == null || _host == null || _validator == null || _host.Manager == null
                || _mapState.Layout == null || !NetworkServer.active)
            {
                Debug.LogError($"[{nameof(WorldMonsterSpawnValidatorProbe)}] Server, layout, host and validator are required.");
                return;
            }

            MapLayout layout = _mapState.Layout;
            int blockedIslands = 0;
            for (int i = 0; i < layout.Islands.Count; i++)
            {
                Vector3 center = new Vector3(layout.Islands[i].Position.x, 0f, layout.Islands[i].Position.y);
                blockedIslands += _validator.TryResolvePosition(center, out _) ? 0 : 1;
            }

            int blockedReefs = 0;
            for (int i = 0; i < layout.Reefs.Count; i++)
            {
                Vector3 center = new Vector3(layout.Reefs[i].Position.x, 0f, layout.Reefs[i].Position.y);
                blockedReefs += _validator.TryResolvePosition(center, out _) ? 0 : 1;
            }

            System.Random random = new System.Random(RandomSeed);
            int accepted = 0;
            int rejected = 0;
            int fullyRejectedBatches = 0;
            int badHeights = 0;
            WorldServices.TryGet<IWorldQuery>(out IWorldQuery query);

            for (int b = 0; b < _host.Manager.Batches.Count; b++)
            {
                MonsterSpawnerRuntime batch = _host.Manager.Batches[b];
                int batchAccepted = 0;
                for (int s = 0; s < SamplesPerBatch; s++)
                {
                    double angle = random.NextDouble() * System.Math.PI * 2d;
                    double distance = batch.SpawnRadius * System.Math.Sqrt(random.NextDouble());
                    Vector3 candidate = batch.Position + new Vector3((float)(System.Math.Cos(angle) * distance), 0f, (float)(System.Math.Sin(angle) * distance));

                    if (_validator.TryResolvePosition(batch.Position, candidate, out Vector3 resolved))
                    {
                        accepted++;
                        batchAccepted++;
                        badHeights += query != null && Mathf.Abs(resolved.y - query.SampleWaterHeight(candidate)) > 0.001f ? 1 : 0;
                    }
                    else
                    {
                        rejected++;
                    }
                }

                fullyRejectedBatches += batchAccepted == 0 ? 1 : 0;
            }

            Debug.Log($"[{nameof(WorldMonsterSpawnValidatorProbe)}] islandsBlocked={blockedIslands}/{layout.Islands.Count} reefsBlocked={blockedReefs}/{layout.Reefs.Count} samples accepted={accepted} rejected={rejected} fullyRejectedBatches={fullyRejectedBatches}/{_host.Manager.Batches.Count} badHeights={badHeights}");
        }
    }
}
