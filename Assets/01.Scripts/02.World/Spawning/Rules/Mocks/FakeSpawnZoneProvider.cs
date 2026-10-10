using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Spawning.Rules.Mocks
{
    public class FakeSpawnZoneProvider : ISpawnZoneProvider
    {
        private readonly IReadOnlyList<SpawnerPoint> _points;

        public FakeSpawnZoneProvider(IReadOnlyList<SpawnerPoint> points)
        {
            _points = points;
        }

        public void QuerySpawners(Vector3 center, float radius, List<SpawnerPoint> results)
        {
            if (_points == null || results == null)
            {
                return;
            }

            float sqrRadius = radius * radius;

            for (int i = 0; i < _points.Count; i++)
            {
                if ((_points[i].Position - center).sqrMagnitude <= sqrRadius)
                {
                    results.Add(_points[i]);
                }
            }
        }
    }
}
