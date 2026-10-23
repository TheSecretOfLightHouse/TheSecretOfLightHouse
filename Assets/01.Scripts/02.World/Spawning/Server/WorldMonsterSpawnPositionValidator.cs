using Lighthouse.Monsters.Spawning.Server;
using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Spawning.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.Spawning.Server
{
    [DisallowMultipleComponent]
    public sealed class WorldMonsterSpawnPositionValidator : MonsterSpawnPositionValidator
    {
        [SerializeField] private MapStateNet _mapState;
        [SerializeField, Min(0f)] private float _islandMargin = 5f;
        [SerializeField, Min(0f)] private float _reefMargin = 5f;

        private SpawnExclusionMap _exclusions;
        private IWorldQuery _query;
        private IRegionGate _regions;

        private void Awake()
        {
            if (_mapState == null)
            {
                Debug.LogError($"[{nameof(WorldMonsterSpawnPositionValidator)}] MapStateNet is required.");
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
            Release();
        }

        public override bool TryResolvePosition(Vector3 candidate, out Vector3 position)
        {
            position = candidate;

            if (_exclusions == null || _query == null || _regions == null)
            {
                return false;
            }

            if (_exclusions.IsBlocked(candidate) || _query.IsSafeZone(candidate))
            {
                return false;
            }

            SeaZone zone = _query.GetZoneAt(candidate);
            if ((_regions.UnlockedZones & zone) == 0)
            {
                return false;
            }

            position = new Vector3(candidate.x, _query.SampleWaterHeight(candidate), candidate.z);
            return true;
        }

        public override bool TryResolvePosition(Vector3 origin, Vector3 candidate, out Vector3 position)
        {
            position = candidate;

            if (_query == null
                || _query.GetZoneAt(origin) != _query.GetZoneAt(candidate))
            {
                return false;
            }

            return TryResolvePosition(candidate, out position);
        }

        private void HandleLayoutReady(MapLayout layout, MapSeedInfo info)
        {
            if (!NetworkServer.active)
            {
                return;
            }

            Release();

            if (!WorldServices.TryGet<IWorldQuery>(out IWorldQuery query)
                || !WorldServices.TryGet<IRegionGate>(out IRegionGate regions))
            {
                Debug.LogError($"[{nameof(WorldMonsterSpawnPositionValidator)}] World query or region gate is not registered.");
                return;
            }

            _query = query;
            _regions = regions;
            _exclusions = new SpawnExclusionMap(layout, _islandMargin, _reefMargin);
        }

        private void HandleLayoutReleased()
        {
            Release();
        }

        private void Release()
        {
            _exclusions = null;
            _query = null;
            _regions = null;
        }
    }
}
