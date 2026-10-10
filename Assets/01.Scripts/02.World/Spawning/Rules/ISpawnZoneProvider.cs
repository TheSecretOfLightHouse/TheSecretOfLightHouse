using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Spawning.Rules
{
    public interface ISpawnZoneProvider
    {
        void QuerySpawners(Vector3 center, float radius, List<SpawnerPoint> results);
    }
}
