using System.Collections.Generic;
using System.Reflection;
using Lighthouse.World.Debris.Rules;
using Lighthouse.World.Debris.Server;
using Lighthouse.World.Islands.Net;
using Lighthouse.World.Islands.Sandbox;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules.Mocks;
using UnityEngine;

namespace Lighthouse.World.Debris.Sandbox
{
    public static class ShoreDropBakeProbe
    {
        private const short ProbeIndex = 1;
        private const float PlacementOffset = 3000f;
        private const float IslandRadius = 33f;
        private const float RequestDistance = 30f;
        private const int Directions = 8;
        private const int Slots = 4;
        private const string SearchRadiusField = "_spotSearchRadius";

        public static string Measure(GameObject prefab, float yawDegrees, float searchRadius)
        {
            var position = new Vector3(PlacementOffset, 0f, PlacementOffset);
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.Euler(0f, yawDegrees, 0f));
            DebrisConfig config = ScriptableObject.CreateInstance<DebrisConfig>();
            try
            {
                typeof(DebrisConfig).GetField(SearchRadiusField, BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(config, searchRadius);
                Physics.SyncTransforms();

                var ground = new IslandGroundHeightSource(new WalkBoundaryBakeProbe.SingleRoot(instance.transform));
                var placements = new List<IslandPlacement>
                {
                    new IslandPlacement(ProbeIndex, 0, 0, SeaZone.Inner, new Vector2(position.x, position.z), IslandRadius, yawDegrees)
                };
                var locator = new ShoreDropLocator(placements, ground, new FlatWorldQuery(), new OpenGate(), config);

                int total = 0;
                int failed = 0;
                var reasons = new Dictionary<ShoreFailure, int>();
                var where = new System.Text.StringBuilder();
                float minGap = float.MaxValue;
                for (int i = 0; i < Directions; i++)
                {
                    float angle = i * Mathf.PI * 2f / Directions;
                    var origin = position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * RequestDistance;
                    var slotPoints = new List<Vector3>();
                    for (int slot = 0; slot < Slots; slot++)
                    {
                        total++;
                        ShoreDropResult result = locator.GetDropPoint(new ShoreDropRequest(origin, DebrisKind.Resource, slot));
                        if (result.IsSuccess)
                        {
                            for (int k = 0; k < slotPoints.Count; k++)
                            {
                                minGap = Mathf.Min(minGap, Vector3.Distance(slotPoints[k], result.Position));
                            }

                            slotPoints.Add(result.Position);
                        }
                        else
                        {
                            failed++;
                            where.Append($" d{i}s{slot}");
                            reasons.TryGetValue(result.Failure, out int count);
                            reasons[result.Failure] = count + 1;
                        }
                    }
                }

                var detail = new System.Text.StringBuilder();
                foreach (KeyValuePair<ShoreFailure, int> pair in reasons)
                {
                    detail.Append($" {pair.Key}={pair.Value}");
                }

                return $"{failed}/{total}{detail} minSlotGap={minGap:F2} [{where}]";
            }
            finally
            {
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(instance);
            }
        }

        private sealed class OpenGate : IRegionGate
        {
            public SeaZone UnlockedZones => SeaZone.All;

            public int GetUnlockDay(SeaZone zone)
            {
                return 1;
            }
        }
    }
}
