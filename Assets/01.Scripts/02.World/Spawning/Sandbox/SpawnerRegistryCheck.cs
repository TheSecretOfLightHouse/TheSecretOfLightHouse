using System;
using System.Collections.Generic;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Spawning.Rules;
using Lighthouse.World.Spawning.Server;
using UnityEngine;

namespace Lighthouse.World.Spawning.Sandbox
{
    public sealed class SpawnerRegistryCheck : MonoBehaviour
    {
        [ContextMenu("Run Checks")]
        private void RunChecks()
        {
            var points = new[]
            {
                new SpawnerPoint(new Vector3(-10f, 0f, 0f), SeaZone.Inner, 1),
                new SpawnerPoint(new Vector3(10f, 0f, 0f), SeaZone.Middle, 2),
                new SpawnerPoint(new Vector3(20f, 0f, 0f), SeaZone.Outer, 3),
                new SpawnerPoint(new Vector3(0f, 20f, 0f), SeaZone.Inner, 1)
            };
            var registry = new SpawnerRegistry(points, 10f);
            var results = new List<SpawnerPoint>(points.Length);

            registry.QuerySpawners(Vector3.zero, 10f, results);
            Require(results.Count == 0, "Initial zones must be locked.");

            registry.SetUnlockedZones(SeaZone.Inner);
            registry.QuerySpawners(Vector3.zero, 10f, results);
            Require(results.Count == 1 && results[0].Position.x == -10f,
                "Radius boundary, negative cell or locked-zone filtering failed.");

            results.Clear();
            registry.SetUnlockedZones(SeaZone.All);
            registry.QuerySpawners(Vector3.zero, 10f, results);
            Require(results.Count == 2, "Unlocked radius query failed.");

            results.Clear();
            registry.QuerySpawners(new Vector3(20f, 0f, 0f), 0f, results);
            Require(results.Count == 1 && results[0].ProfileId == 3,
                "Zero-radius query failed.");

            results.Clear();
            registry.SetUnlockedZones(SeaZone.None);
            registry.QuerySpawners(Vector3.zero, 100f, results);
            Require(results.Count == 0, "Relocking failed.");

            registry.SetUnlockedZones(SeaZone.All);
            registry.QuerySpawners(Vector3.zero, float.MaxValue, results);
            Require(results.Count == 4, "Large-radius fallback failed.");

            points[0] = new SpawnerPoint(Vector3.zero, SeaZone.Outer, 99);
            results.Clear();
            registry.QuerySpawners(new Vector3(-10f, 0f, 0f), 0f, results);
            Require(results.Count == 1 && results[0].ProfileId == 1,
                "Registry must retain its own point snapshot.");

            Debug.Log($"[{nameof(SpawnerRegistryCheck)}] All checks passed.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
