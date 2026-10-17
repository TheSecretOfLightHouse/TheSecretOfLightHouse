using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Spawning.Rules
{
    [CreateAssetMenu(fileName = "SpawnerPlacementConfig",
        menuName = "TheLightHouse/World/SpawnerPlacementConfig")]
    public sealed class SpawnerPlacementConfig : ScriptableObject
    {
        [SerializeField, Min(1f)] private float _minDistance = 30f;
        [SerializeField, Min(1f)] private float _queryCellSize = 50f;
        [SerializeField, Min(0f)] private float _obstacleClearance = 10f;
        [SerializeField, Min(0f)] private float _safeRadius = 50f;
        [SerializeField] private SpawnerAllocation[] _allocations;

        public float MinDistance => _minDistance;
        public float QueryCellSize => _queryCellSize;
        public float ObstacleClearance => _obstacleClearance;
        public float SafeRadius => _safeRadius;

        public IReadOnlyList<SpawnerAllocation> Allocations =>
            _allocations ?? Array.Empty<SpawnerAllocation>();
    }
}
