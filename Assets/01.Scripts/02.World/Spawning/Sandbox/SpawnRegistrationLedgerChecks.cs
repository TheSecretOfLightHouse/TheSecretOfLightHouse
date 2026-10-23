using System.Collections.Generic;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Spawning.Rules;
using UnityEngine;

namespace Lighthouse.World.Spawning.Sandbox
{
    public static class SpawnRegistrationLedgerChecks
    {
        private static readonly Vector3 PositionA = new Vector3(10f, 0f, 20f);
        private static readonly Vector3 PositionB = new Vector3(-30f, 0f, 5f);

        public static bool RunAll(List<string> failures)
        {
            SpawnRegistrationLedger ledger = new SpawnRegistrationLedger();
            SpawnerPoint a = new SpawnerPoint(PositionA, SeaZone.Inner, 1);
            SpawnerPoint sameSpotOtherProfile = new SpawnerPoint(PositionA, SeaZone.Inner, 2);
            SpawnerPoint b = new SpawnerPoint(PositionB, SeaZone.Middle, 1);

            if (ledger.Contains(a))
            {
                failures.Add("empty ledger contains point");
            }

            if (!ledger.TryAdd(a, 11) || ledger.TryAdd(a, 12))
            {
                failures.Add("duplicate add was not rejected");
            }

            if (!ledger.Contains(a) || ledger.Count != 1)
            {
                failures.Add("added point missing");
            }

            if (ledger.Contains(sameSpotOtherProfile) || ledger.Contains(b))
            {
                failures.Add("different profile or position matched");
            }

            ledger.TryAdd(sameSpotOtherProfile, 13);
            ledger.TryAdd(b, 14);
            List<long> ids = new List<long>();
            ledger.CopyBatchIds(ids);
            if (ids.Count != 3 || !ids.Contains(11) || !ids.Contains(13) || !ids.Contains(14))
            {
                failures.Add("batch ids copy is wrong");
            }

            ledger.Clear();
            if (ledger.Count != 0 || ledger.Contains(a))
            {
                failures.Add("clear did not empty ledger");
            }

            return failures.Count == 0;
        }
    }
}
