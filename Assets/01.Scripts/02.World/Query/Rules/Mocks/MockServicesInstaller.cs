#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Harvest.Rules.Mocks;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Ocean.Rules.Mocks;
using Lighthouse.World.Spawning.Rules;
using Lighthouse.World.Spawning.Rules.Mocks;
using UnityEngine;

namespace Lighthouse.World.Query.Rules.Mocks
{
    [DefaultExecutionOrder(-1000)]
    public class MockServicesInstaller : MonoBehaviour
    {
        private const int FakeSpawnerProfileId = 0;

        [SerializeField] private bool _registerWorldQuery = true;
        [SerializeField] private bool _registerForceFieldProvider = true;
        [SerializeField] private bool _registerSpawnZoneProvider = true;
        [SerializeField] private bool _registerHarvestRegistry = true;
        [SerializeField] private bool _registerHazardEvents = true;
        [SerializeField] private Vector3[] _fakeSpawnerPositions;
        [SerializeField] private int _fakeResourceId = 1;

        private IWorldQuery _worldQuery;
        private IForceFieldProvider _forceFieldProvider;
        private ISpawnZoneProvider _spawnZoneProvider;
        private IHarvestSpotRegistry _harvestRegistry;
        private IHazardEvents _hazardEvents;

        private void Awake()
        {
            if (_registerWorldQuery)
            {
                _worldQuery = new FlatWorldQuery();
                WorldServices.Register(_worldQuery);
            }

            if (_registerForceFieldProvider)
            {
                _forceFieldProvider = new ZeroForceFieldProvider();
                WorldServices.Register(_forceFieldProvider);
            }

            if (_registerSpawnZoneProvider)
            {
                _spawnZoneProvider = new FakeSpawnZoneProvider(BuildSpawnerPoints());
                WorldServices.Register(_spawnZoneProvider);
            }

            if (_registerHarvestRegistry)
            {
                _harvestRegistry = new FakeHarvestRegistry(_fakeResourceId);
                WorldServices.Register(_harvestRegistry);
                WorldServices.Register<IHarvestSpotQuery>(_harvestRegistry);
            }

            if (_registerHazardEvents)
            {
                _hazardEvents = new HazardEvents();
                WorldServices.Register(_hazardEvents);
            }
        }

        private void OnDestroy()
        {
            WorldServices.Unregister(_worldQuery);
            WorldServices.Unregister(_forceFieldProvider);
            WorldServices.Unregister(_spawnZoneProvider);
            WorldServices.Unregister(_harvestRegistry);
            WorldServices.Unregister<IHarvestSpotQuery>(_harvestRegistry);
            WorldServices.Unregister(_hazardEvents);
        }

        private List<SpawnerPoint> BuildSpawnerPoints()
        {
            var points = new List<SpawnerPoint>();

            if (_fakeSpawnerPositions == null)
            {
                return points;
            }

            for (int i = 0; i < _fakeSpawnerPositions.Length; i++)
            {
                points.Add(new SpawnerPoint(_fakeSpawnerPositions[i], Ocean.Rules.SeaZone.Inner, FakeSpawnerProfileId));
            }

            return points;
        }
    }
}
#endif
